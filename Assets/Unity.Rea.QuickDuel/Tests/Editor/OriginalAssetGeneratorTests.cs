using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using Unity.Rea.QuickDuel.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTests
{
    // The art and data generators (UQD-T51): the script and config readers that turn the original's GameMaker
    // files into the manifest, run on small stand-ins written to a temp folder, and the committed manifest checked
    // against the catalog that says what the rebuild uses.
    public class OriginalAssetGeneratorTests
    {
        private string folder;

        [SetUp]
        public void MakeFolder()
        {
            folder = Path.Combine(Path.GetTempPath(), "quickduel-generator-" + Path.GetRandomFileName());
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void RemoveFolder() => Directory.Delete(folder, true);

        private string Script(string name, string text)
        {
            var path = Path.Combine(folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        private static AssetManifest Shipped() =>
            AssetManifest.Load(new DirectorySettingsSource(Path.GetFullPath(OriginalAssetGenerator.DataFolder)));

        private static string OnDisk(string relative) => Path.Combine(OriginalAssetGenerator.GameFolder, relative);

        [Test]
        public void TheIntroTextIsTheTxtLinesJoinedWithHashTurnedIntoLineBreaks()
        {
            var script = Script("load_text.gml",
                "txt = \"ignored\";\ntxt = \"First line.#\";\ntxt += \"Second line.\";\nintroText = txt;\ntxt = \"later\";\n");
            Assert.That(OriginalAssetGenerator.ReadIntroText(script), Is.EqualTo("First line.\nSecond line."));
        }

        [Test]
        public void AnIntroScriptThatNeverSetsIntroTextIsRefused()
        {
            Assert.Throws<InvalidDataException>(() => OriginalAssetGenerator.ReadIntroText(Script("a.gml", "txt = \"x\";\n")));
            Assert.Throws<InvalidDataException>(() => OriginalAssetGenerator.ReadIntroText(Script("b.gml", "introText = txt;\n")));
        }

        [Test]
        public void TheAuthorIsTheConfigsOptionAuthor()
        {
            var config = Script("Default.config.gmx", "<Config><Options><option_author> Ryan </option_author></Options></Config>");
            Assert.That(OriginalAssetGenerator.ReadAuthor(config), Is.EqualTo("Ryan"));
            Assert.Throws<InvalidDataException>(() =>
                OriginalAssetGenerator.ReadAuthor(Script("empty.gmx", "<Config><option_author/></Config>")));
        }

        [Test]
        public void ArenasAreReadInScriptOrderWithMultiLineDescriptions()
        {
            var script = Script("load_arenas.gml",
                "arena_name[id_dusty] = \"Dusty Junction\";\n" +
                "arena_asset[id_dusty] = spr_arena_dusty;\n" +
                "arena_description[id_dusty] = \"A dry place.#\";\n" +
                "arena_description[id_dusty] += \"Mind the dust.#\";\n" +
                "arena_name[id_tundra] = \"Frozen Tundra\";\n" +
                "arena_asset[id_tundra] = spr_arena_tundra;\n");
            var arenas = OriginalAssetGenerator.ReadArenas(script);
            Assert.That(arenas.Select(a => a.Id), Is.EqualTo(new[] { "dusty", "tundra" }));
            Assert.That(arenas[0].Name, Is.EqualTo("Dusty Junction"));
            Assert.That(arenas[0].Sprite, Is.EqualTo("spr_arena_dusty"));
            Assert.That(arenas[0].Description, Is.EqualTo("A dry place.\nMind the dust."));
            Assert.That(arenas[1].Description, Is.Null);
        }

        [Test]
        public void AnUnquotedArenaNameIsRefused()
        {
            var script = Script("load_arenas.gml", "arena_name[id_x] = Unquoted;\n");
            Assert.Throws<InvalidDataException>(() => OriginalAssetGenerator.ReadArenas(script));
        }

        [Test]
        public void AmbientsFollowTheCaseTheEmitterIsCreatedUnderAndOthersStayNone()
        {
            var arenas = new List<OriginalAssetGenerator.ArenaRecord>
            {
                new OriginalAssetGenerator.ArenaRecord { Id = "dusty" },
                new OriginalAssetGenerator.ArenaRecord { Id = "tundra" },
            };
            var script = Script("setup_arena_properties.gml",
                "switch (arena) {\n  case id_dusty:\n    // instance_create(0,0,obj_snow_emitter);\n    instance_create(0, 0, obj_sand_emitter);\n    break;\n" +
                "  case id_tundra:\n    break;\n}\n");
            OriginalAssetGenerator.ReadAmbients(script, arenas);
            Assert.That(arenas[0].Ambient, Is.EqualTo(AmbientKind.Sand));
            Assert.That(arenas[1].Ambient, Is.EqualTo(AmbientKind.None));
        }

        [Test]
        public void AnAmbientCaseForAnUnknownArenaOrEmitterIsRefused()
        {
            var arenas = new List<OriginalAssetGenerator.ArenaRecord> { new OriginalAssetGenerator.ArenaRecord { Id = "dusty" } };
            Assert.Throws<InvalidDataException>(() =>
                OriginalAssetGenerator.ReadAmbients(Script("a.gml", "case id_nowhere:\n"), arenas));
            Assert.Throws<InvalidDataException>(() =>
                OriginalAssetGenerator.ReadAmbients(Script("b.gml", "case id_dusty:\n instance_create(0,0,obj_meteor_emitter);\n"), arenas));
        }

        [Test]
        public void FightersAreReadInIdOrderAndOnlyThoseWithANameCount()
        {
            var script = Script("load_characters.gml",
                "globalvar id_ronin; id_ronin = 1;\nglobalvar id_myrmidon; id_myrmidon = 0;\nglobalvar id_shield; id_shield = 9;\n" +
                "character_name[id_ronin] = \"Ronin\";\ncharacter_asset_idle[id_ronin] = spr_ronin_idle;\n" +
                "character_asset_run[id_ronin] = spr_ronin_run;\ncharacter_asset_attack[id_ronin] = spr_ronin_attack;\n" +
                "character_asset_defeat[id_ronin] = spr_ronin_defeat;\nweapon_asset[id_ronin] = spr_w;\nweapon_asset_ground[id_ronin] = spr_wg;\n" +
                "character_name[id_myrmidon] = \"Myrmidon\";\nweapon_asset[id_shield] = spr_shield;\n");
            var fighters = OriginalAssetGenerator.ReadFighters(script);
            Assert.That(fighters.Select(f => f.Id), Is.EqualTo(new[] { "myrmidon", "ronin" }));
            Assert.That(fighters[1].Name, Is.EqualTo("Ronin"));
            Assert.That((fighters[1].Idle, fighters[1].Run, fighters[1].Attack, fighters[1].Defeat),
                Is.EqualTo(("spr_ronin_idle", "spr_ronin_run", "spr_ronin_attack", "spr_ronin_defeat")));
            Assert.That((fighters[1].Weapon, fighters[1].GroundWeapon), Is.EqualTo(("spr_w", "spr_wg")));
        }

        [Test]
        public void ANamedFighterThatIsNeverNumberedIsRefused()
        {
            var script = Script("load_characters.gml", "character_name[id_ghost] = \"Ghost\";\n");
            Assert.Throws<InvalidDataException>(() => OriginalAssetGenerator.ReadFighters(script));
        }

        [Test]
        public void RunningOnAMissingOriginalSaysHowToPointAtIt()
        {
            var e = Assert.Throws<DirectoryNotFoundException>(() => OriginalAssetGenerator.Run(Path.Combine(folder, "nope")));
            Assert.That(e.Message, Does.Contain(OriginalAssetGenerator.RootArgument));
        }

        [Test]
        public void EverySpriteTheCatalogListsIsInTheShippedManifestWithItsFramesOnDisk()
        {
            var manifest = Shipped();
            var entries = OriginalAssetCatalog.Sprites().Concat(OriginalAssetCatalog.Backgrounds).Concat(OriginalAssetCatalog.Buttons());
            foreach (var entry in entries)
            {
                Assert.That(manifest.HasSprite(entry.Id), Is.True, $"{entry.Id} is not in the manifest");
                var sprite = manifest.Sprite(entry.Id);
                Assert.That(sprite.Group, Is.EqualTo(entry.Group), entry.Id);
                foreach (var frame in sprite.Frames)
                {
                    Assert.That(frame, Does.StartWith(entry.Folder + "/"), entry.Id);
                    Assert.That(File.Exists(OnDisk(frame)), Is.True, $"{frame} is missing");
                }
            }
        }

        [Test]
        public void EverySoundFontAndTypefaceTheCatalogListsIsInTheManifestOnDisk()
        {
            var manifest = Shipped();
            foreach (var id in OriginalAssetCatalog.Sounds)
                Assert.That(File.Exists(OnDisk(manifest.Sound(id).Path)), Is.True, id);
            foreach (var id in OriginalAssetCatalog.Fonts)
            {
                var font = manifest.Font(id);
                Assert.That(File.Exists(OnDisk(font.Atlas)), Is.True, id);
                Assert.That(File.Exists(OnDisk(font.Glyphs)), Is.True, id);
            }
            Assert.That(manifest.Typefaces.Select(t => t.Id), Is.SupersetOf(OriginalAssetCatalog.Typefaces));
        }

        [Test]
        public void TheShippedManifestHoldsEveryFighterTheCatalogNamesAndAllItsArenasHaveArt()
        {
            var manifest = Shipped();
            Assert.That(manifest.Fighters.Select(f => f.Id), Is.EquivalentTo(OriginalAssetCatalog.Fighters));
            Assert.That(manifest.Arenas, Is.Not.Empty);
            foreach (var arena in manifest.Arenas)
                Assert.That(manifest.HasSprite(arena.Sprite), Is.True, $"arena {arena.Id} has no sprite {arena.Sprite}");
            Assert.That(manifest.IntroText, Does.StartWith("For centuries"));
            Assert.That(manifest.Author, Is.Not.Empty);
            Assert.That(File.Exists(OnDisk(manifest.Credits)), Is.True);
        }

        [Test]
        public void EveryFileTheManifestNamesExistsOnDisk()
        {
            foreach (var file in Shipped().Files())
                Assert.That(File.Exists(OnDisk(file)), Is.True, $"{file} is missing");
        }

        [Test]
        public void TheImportRulesGovernAManifestFrameAndGiveItPixelArtSettings()
        {
            var sprite = Shipped().Sprite("spr_cloud1");
            var path = $"{OriginalAssetGenerator.GameFolder}/{sprite.Frames[0]}";
            OriginalAssetImportRules.Forget();
            Assert.That(OriginalAssetImportRules.Governs(path), Is.True);
            Assert.That(OriginalAssetImportRules.SpriteOf(path).Id, Is.EqualTo("spr_cloud1"));
            Assert.That(OriginalAssetImportRules.Governs("Assets/Unity.Rea.QuickDuel/Art/Nope.png"), Is.False);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(1f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(settings.spritePivot.x, Is.EqualTo((float)sprite.PivotX).Within(1e-4f));
            Assert.That(settings.spritePivot.y, Is.EqualTo((float)sprite.PivotY).Within(1e-4f));
        }
    }
}
