using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The original Bushido Coda art, sound and fonts the rebuild ships (UQD-T32), as listed by the generated
    // data file Data/assets.json. The Editor's OriginalAssetGenerator copies each file out of BushidoCoda.gmx
    // and writes this file; everything that draws or plays an original asset finds it here by its GameMaker
    // name ("spr_character_ronin_attack", "sfx_hitground3", "fnt_oldenglish"), so no path is hard-coded in
    // behaviour. Paths are relative to the game's asset folder (Assets/Unity.Rea.QuickDuel), use forward slashes
    // and may not climb out of it. A bad file throws one SettingsException naming every problem by key and line.
    public sealed class AssetManifest
    {
        public const string File = "assets.json";
        public const string Format = "quickduel.assets";
        public const int CurrentVersion = 1;

        // The groups a sprite can belong to, in the order the manifest lists them.
        public static readonly IReadOnlyList<string> SpriteGroups = new[]
        {
            "fighter", "weapon", "arena", "loading", "title", "cornerart", "blood", "splatter", "flash", "cursor", "cloud", "ambient", "background", "button",
        };

        private readonly Dictionary<string, OriginalSprite> spritesById = new Dictionary<string, OriginalSprite>();
        private readonly Dictionary<string, OriginalSound> soundsById = new Dictionary<string, OriginalSound>();
        private readonly Dictionary<string, OriginalFont> fontsById = new Dictionary<string, OriginalFont>();

        private AssetManifest(List<OriginalSprite> sprites, List<OriginalSound> sounds, List<OriginalFont> fonts,
            List<OriginalTypeface> typefaces, List<OriginalArena> arenas, List<OriginalFighter> fighters, string introText, string author,
            string credits)
        {
            IntroText = introText;
            Author = author;
            Sprites = sprites;
            Sounds = sounds;
            Fonts = fonts;
            Typefaces = typefaces;
            Arenas = arenas;
            Fighters = fighters;
            Credits = credits;
            foreach (var sprite in sprites)
                spritesById[sprite.Id] = sprite;
            foreach (var sound in sounds)
                soundsById[sound.Id] = sound;
            foreach (var font in fonts)
                fontsById[font.Id] = font;
        }

        public IReadOnlyList<OriginalSprite> Sprites { get; }
        public IReadOnlyList<OriginalSound> Sounds { get; }

        // GameMaker's bitmap fonts: an atlas of pre-rendered glyphs plus its glyph table.
        public IReadOnlyList<OriginalFont> Fonts { get; }

        // TrueType files shipped with the original.
        public IReadOnlyList<OriginalTypeface> Typefaces { get; }

        // The arenas load_arenas.gml defines, in its order.
        public IReadOnlyList<OriginalArena> Arenas { get; }

        // The fighters load_characters.gml defines, in its id order (Myrmidon, Ronin, Gypsy, Vagabond, Exile):
        // cosmetic data only, a name and sprites; the original's stats and skills are not carried (BIBLE section 3).
        public IReadOnlyList<OriginalFighter> Fighters { get; }

        // The intro's story (load_text.gml's introText, UQD-T44), "#" line breaks turned into newlines.
        public string IntroText { get; }

        // The game's author, the original project's option_author (Configs/Default.config.gmx), credited on the credits
        // screen (UQD-T47).
        public string Author { get; }

        // The credits text (the arena photo sources, datafiles/credits.txt).
        public string Credits { get; }

        public OriginalSprite Sprite(string id) =>
            spritesById.TryGetValue(id, out var sprite) ? sprite : throw new KeyNotFoundException($"There is no original sprite called '{id}'.");

        public OriginalSound Sound(string id) =>
            soundsById.TryGetValue(id, out var sound) ? sound : throw new KeyNotFoundException($"There is no original sound called '{id}'.");

        public OriginalFont Font(string id) =>
            fontsById.TryGetValue(id, out var font) ? font : throw new KeyNotFoundException($"There is no original font called '{id}'.");

        public bool HasSprite(string id) => spritesById.ContainsKey(id);

        // The sprites of one group, in manifest order.
        public List<OriginalSprite> InGroup(string group)
        {
            var list = new List<OriginalSprite>();
            foreach (var sprite in Sprites)
                if (sprite.Group == group)
                    list.Add(sprite);
            return list;
        }

        // Every file the manifest names (frames, sounds, font atlases and glyph tables, typefaces, credits).
        public List<string> Files()
        {
            var files = new List<string>();
            foreach (var sprite in Sprites)
                files.AddRange(sprite.Frames);
            foreach (var sound in Sounds)
                files.Add(sound.Path);
            foreach (var font in Fonts)
            {
                files.Add(font.Atlas);
                files.Add(font.Glyphs);
            }
            foreach (var typeface in Typefaces)
                files.Add(typeface.Path);
            files.Add(Credits);
            return files;
        }

        public static AssetManifest Load(ISettingsSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var errors = new List<string>();
            AssetManifest manifest = null;
            string text = null;
            try
            {
                text = source.Read(File);
                if (text == null)
                    errors.Add($"{File}: not found");
            }
            catch (Exception e)
            {
                errors.Add($"{File}: could not be read ({e.Message})");
            }

            if (text != null)
                manifest = Parse(text, errors);
            if (errors.Count > 0 || manifest == null)
                throw new SettingsException(errors, source.Describe);
            return manifest;
        }

        private static AssetManifest Parse(string text, List<string> errors)
        {
            JsonValue root;
            try
            {
                root = Json.Parse(text);
            }
            catch (JsonException e)
            {
                errors.Add($"{File}: is not valid JSON ({e.Message})");
                return null;
            }
            if (root.Kind != JsonKind.Object)
            {
                errors.Add($"{File}: must hold one JSON object {{ ... }}");
                return null;
            }

            var reader = new SettingsReader(root, File, errors);
            var format = reader.Text("format");
            reader.Check(format == Format, "format", $"must be \"{Format}\" (is \"{format}\")");
            var version = reader.Integer("version", 1);
            reader.Check(version == CurrentVersion, "version", $"must be {CurrentVersion} (is {version})");

            var sprites = new List<OriginalSprite>();
            var spriteReader = reader.Section("sprites");
            foreach (var entry in spriteReader.Sections())
            {
                var r = entry.Value;
                var group = r.Text("group");
                r.Check(Contains(SpriteGroups, group), "group", $"must be one of {string.Join(", ", SpriteGroups)} (is \"{group}\")");
                var width = r.Integer("width", 1, 8192);
                var height = r.Integer("height", 1, 8192);
                var originX = r.Integer("originX", -8192, 8192);
                var originY = r.Integer("originY", -8192, 8192);
                var frames = r.Texts("frames");
                foreach (var frame in frames)
                    CheckPath(r, "frames", frame, ".png");
                r.Done();
                sprites.Add(new OriginalSprite(entry.Key, group, width, height, originX, originY, frames));
            }
            spriteReader.Check(sprites.Count > 0, "sprites", "must hold at least one sprite");
            spriteReader.Done();

            var sounds = new List<OriginalSound>();
            var soundReader = reader.Section("sounds");
            foreach (var entry in soundReader.Sections())
            {
                var path = entry.Value.Text("path");
                CheckPath(entry.Value, "path", path, ".ogg");
                entry.Value.Done();
                sounds.Add(new OriginalSound(entry.Key, path));
            }
            soundReader.Done();

            var fonts = new List<OriginalFont>();
            var fontReader = reader.Section("fonts");
            foreach (var entry in fontReader.Sections())
            {
                var r = entry.Value;
                var face = r.Text("face");
                var size = r.Integer("size", 1, 512);
                var atlas = r.Text("atlas");
                CheckPath(r, "atlas", atlas, ".png");
                var glyphs = r.Text("glyphs");
                CheckPath(r, "glyphs", glyphs, ".xml");
                r.Done();
                fonts.Add(new OriginalFont(entry.Key, face, size, atlas, glyphs));
            }
            fontReader.Done();

            var typefaces = new List<OriginalTypeface>();
            var typefaceReader = reader.Section("typefaces");
            foreach (var entry in typefaceReader.Sections())
            {
                var path = entry.Value.Text("path");
                CheckPath(entry.Value, "path", path, ".ttf");
                entry.Value.Done();
                typefaces.Add(new OriginalTypeface(entry.Key, path));
            }
            typefaceReader.Done();

            var spriteIds = new HashSet<string>();
            foreach (var sprite in sprites)
                spriteIds.Add(sprite.Id);

            var arenas = new List<OriginalArena>();
            var arenaReader = reader.Section("arenas");
            foreach (var entry in arenaReader.Sections())
            {
                var r = entry.Value;
                var name = r.Text("name");
                var description = r.Text("description");
                var sprite = r.Text("sprite");
                r.Check(spriteIds.Contains(sprite), "sprite", $"must name a sprite in 'sprites' (is \"{sprite}\")");
                // The ambient effect (UQD-T49): the emitter setup_arena_properties.gml created, or "none"; the
                // original's unused rain, leaf and firefly may be named too. Its sprites must all be in the manifest.
                var ambientName = r.Text("ambient");
                if (!AmbientKinds.TryParse(ambientName, out var ambient))
                    r.Check(false, "ambient", $"must be one of {string.Join(", ", AmbientKinds.Names)} (is \"{ambientName}\")");
                foreach (var needed in AmbientKinds.SpritesOf(ambient))
                    r.Check(spriteIds.Contains(needed), "ambient", $"draws {needed}, which is not in 'sprites'");
                r.Done();
                arenas.Add(new OriginalArena(entry.Key, name, description, sprite, ambient));
            }
            arenaReader.Done();

            var spriteGroups = new Dictionary<string, string>();
            foreach (var sprite in sprites)
                spriteGroups[sprite.Id] = sprite.Group;

            var fighters = new List<OriginalFighter>();
            var fighterReader = reader.Section("fighters");
            foreach (var entry in fighterReader.Sections())
            {
                var r = entry.Value;
                var name = r.Text("name");
                var idle = FighterSprite(r, "idle", "fighter", spriteGroups);
                var run = FighterSprite(r, "run", "fighter", spriteGroups);
                var attack = FighterSprite(r, "attack", "fighter", spriteGroups);
                var defeat = FighterSprite(r, "defeat", "fighter", spriteGroups);
                var weapon = FighterSprite(r, "weapon", "weapon", spriteGroups);
                var groundWeapon = FighterSprite(r, "groundWeapon", "weapon", spriteGroups);
                r.Done();
                fighters.Add(new OriginalFighter(entry.Key, name, idle, run, attack, defeat, weapon, groundWeapon));
            }
            fighterReader.Check(fighters.Count > 0, "fighters", "must hold at least one fighter");
            fighterReader.Done();

            var textReader = reader.Section("text");
            var introText = textReader.Text("intro");
            var author = textReader.Text("author");
            textReader.Done();

            var credits = reader.Text("credits");
            CheckPath(reader, "credits", credits, ".txt");
            reader.Done();

            return errors.Count > 0 ? null : new AssetManifest(sprites, sounds, fonts, typefaces, arenas, fighters, introText, author, credits);
        }

        // A fighter's sprite: the id of a sprite in 'sprites' of the expected group.
        private static string FighterSprite(SettingsReader reader, string key, string group, Dictionary<string, string> spriteGroups)
        {
            var id = reader.Text(key);
            if (id.Length == 0)
                return id; // already reported as empty
            if (!spriteGroups.TryGetValue(id, out var actual))
                reader.Check(false, key, $"must name a sprite in 'sprites' (is \"{id}\")");
            else
                reader.Check(actual == group, key, $"must name a '{group}' sprite (\"{id}\" is '{actual}')");
            return id;
        }

        // A relative, forward-slash path inside the asset folder with the expected extension.
        private static void CheckPath(SettingsReader reader, string key, string path, string extension)
        {
            if (path.Length == 0)
                return; // already reported as empty
            var ok = !path.StartsWith("/", StringComparison.Ordinal)
                && path.IndexOf('\\') < 0
                && path.IndexOf(':') < 0
                && Array.IndexOf(path.Split('/'), "..") < 0
                && Array.IndexOf(path.Split('/'), string.Empty) < 0;
            reader.Check(ok, key, $"must be a relative path with forward slashes inside the asset folder (is \"{path}\")");
            reader.Check(path.EndsWith(extension, StringComparison.OrdinalIgnoreCase), key,
                $"must name a {extension} file (is \"{path}\")");
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (var item in list)
                if (item == value)
                    return true;
            return false;
        }
    }

    // One GameMaker sprite: its frames in order, its size, and its origin (the pixel it is drawn at, measured
    // from the top left, as GameMaker's xorig / yorigin).
    public sealed class OriginalSprite
    {
        public OriginalSprite(string id, string group, int width, int height, int originX, int originY, IReadOnlyList<string> frames)
        {
            Id = id;
            Group = group;
            Width = width;
            Height = height;
            OriginX = originX;
            OriginY = originY;
            Frames = frames;
        }

        public string Id { get; }
        public string Group { get; }
        public int Width { get; }
        public int Height { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public IReadOnlyList<string> Frames { get; }

        // The origin as a pivot from the bottom left (0..1 inside the image), as a sprite importer takes it.
        public double PivotX => (double)OriginX / Width;
        public double PivotY => 1d - (double)OriginY / Height;
    }

    public sealed class OriginalSound
    {
        public OriginalSound(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public string Id { get; }
        public string Path { get; }
    }

    // A GameMaker bitmap font: the system face and size it was rendered from, its glyph atlas and its glyph
    // table (GameMaker's own .font.gmx XML: each glyph's character, atlas rect, shift and offset).
    public sealed class OriginalFont
    {
        public OriginalFont(string id, string face, int size, string atlas, string glyphs)
        {
            Id = id;
            Face = face;
            Size = size;
            Atlas = atlas;
            Glyphs = glyphs;
        }

        public string Id { get; }
        public string Face { get; }
        public int Size { get; }
        public string Atlas { get; }
        public string Glyphs { get; }
    }

    public sealed class OriginalTypeface
    {
        public OriginalTypeface(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public string Id { get; }
        public string Path { get; }
    }

    // An arena as load_arenas.gml named it: its display name, its description ("#" line breaks turned into
    // newlines) and its backdrop sprite; and the ambient effect setup_arena_properties.gml gave it (UQD-T49).
    public sealed class OriginalArena
    {
        public OriginalArena(string id, string name, string description, string sprite, AmbientKind ambient = AmbientKind.None)
        {
            Id = id;
            Name = name;
            Description = description;
            Sprite = sprite;
            Ambient = ambient;
        }

        public AmbientKind Ambient { get; }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string Sprite { get; }
    }

    // A fighter as load_characters.gml defines it, cosmetic data only (UQD-T34): its character_name, its idle, run,
    // attack and defeat strips (character_asset_*) and its weapon in hand and on the ground (weapon_asset,
    // weapon_asset_ground), each the id of a sprite in the manifest. The original's stats, skills and portrait are
    // not carried (BIBLE section 3).
    public sealed class OriginalFighter
    {
        public OriginalFighter(string id, string name, string idle, string run, string attack, string defeat,
            string weapon, string groundWeapon)
        {
            Id = id;
            Name = name;
            Idle = idle;
            Run = run;
            Attack = attack;
            Defeat = defeat;
            Weapon = weapon;
            GroundWeapon = groundWeapon;
        }

        public string Id { get; }
        public string Name { get; }
        public string Idle { get; }
        public string Run { get; }
        public string Attack { get; }
        public string Defeat { get; }
        public string Weapon { get; }
        public string GroundWeapon { get; }
    }
}
