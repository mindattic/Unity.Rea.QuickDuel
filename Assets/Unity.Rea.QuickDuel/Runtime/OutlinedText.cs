using System.Collections.Generic;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The original's outlined text (UQD-T36), the one text style every piece of QuickDuel text shares: draw_text_outline
    // drew the string in black at each offset of a (2t + 1) square around the spot and then in white on top, in one of
    // the original's five bitmap fonts. This element does exactly that with UI Toolkit labels: one black layer per
    // OriginalType.OutlineOffsets entry, shifted by that many original pixels, and the fill layer last, all sharing the
    // style's font asset (OriginalFonts) at its native size, every original pixel drawn PixelScale stage pixels wide
    // like the arena and fighters. Alignment, wrapping and visibility are set on the element and the layers inherit or
    // follow them; opacity multiplies into every layer, as draw_text_outline_alpha drew each copy at that alpha.
    public sealed class OutlinedText : VisualElement
    {
        // The original's 640 x 480 room on the 1080-high stage: every original pixel is 2.25 stage pixels.
        public const float PixelScale = 1080f / FighterLineup.OriginalHeight;

        public const string UssClass = "outlined-text";
        public const string LayerUssClass = "outlined-text__layer";

        private readonly List<Label> outline = new List<Label>();
        private readonly Label fill;
        private string shown = "";

        public OutlinedText(TypeStyle style, float scale = 1f)
        {
            Style = style;
            pickingMode = PickingMode.Ignore;
            AddToClassList(UssClass);

            var font = OriginalFonts.Load(style.Font);
            LineHeight = (font != null ? font.faceInfo.lineHeight : OriginalFonts.FallbackLineHeight) * PixelScale * scale;
            if (font != null) this.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
            this.style.fontSize = LineHeight;

            var step = PixelScale * scale;
            foreach (var (x, y) in OriginalType.OutlineOffsets(style.Outline))
            {
                var layer = Layer("Outline");
                layer.style.translate = new Translate(x * step, y * step);
                outline.Add(layer);
            }
            fill = Layer("Fill");
            Fill = ToColor(OriginalType.Fill);
            OutlineColor = ToColor(OriginalType.Outline);
        }

        public TypeStyle Style { get; }

        // One line of this text, in stage pixels: the font's tallest cell at the original's scale.
        public float LineHeight { get; }

        public string text
        {
            get => shown;
            set
            {
                shown = value ?? "";
                foreach (var layer in outline) layer.text = shown;
                fill.text = shown;
            }
        }

        public Color Fill
        {
            get => fill.resolvedStyle.color;
            set => fill.style.color = value;
        }

        public Color OutlineColor
        {
            get => outline.Count > 0 ? outline[0].style.color.value : Color.clear;
            set { foreach (var layer in outline) layer.style.color = value; }
        }

        // The black copies under the fill, in draw_text_outline's order, and the fill itself.
        public IReadOnlyList<Label> OutlineLayers => outline;
        public Label FillLayer => fill;

        private Label Layer(string name)
        {
            // Every layer covers the element whole and inherits its font, size, alignment and wrapping; set inline so the
            // style holds on any panel, whatever style sheets it carries.
            var layer = new Label { name = name, text = "", pickingMode = PickingMode.Ignore };
            layer.AddToClassList(LayerUssClass);
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.top = 0;
            layer.style.right = 0;
            layer.style.bottom = 0;
            layer.style.marginLeft = layer.style.marginRight = layer.style.marginTop = layer.style.marginBottom = 0;
            layer.style.paddingLeft = layer.style.paddingRight = layer.style.paddingTop = layer.style.paddingBottom = 0;
            Add(layer);
            return layer;
        }

        private static Color ToColor(Rgb rgb) => new Color32(rgb.R, rgb.G, rgb.B, 255);
    }

    // The original's five bitmap fonts as UI Toolkit font assets (Resources/UI/Fonts/<id>.asset), built from their
    // committed atlases and glyph tables by the editor's OriginalFontBuilder.
    public static class OriginalFonts
    {
        public const string ResourceFolder = "UI/Fonts/";

        // The line height a style falls back to, in original pixels, when its font asset is missing.
        public const float FallbackLineHeight = 17f;

        private static readonly Dictionary<string, FontAsset> Loaded = new Dictionary<string, FontAsset>();

        public static FontAsset Load(string id)
        {
            if (Loaded.TryGetValue(id, out var font) && font != null) return font;
            font = Resources.Load<FontAsset>(ResourceFolder + id);
            if (font == null) Debug.LogWarning($"QuickDuel: the original font asset {ResourceFolder}{id} is missing; run QuickDuel > Build Original Font Assets.");
            Loaded[id] = font;
            return font;
        }
    }
}
