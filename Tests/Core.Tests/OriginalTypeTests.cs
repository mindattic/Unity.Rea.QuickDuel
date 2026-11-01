using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The original's type (UQD-T36): each kind of text in the original's font and outline, draw_text_outline's
    // offsets in its own order, and the five shipped glyph tables read whole.
    public class OriginalTypeTests
    {
        private static readonly string Fonts = Path.Combine(FindRepo(), "Assets", "Unity.Rea.QuickDuel", "Fonts");

        private static string FindRepo()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Core.slnx")))
                    return dir.FullName;
            throw new DirectoryNotFoundException("Core.slnx not found above the test output.");
        }

        private static BitmapGlyphTable Table(string font) => BitmapGlyphTable.Parse(File.ReadAllText(Path.Combine(Fonts, font + ".glyphs.xml")));

        [Test]
        public void Each_kind_of_text_uses_the_original_font_and_outline()
        {
            Assert.That(OriginalType.Banner, Is.EqualTo(new TypeStyle("fnt_oldenglish", 1)));
            Assert.That(OriginalType.Countdown, Is.EqualTo(new TypeStyle("fnt_oldenglish", 1)));
            Assert.That(OriginalType.Title, Is.EqualTo(new TypeStyle("fnt_oldenglish", 2)));
            Assert.That(OriginalType.Name, Is.EqualTo(new TypeStyle("fnt_oldenglish_small", 1)));
            Assert.That(OriginalType.Intro, Is.EqualTo(new TypeStyle("fnt_mono", 2)));
            Assert.That(OriginalType.Hud, Is.EqualTo(new TypeStyle("fnt_basic", 1)));
            Assert.That(OriginalType.Button, Is.EqualTo(new TypeStyle("fnt_basic", 1)));
            Assert.That(OriginalType.Message, Is.EqualTo(new TypeStyle("fnt_basic", 0)), "mouseover_message_draw: plain draw_text");
            Assert.That(OriginalType.Small, Is.EqualTo(new TypeStyle("fnt_tiny", 1)));
            Assert.That(OriginalType.Fill, Is.EqualTo(new Rgb(255, 255, 255)));
            Assert.That(OriginalType.Outline, Is.EqualTo(new Rgb(0, 0, 0)));
        }

        [Test]
        public void The_outline_is_draw_text_outlines_square_x_outer_y_inner()
        {
            Assert.That(OriginalType.OutlineOffsets(0), Is.Empty);
            Assert.That(OriginalType.OutlineOffsets(1), Is.EqualTo(new[]
            {
                (-1, -1), (-1, 0), (-1, 1),
                (0, -1), (0, 0), (0, 1),
                (1, -1), (1, 0), (1, 1),
            }));
            var thick = OriginalType.OutlineOffsets(2);
            Assert.That(thick, Has.Count.EqualTo(25));
            Assert.That(thick.First(), Is.EqualTo((-2, -2)));
            Assert.That(thick.Last(), Is.EqualTo((2, 2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => OriginalType.OutlineOffsets(-1));
            Assert.Throws<ArgumentException>(() => new TypeStyle("", 1));
        }

        [Test]
        public void Every_shipped_font_reads_its_printable_ascii_glyphs()
        {
            Assert.That(OriginalType.Fonts, Is.EquivalentTo(new[] { "fnt_basic", "fnt_mono", "fnt_oldenglish", "fnt_oldenglish_small", "fnt_tiny" }));
            foreach (var font in OriginalType.Fonts)
            {
                var table = Table(font);
                Assert.That(table.Glyphs, Has.Count.EqualTo(96), font);
                for (var c = ' '; c <= '\u007f'; c++)
                    Assert.That(table.Has(c), Is.True, $"{font} '{c}'");
                Assert.That(table.LineHeight, Is.EqualTo(table.Glyphs.Max(g => g.Height)), font);
                Assert.That(table.Width("Get Ready"), Is.GreaterThan(0), font);
            }
        }

        [Test]
        public void A_glyph_keeps_its_atlas_rect_shift_and_offset()
        {
            var table = Table("fnt_oldenglish");
            Assert.That(table.Face, Is.EqualTo("Old English Text MT"));
            Assert.That(table.Size, Is.EqualTo(32));
            var bang = table['!'];
            Assert.That((bang.X, bang.Y, bang.Width, bang.Height, bang.Shift, bang.Offset), Is.EqualTo((76, 314, 7, 52, 15, 3)));
            Assert.That(table.Width("!!"), Is.EqualTo(30));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => _ = table['é']);
        }

        [Test]
        public void A_broken_glyph_table_is_refused()
        {
            Assert.Throws<FormatException>(() => BitmapGlyphTable.Parse(""));
            Assert.Throws<FormatException>(() => BitmapGlyphTable.Parse("<font><size>10</size><glyphs/></font>"));
            Assert.Throws<FormatException>(() => BitmapGlyphTable.Parse(
                "<font><size>10</size><glyphs><glyph character=\"65\" x=\"a\" y=\"0\" w=\"1\" h=\"1\" shift=\"1\" offset=\"0\"/></glyphs></font>"));
            Assert.Throws<FormatException>(() => BitmapGlyphTable.Parse(
                "<font><size>10</size><glyphs>" +
                "<glyph character=\"65\" x=\"0\" y=\"0\" w=\"1\" h=\"1\" shift=\"1\" offset=\"0\"/>" +
                "<glyph character=\"65\" x=\"0\" y=\"0\" w=\"1\" h=\"1\" shift=\"1\" offset=\"0\"/></glyphs></font>"));
        }
    }
}
