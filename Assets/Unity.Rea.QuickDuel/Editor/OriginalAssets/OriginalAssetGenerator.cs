using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Unity.Rea.QuickDuel.Core;
using UnityEditor;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTools
{
    /// <summary>
    /// Copies the original Bushido Coda art, sound and fonts the rebuild uses (UQD-T32) out of the GameMaker
    /// project into <see cref="GameFolder"/>, and writes the asset manifest (<see cref="ManifestPath"/>) and the
    /// credits text next to it. Each sprite's size, origin and frame list are read from its <c>.sprite.gmx</c>,
    /// the arenas from <c>load_arenas.gml</c>, so nothing is typed in by hand. Import settings are not clicked
    /// either: <see cref="OriginalAssetImportRules"/> applies them from the manifest on every import.
    /// The copied files are committed, so the game and CI never need the original; rerun this only to refresh
    /// them. Run from the menu or in batch mode:
    /// <c>-executeMethod Unity.Rea.QuickDuel.EditorTools.OriginalAssetGenerator.Generate [-originalRoot &lt;BushidoCoda.gmx&gt;]</c>
    /// (or set QUICKDUEL_ORIGINAL_ROOT). The default is the owner's archive path.
    /// </summary>
    public static class OriginalAssetGenerator
    {
        public const string GameFolder = "Assets/Unity.Rea.QuickDuel";
        public const string DataFolder = GameFolder + "/Data";
        public const string ManifestPath = DataFolder + "/" + AssetManifest.File;
        public const string CreditsFile = "Data/credits.txt";
        public const string DefaultRoot = @"R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\GameMaker\gmlBushidoCoda\BushidoCoda.gmx";
        public const string RootArgument = "-originalRoot";
        public const string RootVariable = "QUICKDUEL_ORIGINAL_ROOT";

        [MenuItem("QuickDuel/Import Original Assets")]
        public static void Generate()
        {
            var ok = false;
            try
            {
                var manifest = Run(ResolveRoot());
                Debug.Log($"QuickDuel original assets: {manifest.Sprites.Count} sprites, {manifest.Sounds.Count} sounds, " +
                          $"{manifest.Fonts.Count} fonts, {manifest.Typefaces.Count} typefaces, {manifest.Arenas.Count} arenas, {manifest.Fighters.Count} fighters -> {ManifestPath}");
                ok = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"QuickDuel original assets failed: {e.Message}\n{e.StackTrace}");
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>The GameMaker project folder: <c>-originalRoot</c>, else QUICKDUEL_ORIGINAL_ROOT, else <see cref="DefaultRoot"/>.</summary>
        public static string ResolveRoot()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == RootArgument)
                    return args[i + 1];
            var variable = Environment.GetEnvironmentVariable(RootVariable);
            return string.IsNullOrEmpty(variable) ? DefaultRoot : variable;
        }

        /// <summary>Copies everything, writes the manifest, imports it all and returns the validated manifest.</summary>
        public static AssetManifest Run(string root)
        {
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"The original GameMaker project was not found at {root} (pass {RootArgument} or set {RootVariable}).");

            var json = new ManifestWriter();
            var sprites = new List<SpriteRecord>();
            foreach (var entry in OriginalAssetCatalog.Sprites())
                sprites.Add(CopySprite(root, entry.Id, entry.Group, entry.Folder));
            foreach (var entry in OriginalAssetCatalog.Backgrounds)
                sprites.Add(CopyBackground(root, entry.Id, entry.Group, entry.Folder));
            foreach (var entry in OriginalAssetCatalog.Buttons())
                sprites.Add(CopySprite(root, entry.Id, entry.Group, entry.Folder));

            var arenas = ReadArenas(Path.Combine(root, "scripts", "load_arenas.gml"));
            ReadAmbients(Path.Combine(root, "scripts", "setup_arena_properties.gml"), arenas);
            var arenaSprites = new List<SpriteRecord>();
            foreach (var arena in arenas)
                if (arenaSprites.FindIndex(s => s.Id == arena.Sprite) < 0)
                    arenaSprites.Add(CopySprite(root, arena.Sprite, "arena", OriginalAssetCatalog.ArenaFolder));
            // Keep the manifest in the group order AssetManifest.SpriteGroups lists.
            sprites.InsertRange(sprites.FindIndex(s => s.Group == "loading"), arenaSprites);

            var fighters = ReadFighters(Path.Combine(root, "scripts", "load_characters.gml"));
            var introText = ReadIntroText(Path.Combine(root, "scripts", "load_text.gml"));
            var author = ReadAuthor(Path.Combine(root, "Configs", "Default.config.gmx"));

            var sounds = new List<KeyValuePair<string, string>>();
            foreach (var sound in OriginalAssetCatalog.Sounds)
            {
                var path = $"{OriginalAssetCatalog.AudioFolder}/{sound}.ogg";
                if (Array.IndexOf(OriginalAssetCatalog.ReencodedSounds, sound) >= 0)
                {
                    if (!File.Exists(Full($"{GameFolder}/{path}")))
                        throw new FileNotFoundException($"{sound}'s original .ogg does not import in Unity; re-encode its .wav to {path} first.");
                }
                else
                {
                    Copy(Path.Combine(root, "sound", "audio", sound + ".ogg"), path);
                }
                sounds.Add(new KeyValuePair<string, string>(sound, path));
            }

            var fonts = new List<FontRecord>();
            foreach (var font in OriginalAssetCatalog.Fonts)
                fonts.Add(CopyFont(root, font));

            var typefaces = new List<KeyValuePair<string, string>>();
            foreach (var typeface in OriginalAssetCatalog.Typefaces)
            {
                var path = $"{OriginalAssetCatalog.FontFolder}/{typeface}.ttf";
                Copy(Path.Combine(root, "..", "fonts", typeface + ".ttf"), path);
                typefaces.Add(new KeyValuePair<string, string>(typeface, path));
            }

            Copy(Path.Combine(root, "datafiles", "credits.txt"), CreditsFile);

            var text = json.Write(sprites, sounds, fonts, typefaces, arenas, fighters, introText, author, CreditsFile);
            Directory.CreateDirectory(Full(DataFolder));
            File.WriteAllText(Full(ManifestPath), text, new UTF8Encoding(false));

            // The manifest is on disk before anything imports, so the import rules can read it.
            AssetDatabase.Refresh();
            var manifest = AssetManifest.Load(new DirectorySettingsSource(Full(DataFolder)));
            OriginalAssetImportRules.Forget();
            foreach (var sprite in manifest.Sprites)
                foreach (var frame in sprite.Frames)
                    AssetDatabase.ImportAsset($"{GameFolder}/{frame}", ImportAssetOptions.ForceUpdate);
            foreach (var font in manifest.Fonts)
                AssetDatabase.ImportAsset($"{GameFolder}/{font.Atlas}", ImportAssetOptions.ForceUpdate);

            var missing = new List<string>();
            foreach (var file in manifest.Files())
                if (!File.Exists(Full($"{GameFolder}/{file}")))
                    missing.Add(file);
            if (missing.Count > 0)
                throw new InvalidOperationException("The manifest names files that were not copied: " + string.Join(", ", missing));

            // The fonts' atlases or glyph tables may have changed: rebuild their UI Toolkit font assets (UQD-T36).
            OriginalFontBuilder.Build();
            return manifest;
        }

        private static SpriteRecord CopySprite(string root, string id, string group, string folder)
        {
            var definition = Path.Combine(root, "sprites", id + ".sprite.gmx");
            if (!File.Exists(definition))
                throw new FileNotFoundException($"The original has no sprite {id}", definition);

            var xml = new XmlDocument();
            xml.Load(definition);
            var sprite = xml.SelectSingleNode("/sprite");
            var record = new SpriteRecord
            {
                Id = id,
                Group = group,
                Width = Int(sprite, "width"),
                Height = Int(sprite, "height"),
                OriginX = Int(sprite, "xorig"),
                OriginY = Int(sprite, "yorigin"),
            };

            var frames = sprite.SelectNodes("frames/frame");
            var ordered = new SortedDictionary<int, string>();
            foreach (XmlNode frame in frames)
                ordered[int.Parse(frame.Attributes["index"].Value, CultureInfo.InvariantCulture)] = frame.InnerText.Trim();
            foreach (var frame in ordered.Values)
            {
                // "images\spr_x_0.png", relative to the sprites folder.
                var name = Path.GetFileName(frame.Replace('\\', '/'));
                var path = $"{folder}/{name}";
                Copy(Path.Combine(root, "sprites", frame.Replace('\\', Path.DirectorySeparatorChar)), path);
                record.Frames.Add(path);
            }
            if (record.Frames.Count == 0)
                throw new InvalidDataException($"The original sprite {id} has no frames.");
            return record;
        }

        // A GameMaker background: one image, its size from the .background.gmx, drawn from its top left.
        private static SpriteRecord CopyBackground(string root, string id, string group, string folder)
        {
            var definition = Path.Combine(root, "background", id + ".background.gmx");
            if (!File.Exists(definition))
                throw new FileNotFoundException($"The original has no background {id}", definition);

            var xml = new XmlDocument();
            xml.Load(definition);
            var background = xml.SelectSingleNode("/background");
            var record = new SpriteRecord
            {
                Id = id,
                Group = group,
                Width = Int(background, "width"),
                Height = Int(background, "height"),
            };
            // "images\bg_x.png", relative to the background folder.
            var image = background.SelectSingleNode("data").InnerText.Trim();
            var path = $"{folder}/{Path.GetFileName(image.Replace('\\', '/'))}";
            Copy(Path.Combine(root, "background", image.Replace('\\', Path.DirectorySeparatorChar)), path);
            record.Frames.Add(path);
            return record;
        }

        private static readonly Regex IntroLine = new Regex(@"^\s*txt\s*(\+?=)\s*(""[^""]*"")\s*;?\s*$", RegexOptions.Compiled);

        /// <summary>
        /// load_text.gml's introText (UQD-T44): the <c>txt</c> lines from the top of the script down to
        /// <c>introText = txt;</c>, joined, with GameMaker's "#" line break turned into a newline.
        /// </summary>
        public static string ReadIntroText(string script)
        {
            var text = new StringBuilder();
            foreach (var line in File.ReadAllLines(script))
            {
                if (Regex.IsMatch(line, @"^\s*introText\s*=\s*txt\s*;"))
                {
                    if (text.Length == 0)
                        throw new InvalidDataException("load_text.gml sets introText from an empty txt.");
                    return text.ToString().Replace('#', '\n');
                }
                var match = IntroLine.Match(line);
                if (!match.Success)
                    continue;
                if (match.Groups[1].Value == "=")
                    text.Clear();
                text.Append(Literal(match.Groups[2].Value, "load_text.gml"));
            }
            throw new InvalidDataException("load_text.gml never sets introText.");
        }

        /// <summary>
        /// The game's author (UQD-T47): the project's <c>option_author</c> in Configs/Default.config.gmx, credited on the
        /// credits screen.
        /// </summary>
        public static string ReadAuthor(string config)
        {
            var xml = new XmlDocument();
            xml.Load(config);
            var author = xml.SelectSingleNode("//option_author")?.InnerText.Trim();
            if (string.IsNullOrEmpty(author))
                throw new InvalidDataException($"{config} names no option_author.");
            return author;
        }

        private static FontRecord CopyFont(string root, string id)
        {
            var definition = Path.Combine(root, "fonts", id + ".font.gmx");
            var xml = new XmlDocument();
            xml.Load(definition);
            var font = xml.SelectSingleNode("/font");
            var record = new FontRecord
            {
                Id = id,
                Face = font.SelectSingleNode("name").InnerText.Trim(),
                Size = Int(font, "size"),
                Atlas = $"{OriginalAssetCatalog.FontFolder}/{id}.png",
                Glyphs = $"{OriginalAssetCatalog.FontFolder}/{id}.glyphs.xml",
            };
            Copy(Path.Combine(root, "fonts", id + ".png"), record.Atlas);
            Copy(definition, record.Glyphs);
            return record;
        }

        private static readonly Regex ArenaLine = new Regex(
            @"^\s*arena_(name|asset|description)\[id_(\w+)\]\s*(\+?=)\s*(.+?)\s*;\s*$", RegexOptions.Compiled);

        /// <summary>
        /// The arenas load_arenas.gml defines, in its order. Each is keyed by its GML id name, not its number:
        /// the original gives Sozza's Point and South Ridge the same number (5), which is T33's to resolve.
        /// "#" (GameMaker's line break) becomes a newline.
        /// </summary>
        public static List<ArenaRecord> ReadArenas(string script)
        {
            var arenas = new List<ArenaRecord>();
            foreach (var line in File.ReadAllLines(script))
            {
                var match = ArenaLine.Match(line);
                if (!match.Success)
                    continue;
                var id = match.Groups[2].Value;
                var arena = arenas.Find(a => a.Id == id);
                if (arena == null)
                {
                    arena = new ArenaRecord { Id = id };
                    arenas.Add(arena);
                }

                var value = match.Groups[4].Value;
                var append = match.Groups[3].Value == "+=";
                switch (match.Groups[1].Value)
                {
                    case "name":
                        arena.Name = Literal(value);
                        break;
                    case "asset":
                        arena.Sprite = value;
                        break;
                    default:
                        arena.Description = (append ? arena.Description : string.Empty) + Literal(value);
                        break;
                }
            }

            foreach (var arena in arenas)
                arena.Description = arena.Description?.Replace('#', '\n').TrimEnd('\n');
            return arenas;
        }

        private static readonly Regex AmbientCase = new Regex(@"^\s*case\s+id_(\w+)\s*:", RegexOptions.Compiled);
        private static readonly Regex AmbientEmitter = new Regex(@"instance_create\s*\([^,]*,[^,]*,\s*(obj_\w+_emitter)\s*\)", RegexOptions.Compiled);

        /// <summary>
        /// The ambient effect each arena plays (UQD-T49): the emitter setup_arena_properties.gml creates under the arena's
        /// <c>case id_x:</c>, or none. An arena the switch does not create an emitter for stays "none".
        /// </summary>
        public static void ReadAmbients(string script, List<ArenaRecord> arenas)
        {
            foreach (var arena in arenas)
                arena.Ambient = AmbientKind.None;
            ArenaRecord current = null;
            foreach (var line in File.ReadAllLines(script))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;
                var match = AmbientCase.Match(line);
                if (match.Success)
                {
                    var id = match.Groups[1].Value;
                    current = arenas.Find(a => a.Id == id)
                        ?? throw new InvalidDataException($"setup_arena_properties.gml names an arena load_arenas.gml does not: id_{id}");
                    continue;
                }
                var emitter = AmbientEmitter.Match(line);
                if (!emitter.Success || current == null)
                    continue;
                var kind = AmbientKinds.FromEmitter(emitter.Groups[1].Value);
                if (kind == AmbientKind.None)
                    throw new InvalidDataException($"setup_arena_properties.gml creates an emitter the rebuild does not know: {emitter.Groups[1].Value}");
                current.Ambient = kind;
            }
        }

        private static readonly Regex FighterId = new Regex(
            @"^\s*globalvar\s+id_(\w+)\s*;\s*id_\w+\s*=\s*(\d+)\s*;", RegexOptions.Compiled);

        private static readonly Regex FighterLine = new Regex(
            @"^\s*(character_name|character_asset_idle|character_asset_run|character_asset_attack|character_asset_defeat|weapon_asset|weapon_asset_ground)\[id_(\w+)\]\s*=\s*(.+?)\s*;\s*$",
            RegexOptions.Compiled);

        /// <summary>
        /// The fighters load_characters.gml defines (UQD-T34), cosmetic data only, in its id order (id_myrmidon = 0 ..
        /// id_exile = 4): each one's character_name and its idle, run, attack, defeat, weapon and ground-weapon sprites.
        /// Only ids that have a character_name are fighters (id_shield is a weapon, not a fighter); the stats, skills
        /// and portraits in the same script are deliberately not read (BIBLE section 3).
        /// </summary>
        public static List<FighterRecord> ReadFighters(string script)
        {
            var order = new Dictionary<string, int>();
            var byId = new Dictionary<string, FighterRecord>();
            foreach (var line in File.ReadAllLines(script))
            {
                var id = FighterId.Match(line);
                if (id.Success)
                {
                    order[id.Groups[1].Value] = int.Parse(id.Groups[2].Value, CultureInfo.InvariantCulture);
                    continue;
                }

                var match = FighterLine.Match(line);
                if (!match.Success)
                    continue;
                var key = match.Groups[2].Value;
                if (!byId.TryGetValue(key, out var fighter))
                    byId[key] = fighter = new FighterRecord { Id = key };
                var value = match.Groups[3].Value;
                switch (match.Groups[1].Value)
                {
                    case "character_name": fighter.Name = Literal(value, "load_characters.gml"); break;
                    case "character_asset_idle": fighter.Idle = value; break;
                    case "character_asset_run": fighter.Run = value; break;
                    case "character_asset_attack": fighter.Attack = value; break;
                    case "character_asset_defeat": fighter.Defeat = value; break;
                    case "weapon_asset": fighter.Weapon = value; break;
                    default: fighter.GroundWeapon = value; break;
                }
            }

            var fighters = new List<FighterRecord>();
            foreach (var fighter in byId.Values)
            {
                if (fighter.Name == null)
                    continue;
                if (!order.ContainsKey(fighter.Id))
                    throw new InvalidDataException($"load_characters.gml names fighter id_{fighter.Id} but never numbers it.");
                fighters.Add(fighter);
            }
            fighters.Sort((a, b) => order[a.Id].CompareTo(order[b.Id]));
            return fighters;
        }

        private static string Literal(string value, string script = "load_arenas.gml")
        {
            if (value.Length < 2 || value[0] != '"' || value[value.Length - 1] != '"')
                throw new InvalidDataException($"Expected a quoted string in {script}, found {value}");
            return value.Substring(1, value.Length - 2);
        }

        private static int Int(XmlNode node, string child) =>
            int.Parse(node.SelectSingleNode(child).InnerText.Trim(), CultureInfo.InvariantCulture);

        private static void Copy(string source, string relative)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException($"The original has no file {source}", source);
            var target = Full($"{GameFolder}/{relative}");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            // Skip identical files so a rerun leaves the working tree untouched.
            if (File.Exists(target) && SameBytes(source, target))
                return;
            File.Copy(source, target, true);
        }

        private static bool SameBytes(string a, string b)
        {
            var left = File.ReadAllBytes(a);
            var right = File.ReadAllBytes(b);
            if (left.Length != right.Length)
                return false;
            for (var i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return false;
            return true;
        }

        private static string Full(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        public sealed class SpriteRecord
        {
            public string Id;
            public string Group;
            public int Width;
            public int Height;
            public int OriginX;
            public int OriginY;
            public readonly List<string> Frames = new List<string>();
        }

        public sealed class FontRecord
        {
            public string Id;
            public string Face;
            public int Size;
            public string Atlas;
            public string Glyphs;
        }

        public sealed class FighterRecord
        {
            public string Id;
            public string Name;
            public string Idle;
            public string Run;
            public string Attack;
            public string Defeat;
            public string Weapon;
            public string GroundWeapon;
        }

        public sealed class ArenaRecord
        {
            public string Id;
            public string Name;
            public string Description;
            public string Sprite;
            public AmbientKind Ambient;
        }

        // Writes assets.json by hand in a fixed layout, so a rerun on the same original is byte-identical.
        private sealed class ManifestWriter
        {
            private readonly StringBuilder sb = new StringBuilder();

            public string Write(List<SpriteRecord> sprites, List<KeyValuePair<string, string>> sounds,
                List<FontRecord> fonts, List<KeyValuePair<string, string>> typefaces, List<ArenaRecord> arenas,
                List<FighterRecord> fighters, string introText, string author, string credits)
            {
                sb.Append("{\n");
                Line(1, $"\"format\": {Str(AssetManifest.Format)},");
                Line(1, $"\"version\": {AssetManifest.CurrentVersion},");
                Line(1, $"\"_source\": {Str("BushidoCoda.gmx (GameMaker Studio, 2013); paths are relative to " + GameFolder)},");
                Line(1, $"\"_generated\": {Str("by Unity.Rea.QuickDuel.EditorTools.OriginalAssetGenerator (QuickDuel > Import Original Assets); do not edit by hand")},");

                Line(1, "\"sprites\": {");
                for (var i = 0; i < sprites.Count; i++)
                {
                    var s = sprites[i];
                    var frames = new List<string>();
                    foreach (var frame in s.Frames)
                        frames.Add(Str(frame));
                    Line(2, $"{Str(s.Id)}: {{ \"group\": {Str(s.Group)}, \"width\": {s.Width}, \"height\": {s.Height}, " +
                            $"\"originX\": {s.OriginX}, \"originY\": {s.OriginY}, \"frames\": [ {string.Join(", ", frames)} ] }}{Comma(i, sprites.Count)}");
                }
                Line(1, "},");

                Line(1, "\"sounds\": {");
                for (var i = 0; i < sounds.Count; i++)
                    Line(2, $"{Str(sounds[i].Key)}: {{ \"path\": {Str(sounds[i].Value)} }}{Comma(i, sounds.Count)}");
                Line(1, "},");

                Line(1, "\"fonts\": {");
                for (var i = 0; i < fonts.Count; i++)
                {
                    var f = fonts[i];
                    Line(2, $"{Str(f.Id)}: {{ \"face\": {Str(f.Face)}, \"size\": {f.Size}, \"atlas\": {Str(f.Atlas)}, \"glyphs\": {Str(f.Glyphs)} }}{Comma(i, fonts.Count)}");
                }
                Line(1, "},");

                Line(1, "\"typefaces\": {");
                for (var i = 0; i < typefaces.Count; i++)
                    Line(2, $"{Str(typefaces[i].Key)}: {{ \"path\": {Str(typefaces[i].Value)} }}{Comma(i, typefaces.Count)}");
                Line(1, "},");

                Line(1, "\"arenas\": {");
                for (var i = 0; i < arenas.Count; i++)
                {
                    var a = arenas[i];
                    Line(2, $"{Str(a.Id)}: {{ \"name\": {Str(a.Name)}, \"description\": {Str(a.Description)}, \"sprite\": {Str(a.Sprite)}, " +
                            $"\"ambient\": {Str(AmbientKinds.NameOf(a.Ambient))} }}{Comma(i, arenas.Count)}");
                }
                Line(1, "},");

                Line(1, "\"fighters\": {");
                for (var i = 0; i < fighters.Count; i++)
                {
                    var f = fighters[i];
                    Line(2, $"{Str(f.Id)}: {{ \"name\": {Str(f.Name)}, \"idle\": {Str(f.Idle)}, \"run\": {Str(f.Run)}, \"attack\": {Str(f.Attack)}, " +
                            $"\"defeat\": {Str(f.Defeat)}, \"weapon\": {Str(f.Weapon)}, \"groundWeapon\": {Str(f.GroundWeapon)} }}{Comma(i, fighters.Count)}");
                }
                Line(1, "},");

                Line(1, "\"text\": {");
                Line(2, $"\"intro\": {Str(introText)},");
                Line(2, $"\"author\": {Str(author)}");
                Line(1, "},");

                Line(1, $"\"credits\": {Str(credits)}");
                sb.Append("}\n");
                return sb.ToString();
            }

            private void Line(int depth, string text) => sb.Append(' ', depth * 2).Append(text).Append('\n');

            private static string Comma(int index, int count) => index < count - 1 ? "," : string.Empty;

            private static string Str(string value)
            {
                var b = new StringBuilder("\"");
                foreach (var c in value ?? string.Empty)
                {
                    switch (c)
                    {
                        case '"': b.Append("\\\""); break;
                        case '\\': b.Append("\\\\"); break;
                        case '\n': b.Append("\\n"); break;
                        case '\r': break;
                        case '\t': b.Append("\\t"); break;
                        default:
                            if (c < ' ')
                                b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                b.Append(c);
                            break;
                    }
                }
                return b.Append('"').ToString();
            }
        }
    }
}
