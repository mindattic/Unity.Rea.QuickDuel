using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // One arena photo credit from the original's datafiles/credits.txt: the name it gave the picture and the address
    // (or addresses) it was taken from.
    public sealed class CreditsEntry
    {
        public CreditsEntry(string name, IReadOnlyList<string> sources)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A credit needs a name.", nameof(name));
            if (sources == null || sources.Count == 0) throw new ArgumentException($"The credit '{name}' names no source.", nameof(sources));
            Name = name;
            Sources = sources;
        }

        public string Name { get; }
        public IReadOnlyList<string> Sources { get; }
    }

    // credits.txt as the original wrote it: blocks of lines parted by one or more blank lines, each block a picture's
    // name on its first line and the address it came from on each line after it.
    public static class CreditsText
    {
        public static List<CreditsEntry> Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var entries = new List<CreditsEntry>();
            string name = null;
            var sources = new List<string>();
            void Close()
            {
                if (name != null) entries.Add(new CreditsEntry(name, sources.ToArray()));
                name = null;
                sources.Clear();
            }
            foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    Close();
                else if (name == null)
                    name = line;
                else
                    sources.Add(line);
            }
            Close();
            if (entries.Count == 0) throw new FormatException("The credits text holds no credit.");
            return entries;
        }
    }

    // What a line of the credits is, and so how it is drawn.
    public enum CreditsLineKind { Heading, Byline, Author, Section, Name, Source }

    // One line of the roll: its text, its kind and its top, in room pixels, below the top of the roll.
    public sealed class CreditsLine
    {
        internal CreditsLine(CreditsLineKind kind, string text, int top)
        {
            Kind = kind;
            Text = text;
            Top = top;
        }

        public CreditsLineKind Kind { get; }
        public string Text { get; }
        public int Top { get; }

        // The original's type each kind is drawn in: the heading in fnt_oldenglish as its banners were, the author's
        // name in fnt_oldenglish_small as the winner's name was, the byline, section and picture names in fnt_basic as the
        // buttons were, and the addresses in fnt_tiny.
        public TypeStyle Style => StyleOf(Kind);

        public static TypeStyle StyleOf(CreditsLineKind kind)
        {
            switch (kind)
            {
                case CreditsLineKind.Heading: return OriginalType.Banner;
                case CreditsLineKind.Author: return OriginalType.Name;
                case CreditsLineKind.Source: return OriginalType.Small;
                default: return OriginalType.Button;
            }
        }
    }

    // The credits screen (UQD-T47), which the original never had: its Credits button ("Who made what?") only clicked,
    // its room_goto commented out. The rebuild shows what that button was for: the game's author (the project's
    // option_author) and every arena photo credit in credits.txt, rolling up the room from its foot a pixel a 60 fps
    // step (the menu background's own speed) and starting again from the foot once the last line has gone over the top.
    // Its layout (the line advances and gaps below) is the rebuild's own (BIBLE).
    public sealed class CreditsRoll
    {
        // A pixel a step, as bg_splatter_scroll moves.
        public const int Speed = 1;

        // The black laid over the room under the roll, so the text reads over the scrolling blood.
        public const double Shade = 0.8;

        // The advance under each kind of line, in room pixels: each font's line height and a little air.
        public const int HeadingAdvance = 44;
        public const int BylineAdvance = 18;
        public const int AuthorAdvance = 34;
        public const int SectionAdvance = 26;
        public const int NameAdvance = 17;
        public const int SourceAdvance = 13;

        // The gap after each credit's last address.
        public const int EntryGap = 12;

        public const string Heading = "Credits";
        public const string Byline = "A game by";
        public const string Section = "Arena photographs";

        private readonly List<CreditsLine> lines = new List<CreditsLine>();

        public CreditsRoll(string author, IReadOnlyList<CreditsEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(author)) throw new ArgumentException("The credits need the game's author.", nameof(author));
            Author = author;
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));

            var top = 0;
            void Add(CreditsLineKind kind, string text, int advance)
            {
                lines.Add(new CreditsLine(kind, text, top));
                top += advance;
            }
            Add(CreditsLineKind.Heading, Heading, HeadingAdvance);
            Add(CreditsLineKind.Byline, Byline, BylineAdvance);
            Add(CreditsLineKind.Author, author, AuthorAdvance);
            if (entries.Count > 0) Add(CreditsLineKind.Section, Section, SectionAdvance);
            foreach (var entry in entries)
            {
                Add(CreditsLineKind.Name, entry.Name, NameAdvance);
                foreach (var source in entry.Sources)
                    Add(CreditsLineKind.Source, source, SourceAdvance);
                top += EntryGap;
            }
            Height = top;
        }

        // credits.txt read and laid out under the author's credit.
        public static CreditsRoll FromText(string author, string creditsText) => new CreditsRoll(author, CreditsText.Parse(creditsText));

        public string Author { get; }
        public IReadOnlyList<CreditsEntry> Entries { get; }

        // Every line, top to bottom.
        public IReadOnlyList<CreditsLine> Lines => lines;

        // The roll's height, in room pixels, from the top of its first line to below its last credit's gap.
        public int Height { get; }

        // The steps rolled since the screen opened.
        public long Steps { get; private set; }

        // Where the top of the roll is in the room: at the room's foot when the screen opens, a pixel higher each step,
        // and back at the foot once all of it has gone over the top.
        public int Top => MainMenu.RoomHeight - (int)(Steps * Speed % (Height + MainMenu.RoomHeight));

        // Where a line's top is in the room now.
        public int LineY(CreditsLine line) => Top + line.Top;

        // Starts the roll again from the room's foot.
        public void Restart() => Steps = 0;

        public void Step() => Steps++;
    }
}
