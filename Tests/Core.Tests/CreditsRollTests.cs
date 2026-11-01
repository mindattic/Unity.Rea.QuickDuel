using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The credits screen (UQD-T47): credits.txt read into its arena photo credits, laid out under the author's credit,
    // rolling up the room, and the main menu's Credits button opening and closing it.
    public class CreditsRollTests
    {
        private const string Sample =
            "Dusty Junction\r\nhttp://a.example/1\r\n\r\nThe Fumaroles\r\nhttp://a.example/2\r\n\r\n\r\n\r\nPanoramas\r\nhttp://a.example/3\r\nhttp://a.example/4\r\n";

        [Test]
        public void Credits_text_reads_as_blocks_of_a_name_and_its_sources_parted_by_blank_lines()
        {
            var entries = CreditsText.Parse(Sample);
            Assert.That(entries.Select(e => e.Name), Is.EqualTo(new[] { "Dusty Junction", "The Fumaroles", "Panoramas" }));
            Assert.That(entries[0].Sources, Is.EqualTo(new[] { "http://a.example/1" }));
            Assert.That(entries[2].Sources, Is.EqualTo(new[] { "http://a.example/3", "http://a.example/4" }));
        }

        [Test]
        public void Credits_text_that_names_nothing_or_a_picture_without_a_source_does_not_read()
        {
            Assert.Throws<FormatException>(() => CreditsText.Parse("\n\n"));
            Assert.Throws<ArgumentException>(() => CreditsText.Parse("Dusty Junction\n\nThe Fumaroles\nhttp://a.example/2"));
            Assert.Throws<ArgumentException>(() => new CreditsRoll(" ", Array.Empty<CreditsEntry>()));
        }

        [Test]
        public void The_roll_puts_the_heading_and_the_author_above_every_credit_in_the_files_order()
        {
            var roll = CreditsRoll.FromText("Ryan DeBraal", Sample);
            Assert.That(roll.Lines.Select(l => (l.Kind, l.Text)), Is.EqualTo(new[]
            {
                (CreditsLineKind.Heading, "Credits"),
                (CreditsLineKind.Byline, "A game by"),
                (CreditsLineKind.Author, "Ryan DeBraal"),
                (CreditsLineKind.Section, "Arena photographs"),
                (CreditsLineKind.Name, "Dusty Junction"),
                (CreditsLineKind.Source, "http://a.example/1"),
                (CreditsLineKind.Name, "The Fumaroles"),
                (CreditsLineKind.Source, "http://a.example/2"),
                (CreditsLineKind.Name, "Panoramas"),
                (CreditsLineKind.Source, "http://a.example/3"),
                (CreditsLineKind.Source, "http://a.example/4"),
            }));
            var tops = roll.Lines.Select(l => l.Top).ToList();
            Assert.That(tops, Is.Ordered.Ascending);
            Assert.That(tops.Distinct().Count(), Is.EqualTo(tops.Count), "no two lines on one another");
            Assert.That(roll.Lines[0].Top, Is.Zero);
            Assert.That(roll.Lines[6].Top - roll.Lines[5].Top, Is.EqualTo(CreditsRoll.SourceAdvance + CreditsRoll.EntryGap));
            Assert.That(roll.Height, Is.EqualTo(roll.Lines[^1].Top + CreditsRoll.SourceAdvance + CreditsRoll.EntryGap));

            Assert.That(roll.Lines[0].Style, Is.EqualTo(OriginalType.Banner));
            Assert.That(roll.Lines[2].Style, Is.EqualTo(OriginalType.Name));
            Assert.That(roll.Lines[4].Style, Is.EqualTo(OriginalType.Button));
            Assert.That(roll.Lines[5].Style, Is.EqualTo(OriginalType.Small));
        }

        [Test]
        public void The_roll_rises_from_the_rooms_foot_a_pixel_a_step_and_starts_again_once_it_has_gone_over_the_top()
        {
            var roll = CreditsRoll.FromText("Ryan DeBraal", Sample);
            Assert.That(roll.Top, Is.EqualTo(MainMenu.RoomHeight));
            roll.Step();
            Assert.That(roll.Top, Is.EqualTo(MainMenu.RoomHeight - 1));
            Assert.That(roll.LineY(roll.Lines[2]), Is.EqualTo(MainMenu.RoomHeight - 1 + roll.Lines[2].Top));
            for (var i = 1; i < roll.Height + MainMenu.RoomHeight - 1; i++) roll.Step();
            Assert.That(roll.Top, Is.EqualTo(-roll.Height + 1), "the last of it a pixel from gone");
            roll.Step();
            Assert.That(roll.Top, Is.EqualTo(MainMenu.RoomHeight), "back at the foot");
            roll.Restart();
            Assert.That(roll.Top, Is.EqualTo(MainMenu.RoomHeight));
        }

        [Test]
        public void Credits_opens_the_roll_over_the_menu_and_a_press_a_key_or_escape_closes_it_with_the_click()
        {
            var menu = new MainMenu(0, AiDifficulty.Normal, new Random(1), CreditsRoll.FromText("Ryan DeBraal", Sample));
            var clicks = new List<MenuButtonKind>();
            var flicks = new List<MenuButtonKind>();
            var quits = 0;
            menu.Clicked += b => clicks.Add(b.Kind);
            menu.Flicked += b => flicks.Add(b.Kind);
            menu.Quit += () => quits++;

            menu.PointAt(60, 384);
            Assert.That(menu.Message, Is.EqualTo("Who made what?"));
            menu.Press(60, 384);
            Assert.That(menu.CreditsOpen, Is.True);
            Assert.That(clicks, Is.EqualTo(new[] { MenuButtonKind.Credits }));
            Assert.That(menu.Hovered, Is.Null, "the buttons stand down");
            Assert.That(menu.Message, Is.Empty);

            menu.RunTo(10);
            Assert.That(menu.Credits.Steps, Is.EqualTo(10), "the roll moves with the room's steps");
            Assert.That(menu.ScrollX, Is.EqualTo(-10), "and the blood scrolls on behind it");
            menu.PointAt(600, 320);
            menu.FocusNext();
            Assert.That(menu.Hovered, Is.Null);
            Assert.That(flicks, Is.EqualTo(new[] { MenuButtonKind.Credits }), "nothing flicks under the credits");

            menu.Press(600, 320);
            Assert.That(menu.CreditsOpen, Is.False);
            Assert.That(menu.Chosen, Is.False, "the press that closes the credits presses nothing under them");
            Assert.That(clicks, Is.EqualTo(new[] { MenuButtonKind.Credits, MenuButtonKind.Credits }));
            menu.RunTo(20);
            Assert.That(menu.Credits.Steps, Is.EqualTo(10), "the roll stops while closed");

            menu.FocusPrevious();
            Assert.That(menu.Hovered.Kind, Is.EqualTo(MenuButtonKind.Credits));
            menu.Activate();
            Assert.That(menu.CreditsOpen, Is.True);
            Assert.That(menu.Credits.Steps, Is.Zero, "from the room's foot again");
            menu.Activate();
            Assert.That(menu.CreditsOpen, Is.False, "Enter closes them");

            menu.FocusPrevious();
            menu.Activate();
            menu.Cancel();
            Assert.That(menu.CreditsOpen, Is.False, "Escape closes them");
            Assert.That(menu.QuitRequested, Is.False, "and does not end the game");
            Assert.That(quits, Is.Zero);
            menu.Cancel();
            Assert.That(menu.QuitRequested, Is.True, "Escape on the menu still ends it");
        }

        [Test]
        public void A_menu_made_without_credits_still_credits_its_author()
        {
            var menu = new MainMenu(0, AiDifficulty.Normal, new Random(1));
            Assert.That(menu.Credits.Author, Is.EqualTo(MainMenu.DefaultAuthor));
            Assert.That(menu.Credits.Lines.Select(l => l.Kind), Is.EqualTo(new[] { CreditsLineKind.Heading, CreditsLineKind.Byline, CreditsLineKind.Author }));
        }
    }
}
