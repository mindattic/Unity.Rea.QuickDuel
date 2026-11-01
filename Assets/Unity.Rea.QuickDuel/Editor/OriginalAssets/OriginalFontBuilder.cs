using System;
using System.IO;
using System.Reflection;
using Unity.Rea.QuickDuel.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace Unity.Rea.QuickDuel.EditorTools
{
    /// <summary>
    /// Turns the original's five GameMaker bitmap fonts (UQD-T32 copied each one's atlas and glyph table into
    /// <c>Fonts/</c>) into UI Toolkit font assets (UQD-T36), so every label draws the original's own glyphs: a static
    /// TextCore <see cref="FontAsset"/> per font at <c>Resources/UI/Fonts/&lt;id&gt;.asset</c>, its atlas the committed
    /// atlas's alpha, pixel for pixel (see <see cref="CoverageAtlas"/>), and its glyph and character tables the glyph table's cells, cut exactly where GameMaker cut
    /// them. Nothing is re-rasterised: the faces they came from (Old English Text MT, Monotype Corsiva, Estrangelo
    /// Edessa) are system fonts that cannot ship, and the atlases are the pixels the original drew.
    ///
    /// The face is measured in original pixels: a font's point size is its line height (the tallest cell), so a label
    /// whose font-size is that line height times the stage scale draws every original pixel at the same scale as the
    /// arena and fighters. Each cell's top is the line's top (GameMaker's cells start there), so a glyph's bearing is the
    /// whole line height above a baseline at the bottom of the line.
    ///
    /// Run from the menu or in batch mode via
    /// <c>-executeMethod Unity.Rea.QuickDuel.EditorTools.OriginalFontBuilder.Build</c>; <see cref="OriginalAssetGenerator"/>
    /// rebuilds them after every import and <see cref="DuelSceneBuilder"/> builds any that are missing.
    /// </summary>
    public static class OriginalFontBuilder
    {
        public const string Folder = "Assets/Unity.Rea.QuickDuel/Resources/UI/Fonts";
        public const string ResourceFolder = "UI/Fonts";

        [MenuItem("QuickDuel/Build Original Font Assets")]
        public static void Build()
        {
            foreach (var font in OriginalType.Fonts)
                BuildFont(font);
            AssetDatabase.SaveAssets();
            Debug.Log($"QuickDuel: built {OriginalType.Fonts.Count} original font assets in {Folder}");
        }

        /// <summary>Builds any of the font assets that are missing; leaves committed ones untouched.</summary>
        public static void Ensure()
        {
            var built = false;
            foreach (var font in OriginalType.Fonts)
            {
                if (AssetDatabase.LoadAssetAtPath<FontAsset>(AssetPath(font)) != null) continue;
                BuildFont(font);
                built = true;
            }
            if (built) AssetDatabase.SaveAssets();
        }

        public static string AssetPath(string font) => $"{Folder}/{font}.asset";

        public static FontAsset BuildFont(string id)
        {
            var atlasPath = $"{OriginalAssetGenerator.GameFolder}/{OriginalAssetCatalog.FontFolder}/{id}.png";
            var glyphPath = $"{OriginalAssetGenerator.GameFolder}/{OriginalAssetCatalog.FontFolder}/{id}.glyphs.xml";
            if (!File.Exists(Path.GetFullPath(atlasPath)))
                throw new FileNotFoundException($"The atlas of {id} is missing; run QuickDuel > Import Original Assets.", atlasPath);
            var atlas = CoverageAtlas(id, Path.GetFullPath(atlasPath));
            var table = BitmapGlyphTable.Parse(File.ReadAllText(Path.GetFullPath(glyphPath)));

            var asset = ScriptableObject.CreateInstance<FontAsset>();
            asset.name = id;
            Set(asset, "m_Version", "1.1.0");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            Set(asset, "m_AtlasWidth", atlas.width);
            Set(asset, "m_AtlasHeight", atlas.height);
            // A glyph cell is drawn whole, edge to edge, so there is no padding to sample around it.
            Set(asset, "m_AtlasPadding", 0);
            // A plain coverage bitmap (not a distance field), tinted by the text's colour like any TextCore bitmap font.
            Set(asset, "m_AtlasRenderMode", GlyphRenderMode.RASTER);
            asset.atlasTextures = new[] { atlas };
            asset.isMultiAtlasTexturesEnabled = false;

            var line = table.LineHeight;
            asset.faceInfo = new FaceInfo
            {
                familyName = table.Face,
                styleName = "Regular",
                pointSize = line,
                scale = 1f,
                lineHeight = line,
                ascentLine = line,
                capLine = line,
                meanLine = line / 2f,
                baseline = 0f,
                descentLine = 0f,
                underlineOffset = -1f,
                underlineThickness = 1f,
                strikethroughOffset = line / 2f,
                strikethroughThickness = 1f,
                tabWidth = table.Has(' ') ? table[' '].Shift * 4 : line,
            };

            var index = 0u;
            foreach (var cell in table.Glyphs)
            {
                // TextCore measures atlas rects from the bottom; GameMaker from the top.
                var rect = new GlyphRect(cell.X, atlas.height - cell.Y - cell.Height, cell.Width, cell.Height);
                var metrics = new GlyphMetrics(cell.Width, cell.Height, cell.Offset, line, cell.Shift);
                var glyph = new Glyph(index, metrics, rect, 1f, 0);
                asset.glyphTable.Add(glyph);
                asset.characterTable.Add(new Character((uint)cell.Character, asset, glyph));
                index++;
            }

            var material = new Material(BitmapShader()) { name = id + " Material" };
            material.SetTexture("_MainTex", atlas);
            material.SetFloat("_TextureWidth", atlas.width);
            material.SetFloat("_TextureHeight", atlas.height);
            asset.material = material;

            var path = AssetPath(id);
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Path.GetFullPath(Folder));
                AssetDatabase.Refresh();
            }
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(atlas, asset);
            AssetDatabase.AddObjectToAsset(material, asset);
            asset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // GameMaker's atlas is white glyphs whose alpha is their ink. TextCore draws a bitmap font from an Alpha8 atlas it
        // tints with the text's colour (an RGBA one it would draw as colour glyphs, always white), so the atlas the font
        // asset carries is that alpha channel, pixel for pixel, point-filtered so the original's pixels stay square.
        private static Texture2D CoverageAtlas(string id, string pngPath)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(pngPath)))
                    throw new InvalidDataException($"The atlas of {id} is not a readable PNG: {pngPath}");
                var pixels = source.GetPixels32();
                var coverage = new byte[pixels.Length];
                for (var i = 0; i < pixels.Length; i++)
                    coverage[i] = pixels[i].a;
                var atlas = new Texture2D(source.width, source.height, TextureFormat.Alpha8, false)
                {
                    name = id + " Atlas",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                atlas.SetPixelData(coverage, 0);
                atlas.Apply(false, false);
                return atlas;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        // The bitmap text shader TextCore gives its own bitmap font assets (TextShaderUtilities.ShaderRef_MobileBitmap).
        private static Shader BitmapShader()
        {
            var utilities = typeof(FontAsset).Assembly.GetType("UnityEngine.TextCore.Text.TextShaderUtilities");
            var property = utilities?.GetProperty("ShaderRef_MobileBitmap", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var shader = property?.GetValue(null) as Shader;
            return shader != null ? shader : throw new InvalidOperationException("TextCore's bitmap text shader was not found.");
        }

        // FontAsset keeps its atlas size, padding and render mode behind internal setters; its serialized fields are
        // the supported way an editor tool writes them.
        private static void Set(object target, string field, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, field);
        }
    }
}
