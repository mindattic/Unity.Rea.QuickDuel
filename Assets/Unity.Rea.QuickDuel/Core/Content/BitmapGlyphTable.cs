using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Unity.Rea.QuickDuel.Core
{
    // A GameMaker bitmap font's glyph table (UQD-T36), read from its .font.gmx XML (copied as Fonts/<id>.glyphs.xml by
    // UQD-T32). GameMaker cut every glyph as a cell whose top is the top of the line: the glyph's atlas rect is
    // (x, y, w, h) with y down, it is drawn `offset` pixels right of the pen, and the pen then moves `shift` pixels.
    // A cell's height is cropped below the glyph's lowest ink, so the line is as tall as the tallest cell.
    public sealed class BitmapGlyphTable
    {
        private readonly Dictionary<int, BitmapGlyph> byCharacter;

        private BitmapGlyphTable(string face, int size, List<BitmapGlyph> glyphs)
        {
            Face = face;
            Size = size;
            Glyphs = glyphs;
            byCharacter = new Dictionary<int, BitmapGlyph>();
            foreach (var glyph in glyphs)
            {
                if (byCharacter.ContainsKey(glyph.Character))
                    throw new FormatException($"The glyph table lists character {glyph.Character} twice.");
                byCharacter.Add(glyph.Character, glyph);
                LineHeight = Math.Max(LineHeight, glyph.Height);
            }
        }

        // The system face and point size GameMaker rendered the atlas from.
        public string Face { get; }
        public int Size { get; }
        public IReadOnlyList<BitmapGlyph> Glyphs { get; }

        // The tallest cell: the height of one line of this font, in original pixels.
        public int LineHeight { get; }

        public bool Has(char c) => byCharacter.ContainsKey(c);

        public BitmapGlyph this[char c] => byCharacter.TryGetValue(c, out var glyph)
            ? glyph
            : throw new KeyNotFoundException($"The font has no glyph for '{c}' ({(int)c}).");

        // How wide `text` is on one line, in original pixels: the sum of its shifts, as GameMaker's string_width.
        public int Width(string text)
        {
            var width = 0;
            foreach (var c in text ?? "")
                if (byCharacter.TryGetValue(c, out var glyph))
                    width += glyph.Shift;
            return width;
        }

        public static BitmapGlyphTable Parse(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) throw new FormatException("The glyph table is empty.");
            var document = new XmlDocument();
            document.LoadXml(xml);
            var font = document.SelectSingleNode("/font") ?? throw new FormatException("The glyph table has no <font> root.");
            var face = font.SelectSingleNode("name")?.InnerText.Trim() ?? "";
            var size = Int(font.SelectSingleNode("size")?.InnerText, "size");
            var glyphs = new List<BitmapGlyph>();
            var nodes = font.SelectNodes("glyphs/glyph");
            if (nodes == null || nodes.Count == 0) throw new FormatException("The glyph table lists no glyphs.");
            foreach (XmlNode node in nodes)
            {
                var glyph = new BitmapGlyph(
                    Int(Attribute(node, "character"), "character"),
                    Int(Attribute(node, "x"), "x"), Int(Attribute(node, "y"), "y"),
                    Int(Attribute(node, "w"), "w"), Int(Attribute(node, "h"), "h"),
                    Int(Attribute(node, "shift"), "shift"), Int(Attribute(node, "offset"), "offset"));
                if (glyph.Width < 0 || glyph.Height < 0 || glyph.X < 0 || glyph.Y < 0)
                    throw new FormatException($"Glyph {glyph.Character} has a negative rect.");
                glyphs.Add(glyph);
            }
            return new BitmapGlyphTable(face, size, glyphs);
        }

        private static string Attribute(XmlNode node, string name) => node.Attributes?[name]?.Value;

        private static int Int(string value, string what) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new FormatException($"The glyph table's {what} '{value}' is not a whole number.");
    }

    // One glyph cell: its character, its atlas rect in pixels (y down, from the atlas's top), how far right of the pen
    // it is drawn (offset) and how far the pen then moves (shift).
    public readonly struct BitmapGlyph
    {
        public BitmapGlyph(int character, int x, int y, int width, int height, int shift, int offset)
        {
            Character = character;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Shift = shift;
            Offset = offset;
        }

        public int Character { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int Shift { get; }
        public int Offset { get; }
    }
}
