using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.EditorTools
{
    /// <summary>
    /// Which of the original's resources the rebuild uses (UQD-T32), by their GameMaker names, and where each
    /// lands under <see cref="OriginalAssetGenerator.GameFolder"/>. Only what a parity ticket draws or plays is
    /// listed (docs/RECONSTRUCTION.md section 2.4): the stats, skills, overworld, portrait and menu-button art
    /// is dropped with the features it belonged to
    /// (the main menu's buttons are listed, UQD-T45). Arenas are not listed here; they are read from
    /// <c>load_arenas.gml</c> so the list matches the original exactly.
    /// </summary>
    public static class OriginalAssetCatalog
    {
        public static readonly string[] Fighters = { "exile", "gypsy", "myrmidon", "ronin", "vagabond" };
        public static readonly string[] FighterStates = { "idle", "run", "attack", "defeat" };

        /// <summary>One sprite to copy: its GameMaker name, its manifest group and the folder its frames go to.</summary>
        public readonly struct SpriteEntry
        {
            public SpriteEntry(string id, string group, string folder)
            {
                Id = id;
                Group = group;
                Folder = folder;
            }

            public string Id { get; }
            public string Group { get; }
            public string Folder { get; }
        }

        /// <summary>Every sprite except the arenas, in manifest order.</summary>
        public static List<SpriteEntry> Sprites()
        {
            var list = new List<SpriteEntry>();
            foreach (var fighter in Fighters)
                foreach (var state in FighterStates)
                    list.Add(new SpriteEntry($"spr_character_{fighter}_{state}", "fighter", "Art/Fighters"));

            // obj_weapon: the thrown weapon (weapon_asset) and where it lands (weapon_asset_ground); the
            // Myrmidon also drops a shield (spawn_weapons.gml).
            foreach (var fighter in Fighters)
            {
                list.Add(new SpriteEntry($"spr_weapon_{fighter}", "weapon", "Art/Weapons"));
                list.Add(new SpriteEntry($"spr_weapon_{fighter}_ground", "weapon", "Art/Weapons"));
            }
            list.Add(new SpriteEntry("spr_weapon_myrmidon_shield", "weapon", "Art/Weapons"));
            list.Add(new SpriteEntry("spr_weapon_myrmidon_shield_ground", "weapon", "Art/Weapons"));

            // obj_fade_*: one of the 14 loading images per transition.
            for (var i = 1; i <= 14; i++)
                list.Add(new SpriteEntry($"spr_loading{i}", "loading", "Art/Loading"));

            list.Add(new SpriteEntry("spr_titlescreen", "title", "Art/Frame"));
            // obj_cornerart_left / right draw the "2" variants; the plain ones are their earlier cut.
            foreach (var corner in new[] { "spr_cornerart_left", "spr_cornerart_left2", "spr_cornerart_right", "spr_cornerart_right2" })
                list.Add(new SpriteEntry(corner, "cornerart", "Art/Frame"));
            list.Add(new SpriteEntry("spr_katana_cursor", "cursor", "Art/Frame"));

            // obj_blood_emitter, obj_splatter, obj_flash.
            list.Add(new SpriteEntry("spr_blood_drop1", "blood", "Art/Aftermath"));
            list.Add(new SpriteEntry("spr_blood_drop2", "blood", "Art/Aftermath"));
            for (var i = 1; i <= 10; i++)
                list.Add(new SpriteEntry($"spr_splatter{i}", "splatter", "Art/Aftermath"));
            list.Add(new SpriteEntry("spr_flash", "flash", "Art/Aftermath"));

            // The intro (UQD-T44): obj_cloud1 to 4's clouds.
            for (var i = 1; i <= 4; i++)
                list.Add(new SpriteEntry($"spr_cloud{i}", "cloud", "Art/Intro"));
            list.AddRange(Ambient());
            return list;
        }

        /// <summary>
        /// The arenas' ambient particles (UQD-T49): obj_sand, obj_butterfly, obj_snow, obj_fly, and the unused
        /// obj_raindrop, obj_leaf_emitter and obj_firefly's sprites. Fog draws spr_cloud1, already listed for the intro.
        /// </summary>
        public static List<SpriteEntry> Ambient()
        {
            var list = new List<SpriteEntry>();
            foreach (var id in new[]
                     {
                         "spr_sand1", "spr_sand2", "spr_butterfly", "spr_snow1", "spr_snow2", "spr_fly", "spr_fly2",
                         "spr_rain", "spr_leaf1", "spr_leaf2", "spr_firefly", "spr_firefly_dark",
                     })
                list.Add(new SpriteEntry(id, "ambient", "Art/Ambient"));
            return list;
        }

        /// <summary>
        /// GameMaker backgrounds (a <c>.background.gmx</c> and one image) the rebuild draws, listed with the sprites in
        /// the "background" group: the intro room's bg_mapscreen (UQD-T44).
        /// </summary>
        public static readonly SpriteEntry[] Backgrounds =
        {
            new SpriteEntry("bg_mapscreen", "background", "Art/Intro"),
            // The main menu room's background, scrolling behind the title's cut-out logo (UQD-T45).
            new SpriteEntry("bg_splatter_scroll", "background", "Art/Menu"),
            // The victory and gameover rooms' backgrounds, the match verdict (UQD-T48).
            new SpriteEntry("bg_victory", "background", "Art/Verdict"),
            new SpriteEntry("bg_gameover", "background", "Art/Verdict"),
        };

        /// <summary>
        /// The main menu's buttons (UQD-T45), each with the "_over" twin drawn under the mouse, listed after the backgrounds
        /// in the "button" group: New Game (right1), Versus (right2), the difficulty in Options' slot (right3), Quit
        /// (left4) and Credits (left1, UQD-T47); Scores' left3 was a stub and is dropped.
        /// </summary>
        public static List<SpriteEntry> Buttons()
        {
            var list = new List<SpriteEntry>();
            foreach (var id in new[] { "spr_btn_right1", "spr_btn_right2", "spr_btn_right3", "spr_btn_left4", "spr_btn_left1" })
            {
                list.Add(new SpriteEntry(id, "button", "Art/Menu"));
                list.Add(new SpriteEntry(id + "_over", "button", "Art/Menu"));
            }
            // obj_btn_retry and obj_btn_nextbattle's spr_btn_right4, on the verdict screen (UQD-T48).
            list.Add(new SpriteEntry("spr_btn_right4", "button", "Art/Verdict"));
            list.Add(new SpriteEntry("spr_btn_right4_over", "button", "Art/Verdict"));
            return list;
        }

        public const string ArenaFolder = "Art/Arenas";
        public const string AudioFolder = "Audio";
        public const string FontFolder = "Fonts";

        /// <summary>
        /// The duel sounds (already played, UQD-T5, T24), the four weapon hit-ground sounds obj_weapon chooses
        /// from, and the menu buttons' hover flick and click.
        /// </summary>
        public static readonly string[] Sounds =
        {
            "sfx_beep", "sfx_drumroll", "sfx_unsheath", "sfx_slash", "sfx_victory", "sfx_defeat",
            "sfx_hitground1", "sfx_hitground2", "sfx_hitground3", "sfx_hitground4",
            "sfx_flick", "sfx_click",
        };

        /// <summary>
        /// Sounds whose original <c>.ogg</c> Unity's FMOD importer cannot decode ("FSBTool ERROR: Internal error from
        /// FMOD sub-system"). The committed copy is a re-encode of the original's own <c>.wav</c> (UQD-T24), so the
        /// generator keeps it rather than copying the broken file over it.
        /// </summary>
        public static readonly string[] ReencodedSounds = { "sfx_defeat" };

        /// <summary>Every bitmap font the original draws text with.</summary>
        public static readonly string[] Fonts = { "fnt_basic", "fnt_mono", "fnt_oldenglish", "fnt_oldenglish_small", "fnt_tiny" };

        /// <summary>
        /// The TrueType Old English face shipped beside the project (<c>gmlBushidoCoda/fonts/oldenglish.ttf</c>,
        /// Elfring "OldEnglish"). The headline fonts (fnt_oldenglish, fnt_oldenglish_small) were rendered from the
        /// look-alike system face Old English Text MT, which cannot ship; this one lets outlined text be drawn at
        /// any size. The other faces in that folder came from the Android builds and nothing here uses them.
        /// </summary>
        public static readonly string[] Typefaces = { "oldenglish" };
    }
}
