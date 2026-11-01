using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The original asset pipeline (UQD-T32): the generated manifest lists every original sprite, sound and font
    // the parity tickets use, every file it names is committed, each image carries the generated pixel-art import
    // settings, and a bad manifest reports every problem by key and line.
    public class AssetManifestTests
    {
        private static readonly string Repo = FindRepo();
        private static readonly string GameFolder = Path.Combine(Repo, "Assets", "Unity.Rea.QuickDuel");
        private static readonly Lazy<AssetManifest> Shipped = new Lazy<AssetManifest>(() =>
            AssetManifest.Load(new DirectorySettingsSource(Path.Combine(GameFolder, "Data"))));

        private static string FindRepo()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Core.slnx")))
                    return dir.FullName;
            throw new DirectoryNotFoundException("Core.slnx not found above the test output.");
        }

        [Test]
        public void The_shipped_manifest_lists_every_used_fighter_strip()
        {
            var m = Shipped.Value;
            foreach (var fighter in new[] { "exile", "gypsy", "myrmidon", "ronin", "vagabond" })
            {
                Assert.That(m.Sprite($"spr_character_{fighter}_idle").Frames, Has.Count.EqualTo(1));
                Assert.That(m.Sprite($"spr_character_{fighter}_run").Frames, Has.Count.EqualTo(1));
                Assert.That(m.Sprite($"spr_character_{fighter}_attack").Frames, Has.Count.EqualTo(4));
                Assert.That(m.Sprite($"spr_character_{fighter}_defeat").Frames, Has.Count.EqualTo(4));
                Assert.That(m.HasSprite($"spr_weapon_{fighter}"), Is.True);
                Assert.That(m.HasSprite($"spr_weapon_{fighter}_ground"), Is.True);
            }
            Assert.That(m.InGroup("fighter"), Has.Count.EqualTo(20));
            Assert.That(m.InGroup("weapon"), Has.Count.EqualTo(12));
            Assert.That(m.Sprite("spr_character_ronin_attack").OriginX, Is.EqualTo(64));
        }

        [Test]
        public void The_shipped_manifest_lists_the_arenas_transitions_frame_and_aftermath_art()
        {
            var m = Shipped.Value;
            Assert.That(m.Arenas, Has.Count.EqualTo(11));
            Assert.That(m.InGroup("arena"), Has.Count.EqualTo(11));
            Assert.That(m.Arenas[0].Name, Is.EqualTo("Wastes of Nod"));
            Assert.That(m.Arenas.All(a => m.HasSprite(a.Sprite)), Is.True);
            Assert.That(m.InGroup("loading"), Has.Count.EqualTo(14));
            Assert.That(m.InGroup("title"), Has.Count.EqualTo(1));
            Assert.That(m.InGroup("cornerart"), Has.Count.EqualTo(4));
            Assert.That(m.InGroup("cursor").Single().Id, Is.EqualTo("spr_katana_cursor"));
            Assert.That(m.InGroup("blood"), Has.Count.EqualTo(2));
            Assert.That(m.InGroup("splatter"), Has.Count.EqualTo(10));
            Assert.That(m.InGroup("flash").Single().Id, Is.EqualTo("spr_flash"));
        }

        [Test]
        public void The_corner_arts_slide_by_the_size_the_manifest_gives_them()
        {
            var m = Shipped.Value;
            foreach (var id in new[] { "spr_cornerart_left2", "spr_cornerart_right2" })
            {
                Assert.That(m.Sprite(id).Width, Is.EqualTo(CornerArt.SpriteWidth), id);
                Assert.That(m.Sprite(id).Height, Is.EqualTo(CornerArt.SpriteHeight), id);
                Assert.That(m.Sprite(id).OriginX, Is.Zero, id);
                Assert.That(m.Sprite(id).OriginY, Is.Zero, id);
            }
        }

        // The intro (UQD-T44): obj_cloud1 to 4's clouds, the room's bg_mapscreen and load_text.gml's introText.
        [Test]
        public void The_shipped_manifest_carries_the_intro()
        {
            var m = Shipped.Value;
            Assert.That(m.InGroup("cloud").Select(s => s.Id), Is.EqualTo(new[] { "spr_cloud1", "spr_cloud2", "spr_cloud3", "spr_cloud4" }));
            foreach (var cloud in m.InGroup("cloud"))
            {
                Assert.That(cloud.Width, Is.EqualTo(IntroScene.CloudWidth), cloud.Id);
                Assert.That(cloud.Height, Is.EqualTo(IntroScene.CloudHeight), cloud.Id);
                Assert.That(cloud.OriginX, Is.Zero, cloud.Id);
                Assert.That(cloud.OriginY, Is.Zero, cloud.Id);
            }
            var map = m.Sprite("bg_mapscreen");
            Assert.That(map.Group, Is.EqualTo("background"));
            Assert.That((map.Width, map.Height), Is.EqualTo((IntroScene.RoomWidth, IntroScene.RoomHeight)));

            Assert.That(m.IntroText, Does.StartWith("For centuries the empire had been fragmented.\nFactions waged war endlessly against eachother.\n\n"));
            Assert.That(m.IntroText, Does.Contain("A young boy was crowned the Enlightened Ruler.\n\n"));
            Assert.That(m.IntroText, Does.EndWith("The end of a way of life had begun.\n\nThe Bushido Coda."));
            Assert.That(m.IntroText, Does.Not.Contain("#"));
        }

        // The verdict screen (UQD-T48): bg_victory and bg_gameover, each the 640 x 480 room.
        [Test]
        public void The_shipped_manifest_carries_the_verdict_backgrounds()
        {
            var m = Shipped.Value;
            foreach (var id in new[] { VerdictScreen.VictoryBackground, VerdictScreen.GameOverBackground })
            {
                var background = m.Sprite(id);
                Assert.That(background.Group, Is.EqualTo("background"), id);
                Assert.That((background.Width, background.Height), Is.EqualTo((VerdictScreen.RoomWidth, VerdictScreen.RoomHeight)), id);
                Assert.That(background.Frames, Has.Count.EqualTo(1), id);
            }
        }

        // The main menu (UQD-T45): bg_splatter_scroll and the buttons it draws, each with its "_over" twin.
        [Test]
        public void The_shipped_manifest_carries_the_main_menu()
        {
            var m = Shipped.Value;
            var scroll = m.Sprite("bg_splatter_scroll");
            Assert.That(scroll.Group, Is.EqualTo("background"));
            Assert.That((scroll.Width, scroll.Height), Is.EqualTo((MainMenu.ScrollWidth, MainMenu.ScrollHeight)));
            var title = m.Sprite("spr_titlescreen");
            Assert.That((title.Width, title.Height), Is.EqualTo((MainMenu.RoomWidth, MainMenu.RoomHeight)));

            var menu = new MainMenu(0, AiDifficulty.Normal, new System.Random(0));
            var ids = new System.Collections.Generic.List<string>();
            foreach (var button in menu.Buttons)
            {
                ids.Add(button.Sprite);
                ids.Add(button.OverSprite);
            }
            // Then the verdict screen's Retry / Continue (UQD-T48).
            var verdict = new VerdictScreen(VerdictKind.Victory, "", "").Button;
            ids.Add(verdict.Sprite);
            ids.Add(verdict.OverSprite);
            Assert.That(m.InGroup("button").Select(s => s.Id), Is.EqualTo(ids));
            foreach (var sprite in m.InGroup("button"))
            {
                Assert.That((sprite.Width, sprite.Height), Is.EqualTo((MenuButton.Width, MenuButton.Height)), sprite.Id);
                Assert.That((sprite.OriginX, sprite.OriginY), Is.EqualTo((MenuButton.OriginX, MenuButton.OriginY)), sprite.Id);
            }
            Assert.That(m.Sound("sfx_flick").Path, Is.EqualTo("Audio/sfx_flick.ogg"));
            Assert.That(m.Sound("sfx_click").Path, Is.EqualTo("Audio/sfx_click.ogg"));
        }

        [Test]
        public void The_shipped_manifest_lists_the_sounds_fonts_and_credits()
        {
            var m = Shipped.Value;
            foreach (var id in new[] { "sfx_hitground1", "sfx_hitground2", "sfx_hitground3", "sfx_hitground4", "sfx_flick", "sfx_click", "sfx_beep" })
                Assert.That(m.Sound(id).Path, Is.EqualTo($"Audio/{id}.ogg"));
            Assert.That(m.Fonts.Select(f => f.Id), Is.EqualTo(new[] { "fnt_basic", "fnt_mono", "fnt_oldenglish", "fnt_oldenglish_small", "fnt_tiny" }));
            Assert.That(m.Font("fnt_oldenglish").Size, Is.EqualTo(32));
            Assert.That(m.Typefaces.Single().Path, Is.EqualTo("Fonts/oldenglish.ttf"));
            Assert.That(m.Credits, Is.EqualTo("Data/credits.txt"));
            Assert.That(File.ReadAllText(Path.Combine(GameFolder, m.Credits)), Does.Contain("Dusty Junction"));
        }

        // The credits screen (UQD-T47): the project's option_author, and credits.txt reading as its arena credits.
        [Test]
        public void The_shipped_manifest_carries_the_author_and_credits_that_read()
        {
            var m = Shipped.Value;
            Assert.That(m.Author, Is.EqualTo("Ryan DeBraal"));
            var roll = CreditsRoll.FromText(m.Author, File.ReadAllText(Path.Combine(GameFolder, m.Credits)));
            Assert.That(roll.Entries.Select(e => e.Name).Take(7), Is.EqualTo(new[]
                { "Dusty Junction", "The Fumaroles", "Sozza's Point", "Southern Ridge", "Arid Plains", "Interior Sea", "Tu'Verone Forest" }));
            Assert.That(roll.Entries, Has.Count.EqualTo(13));
        }

        [Test]
        public void Every_file_the_manifest_names_is_committed()
        {
            var missing = Shipped.Value.Files().Where(f => !File.Exists(Path.Combine(GameFolder, f))).ToList();
            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void Every_original_image_has_the_generated_pixel_art_import_settings()
        {
            var m = Shipped.Value;
            var images = m.Sprites.SelectMany(s => s.Frames).Concat(m.Fonts.Select(f => f.Atlas)).ToList();
            foreach (var image in images)
            {
                var meta = Path.Combine(GameFolder, image + ".meta");
                Assert.That(File.Exists(meta), Is.True, $"{image} has no .meta");
                var text = File.ReadAllText(meta);
                Assert.That(text, Does.Contain("textureType: 8"), $"{image} is not a sprite");
                Assert.That(text, Does.Contain("spriteMode: 1"), $"{image} is not a single sprite");
                Assert.That(text, Does.Contain("spritePixelsToUnits: 1\n").Or.Contain("spritePixelsToUnits: 1\r\n"), $"{image} is not 1 pixel per unit");
                Assert.That(text, Does.Contain("filterMode: 0"), $"{image} is not point filtered");
                Assert.That(text, Does.Contain("enableMipMap: 0"), $"{image} has mipmaps");
            }

            var ronin = File.ReadAllText(Path.Combine(GameFolder, m.Sprite("spr_character_ronin_attack").Frames[0] + ".meta"));
            Assert.That(ronin, Does.Contain("spritePivot: {x: 0.5, y: 0.5}"));
            var loading = File.ReadAllText(Path.Combine(GameFolder, m.Sprite("spr_loading1").Frames[0] + ".meta"));
            Assert.That(loading, Does.Contain("spritePivot: {x: 0, y: 1}"), "a top-left GameMaker origin pivots at the top left");
        }

        // The arenas' ambient effects (UQD-T49): setup_arena_properties.gml's five emitters, every other arena still air,
        // and every sprite the eight effects draw (the unused rain, leaf and firefly's too) in the manifest.
        [Test]
        public void The_shipped_arenas_carry_the_originals_ambient_emitters()
        {
            var m = Shipped.Value;
            var ambient = m.Arenas.ToDictionary(a => a.Id, a => a.Ambient);
            Assert.That(ambient["desert"], Is.EqualTo(AmbientKind.Sand));
            Assert.That(ambient["pastelfields"], Is.EqualTo(AmbientKind.Butterfly));
            Assert.That(ambient["snow"], Is.EqualTo(AmbientKind.Snow));
            Assert.That(ambient["fumaroles"], Is.EqualTo(AmbientKind.Fog));
            Assert.That(ambient["aridplains"], Is.EqualTo(AmbientKind.Fly));
            Assert.That(ambient.Values.Count(k => k == AmbientKind.None), Is.EqualTo(6));
            Assert.That(ambient.Values.Any(AmbientKinds.Unused), Is.False, "no arena created rain, leaves or fireflies");
            foreach (AmbientKind kind in Enum.GetValues(typeof(AmbientKind)))
                foreach (var id in AmbientKinds.SpritesOf(kind))
                    Assert.That(m.HasSprite(id), Is.True, id);
            Assert.That(m.InGroup("ambient"), Has.Count.EqualTo(12));
            Assert.That(m.Sprite(AmbientField.Butterfly).Frames, Has.Count.EqualTo(AmbientField.ButterflyFrames));
            Assert.That(m.Sprite(AmbientField.Sand1).Width, Is.EqualTo(AmbientField.SandSize));
            Assert.That(m.Sprite(AmbientField.Snow1).Height, Is.EqualTo(AmbientField.SnowSize));
            Assert.That(m.Sprite(AmbientField.Cloud).Width, Is.EqualTo(AmbientField.CloudWidth));
            Assert.That(m.Sprite(AmbientField.Cloud).Height, Is.EqualTo(AmbientField.CloudHeight));
            Assert.That(m.Sprite(AmbientField.Rain).Height, Is.EqualTo(AmbientField.RainHeight));
        }

        [Test]
        public void An_arena_may_name_any_of_the_eight_effects_and_nothing_else()
        {
            const string none = "\"ambient\": \"none\"";
            var rain = Good.Replace(none, "\"ambient\": \"rain\"")
                .Replace("\"sprites\": {", "\"sprites\": {\n    \"spr_rain\": { \"group\": \"ambient\", \"width\": 1, \"height\": 4, " +
                                           "\"originX\": 0, \"originY\": 0, \"frames\": [ \"Art/r_0.png\" ] },");
            Assert.That(Load(rain).Arenas.Single().Ambient, Is.EqualTo(AmbientKind.Rain));

            var errors = Errors(Good.Replace(none, "\"ambient\": \"hail\""));
            Assert.That(errors, Has.Some.Contains("'ambient' must be one of none, sand, butterfly"));
            errors = Errors(Good.Replace(none, "\"ambient\": \"snow\""));
            Assert.That(errors, Has.Some.Contains("draws spr_snow1, which is not in 'sprites'"));
        }

        [Test]
        public void A_GameMaker_origin_becomes_a_bottom_left_pivot()
        {
            var sprite = new OriginalSprite("s", "flash", 90, 60, 30, 15, new[] { "Art/s_0.png" });
            Assert.That(sprite.PivotX, Is.EqualTo(1d / 3d));
            Assert.That(sprite.PivotY, Is.EqualTo(0.75));
        }

        private static readonly string Good = @"{
  ""format"": ""quickduel.assets"",
  ""version"": 1,
  ""sprites"": {
    ""spr_a"": { ""group"": ""arena"", ""width"": 10, ""height"": 10, ""originX"": 0, ""originY"": 0, ""frames"": [ ""Art/a_0.png"" ] },
    ""spr_f"": { ""group"": ""fighter"", ""width"": 10, ""height"": 10, ""originX"": 5, ""originY"": 5, ""frames"": [ ""Art/f_0.png"" ] },
    ""spr_w"": { ""group"": ""weapon"", ""width"": 10, ""height"": 10, ""originX"": 5, ""originY"": 5, ""frames"": [ ""Art/w_0.png"" ] }
  },
  ""sounds"": { ""sfx_a"": { ""path"": ""Audio/sfx_a.ogg"" } },
  ""fonts"": { ""fnt_a"": { ""face"": ""A"", ""size"": 10, ""atlas"": ""Fonts/fnt_a.png"", ""glyphs"": ""Fonts/fnt_a.glyphs.xml"" } },
  ""typefaces"": { ""a"": { ""path"": ""Fonts/a.ttf"" } },
  ""arenas"": { ""a"": { ""name"": ""A"", ""description"": ""An arena."", ""sprite"": ""spr_a"", ""ambient"": ""none"" } },
  ""fighters"": { ""f"": { ""name"": ""F"", ""idle"": ""spr_f"", ""run"": ""spr_f"", ""attack"": ""spr_f"", ""defeat"": ""spr_f"", ""weapon"": ""spr_w"", ""groundWeapon"": ""spr_w"" } },
  ""text"": { ""intro"": ""A story."", ""author"": ""An Author"" },
  ""credits"": ""Data/credits.txt""
}".Replace("\r\n", "\n");

        private static AssetManifest Load(string text) =>
            AssetManifest.Load(new MemorySettingsSource().Set(AssetManifest.File, text));

        private static string[] Errors(string text) =>
            Assert.Throws<SettingsException>(() => Load(text)).Errors.ToArray();

        [Test]
        public void A_minimal_manifest_loads()
        {
            var m = Load(Good);
            Assert.That(m.Sprite("spr_a").Frames, Is.EqualTo(new[] { "Art/a_0.png" }));
            Assert.That(m.Fighters.Single().Weapon, Is.EqualTo("spr_w"));
            Assert.That(m.Files(), Is.EqualTo(new[] { "Art/a_0.png", "Art/f_0.png", "Art/w_0.png", "Audio/sfx_a.ogg", "Fonts/fnt_a.png", "Fonts/fnt_a.glyphs.xml", "Fonts/a.ttf", "Data/credits.txt" }));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => m.Sprite("spr_missing"));
        }

        [Test]
        public void A_bad_manifest_reports_every_problem()
        {
            var bad = Good
                .Replace("\"group\": \"arena\"", "\"group\": \"portrait\"")
                .Replace("Art/a_0.png", "../a_0.png")
                .Replace("Audio/sfx_a.ogg", "C:/sfx_a.wav")
                .Replace("\"sprite\": \"spr_a\"", "\"sprite\": \"spr_b\"")
                .Replace("\"version\": 1", "\"version\": 2, \"extra\": true");
            var errors = Errors(bad);
            Assert.That(errors, Has.Some.Contains("'group' must be one of"));
            Assert.That(errors, Has.Some.Contains("'frames' must be a relative path").And.Contains("../a_0.png"));
            Assert.That(errors, Has.Some.Contains("'path' must be a relative path"));
            Assert.That(errors, Has.Some.Contains("'path' must name a .ogg file"));
            Assert.That(errors, Has.Some.Contains("'sprite' must name a sprite"));
            Assert.That(errors, Has.Some.Contains("'version' must be 1"));
            Assert.That(errors, Has.Some.Contains("unknown key 'extra'"));
        }

        // The fighters are cosmetic data (UQD-T34): a name and sprites of the right group, and nothing else -- a stat
        // like the original's PWR is an unknown key, not a field.
        [Test]
        public void A_bad_fighter_reports_every_problem()
        {
            var bad = Good
                .Replace("\"idle\": \"spr_f\"", "\"idle\": \"spr_missing\"")
                .Replace("\"run\": \"spr_f\"", "\"run\": \"spr_w\"")
                .Replace("\"groundWeapon\": \"spr_w\"", "\"groundWeapon\": \"spr_w\", \"PWR\": 6")
                .Replace("\"name\": \"F\"", "\"name\": \"\"");
            var errors = Errors(bad);
            Assert.That(errors, Has.Some.Contains("'idle' must name a sprite in 'sprites'").And.Contains("spr_missing"));
            Assert.That(errors, Has.Some.Contains("'run' must name a 'fighter' sprite"));
            Assert.That(errors, Has.Some.Contains("unknown key 'PWR'"));
            Assert.That(errors, Has.Some.Contains("'name'"));

            var none = Good.Replace(
                "\"f\": { \"name\": \"F\", \"idle\": \"spr_f\", \"run\": \"spr_f\", \"attack\": \"spr_f\", \"defeat\": \"spr_f\", \"weapon\": \"spr_w\", \"groundWeapon\": \"spr_w\" }",
                string.Empty);
            Assert.That(Errors(none), Has.Some.Contains("must hold at least one fighter"));
        }

        [Test]
        public void The_shipped_manifest_lists_the_five_fighters_as_cosmetic_data()
        {
            var m = Shipped.Value;
            Assert.That(m.Fighters.Select(f => f.Name), Is.EqualTo(new[] { "Myrmidon", "Ronin", "Gypsy", "Vagabond", "Exile" }),
                "load_characters.gml's id order");
            foreach (var fighter in m.Fighters)
            {
                Assert.That(fighter.Idle, Is.EqualTo($"spr_character_{fighter.Id}_idle"));
                Assert.That(fighter.Run, Is.EqualTo($"spr_character_{fighter.Id}_run"));
                Assert.That(fighter.Attack, Is.EqualTo($"spr_character_{fighter.Id}_attack"));
                Assert.That(fighter.Defeat, Is.EqualTo($"spr_character_{fighter.Id}_defeat"));
                Assert.That(fighter.Weapon, Is.EqualTo($"spr_weapon_{fighter.Id}"));
                Assert.That(fighter.GroundWeapon, Is.EqualTo($"spr_weapon_{fighter.Id}_ground"));
                Assert.That(m.Sprite(fighter.Attack).Frames, Has.Count.EqualTo(4));
                Assert.That(m.Sprite(fighter.Defeat).Frames, Has.Count.EqualTo(4));
            }
        }

        [Test]
        public void A_missing_manifest_is_reported()
        {
            var errors = Assert.Throws<SettingsException>(() => AssetManifest.Load(new MemorySettingsSource())).Errors;
            Assert.That(errors, Is.EqualTo(new[] { "assets.json: not found" }));
        }
    }
}
