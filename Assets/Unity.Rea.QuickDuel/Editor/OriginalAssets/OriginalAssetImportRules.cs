using System;
using System.Collections.Generic;
using System.IO;
using Unity.Rea.QuickDuel.Core;
using UnityEditor;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTools
{
    /// <summary>
    /// The import settings of the original art (UQD-T32), applied in code on every import rather than set by
    /// hand in the Inspector. Every image the asset manifest names (sprite frames and font atlases) imports as
    /// a single sprite at 1 pixel per unit with point filtering, no mipmaps and no compression, so the 2013 pixel
    /// art stays exact; a sprite frame's pivot is its GameMaker origin. Images the manifest does not name keep
    /// Unity's defaults.
    /// </summary>
    public sealed class OriginalAssetImportRules : AssetPostprocessor
    {
        // Bump to reimport every original image after changing the rules below.
        private const uint Version = 2;

        private static Dictionary<string, OriginalSprite> framesByPath;
        private static HashSet<string> atlases;
        private static DateTime loadedStamp;

        public override uint GetVersion() => Version;

        /// <summary>Drops the cached manifest so the next import reads it again.</summary>
        public static void Forget() => framesByPath = null;

        /// <summary>The sprite whose frame is at <paramref name="assetPath"/>, or null.</summary>
        public static OriginalSprite SpriteOf(string assetPath)
        {
            Load();
            return framesByPath != null && framesByPath.TryGetValue(assetPath, out var sprite) ? sprite : null;
        }

        /// <summary>True for a texture the manifest names (a sprite frame or a font atlas).</summary>
        public static bool Governs(string assetPath)
        {
            Load();
            return framesByPath != null && (framesByPath.ContainsKey(assetPath) || atlases.Contains(assetPath));
        }

        private void OnPreprocessTexture()
        {
            if (!Governs(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            Apply(importer, SpriteOf(assetPath));
        }

        /// <summary>Applies the pixel-art settings to <paramref name="importer"/>, pivoting on the sprite's origin when given.</summary>
        public static void Apply(TextureImporter importer, OriginalSprite sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (sprite != null)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2((float)sprite.PivotX, (float)sprite.PivotY);
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }
            importer.SetTextureSettings(settings);

            // A project in 2D mode slices a new image into sprites before any preprocessor runs; in Single mode
            // that sheet is dead data, so drop it to keep the committed .meta files to what the rules set.
            var serialized = new SerializedObject(importer);
            var sheet = serialized.FindProperty("m_SpriteSheet.m_Sprites");
            var names = serialized.FindProperty("m_InternalIDToNameTable");
            if ((sheet != null && sheet.arraySize > 0) || (names != null && names.arraySize > 0))
            {
                sheet?.ClearArray();
                names?.ClearArray();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Load()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OriginalAssetGenerator.ManifestPath));
            if (!File.Exists(path))
            {
                framesByPath = null;
                return;
            }

            var stamp = File.GetLastWriteTimeUtc(path);
            if (framesByPath != null && stamp == loadedStamp)
                return;

            try
            {
                var manifest = AssetManifest.Load(new DirectorySettingsSource(Path.GetDirectoryName(path)));
                var frames = new Dictionary<string, OriginalSprite>();
                foreach (var sprite in manifest.Sprites)
                    foreach (var frame in sprite.Frames)
                        frames[$"{OriginalAssetGenerator.GameFolder}/{frame}"] = sprite;
                var fonts = new HashSet<string>();
                foreach (var font in manifest.Fonts)
                    fonts.Add($"{OriginalAssetGenerator.GameFolder}/{font.Atlas}");
                framesByPath = frames;
                atlases = fonts;
                loadedStamp = stamp;
            }
            catch (SettingsException e)
            {
                framesByPath = null;
                Debug.LogError(e.Message);
            }
        }
    }
}
