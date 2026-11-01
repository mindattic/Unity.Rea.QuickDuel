using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The settings registry: every tuning number of the duel, loaded from Settings/tuning.json (StreamingAssets
    // in a build) through an ISettingsSource and validated on load, so no number is hard-coded in behaviour.
    // The file holds named profiles ("standard" is Bushido Coda's own figures) and says which one is the default;
    // a profile is a complete set, so choosing one swaps every figure at once. A bad file throws one
    // SettingsException naming every problem by file, key and line.
    public sealed class TuningRegistry
    {
        public const string Folder = "Settings";
        public const string File = "tuning.json";
        public const string Format = "quickduel.tuning";
        public const int CurrentVersion = 1;

        private readonly List<TuningProfile> profiles;

        private TuningRegistry(List<TuningProfile> profiles, TuningProfile defaultProfile)
        {
            this.profiles = profiles;
            Default = defaultProfile;
        }

        public IReadOnlyList<TuningProfile> Profiles => profiles;

        // The profile the file names as its default.
        public TuningProfile Default { get; }

        // The profile called name; throws if there is none, so a typo in a caller is loud.
        public TuningProfile Get(string name)
        {
            foreach (var profile in profiles)
                if (profile.Name == name)
                    return profile;
            throw new KeyNotFoundException($"There is no tuning profile called '{name}'.");
        }

        public bool Has(string name)
        {
            foreach (var profile in profiles)
                if (profile.Name == name)
                    return true;
            return false;
        }

        public static TuningRegistry Load(ISettingsSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var errors = new List<string>();
            var registry = Read(source, errors);
            if (errors.Count > 0 || registry == null)
                throw new SettingsException(errors, source.Describe);
            return registry;
        }

        private static TuningRegistry Read(ISettingsSource source, List<string> errors)
        {
            string text;
            try
            {
                text = source.Read(File);
            }
            catch (Exception e)
            {
                errors.Add($"{File}: could not be read ({e.Message})");
                return null;
            }
            if (text == null)
            {
                errors.Add($"{File}: not found");
                return null;
            }

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
            var defaultName = reader.Text("default");

            var list = new List<TuningProfile>();
            var profileReader = reader.Section("profiles");
            foreach (var entry in profileReader.Sections())
            {
                var profile = TuningProfile.Read(entry.Key, entry.Value);
                entry.Value.Done();
                list.Add(profile);
            }
            profileReader.Check(list.Count > 0, "profiles", "must hold at least one profile");
            profileReader.Done();
            reader.Done();

            TuningProfile chosen = null;
            foreach (var profile in list)
                if (profile.Name == defaultName)
                    chosen = profile;
            reader.Check(chosen != null || defaultName.Length == 0, "default",
                $"must name one of the profiles ({Names(list)}) (is \"{defaultName}\")");

            return chosen == null ? null : new TuningRegistry(list, chosen);
        }

        private static string Names(List<TuningProfile> list)
        {
            var names = new List<string>();
            foreach (var profile in list)
                names.Add(profile.Name);
            return string.Join(", ", names);
        }
    }

    // One complete, named set of tuning numbers.
    public sealed class TuningProfile
    {
        public string Name { get; private set; }
        public DuelTuning Duel { get; private set; }
        public AiTuning Ai { get; private set; }
        public MatchTuning Match { get; private set; }
        public JudgeTuning Judge { get; private set; }
        public OnlineTuning Online { get; private set; }
        public string[] Taunts { get; private set; }

        internal static TuningProfile Read(string name, SettingsReader r) => new TuningProfile
        {
            Name = name,
            Duel = DuelTuning.Read(r.Section("duel")),
            Ai = AiTuning.Read(r.Section("ai")),
            Match = MatchTuning.Read(r.Section("match")),
            Judge = JudgeTuning.Read(r.Section("judge")),
            Online = OnlineTuning.Read(r.Section("online")),
            Taunts = r.Texts("taunts"),
        };
    }

    // The round's clock: the startup before it, the 3-2-1 countdown, the random drumroll, and how long a strike may take.
    public sealed class DuelTuning
    {
        // obj_gameclock's startup_time: how long the startup runs before the countdown begins by itself (RoundStartup).
        public double StartupSeconds { get; private set; }
        public double CountdownSeconds { get; private set; }
        public double MinStandoffSeconds { get; private set; }
        public double MaxStandoffSeconds { get; private set; }
        public double StrikeTimeoutSeconds { get; private set; }

        // How long to wait after the first strike for a later-delivered input event that happened earlier.
        public double SettleSeconds { get; private set; }

        // A local round's settings (MatchSession, DuelController): the same clock with no strike timeout, as
        // gameclock_action counted until someone struck and never gave up (UQD-T68). The timeout is online's alone,
        // where a peer can vanish.
        public DuelSettings ToLocalSettings()
        {
            var settings = ToSettings();
            settings.StrikeTimeoutSeconds = double.PositiveInfinity;
            return settings;
        }

        // An online round's settings (OnlineDuelHost, OnlineDuelClient): the strike timeout applies.
        public DuelSettings ToSettings() => new DuelSettings
        {
            CountdownSeconds = CountdownSeconds,
            MinStandoffSeconds = MinStandoffSeconds,
            MaxStandoffSeconds = MaxStandoffSeconds,
            StrikeTimeoutSeconds = StrikeTimeoutSeconds,
            SettleSeconds = SettleSeconds,
        };

        internal static DuelTuning Read(SettingsReader r)
        {
            var tuning = new DuelTuning
            {
                StartupSeconds = r.Number("startupSeconds", 0, 60),
                CountdownSeconds = r.Number("countdownSeconds", 0, 60),
                MinStandoffSeconds = r.Number("minStandoffSeconds", 0, 60),
                MaxStandoffSeconds = r.Number("maxStandoffSeconds", 0, 60),
                StrikeTimeoutSeconds = r.Number("strikeTimeoutSeconds", 0.001, 60),
                SettleSeconds = r.Number("settleSeconds", 0, 5),
            };
            r.Check(tuning.MinStandoffSeconds <= tuning.MaxStandoffSeconds, "minStandoffSeconds",
                "must not exceed maxStandoffSeconds");
            r.Done();
            return tuning;
        }
    }

    // The seconds range a computer duelist draws its reaction from.
    public readonly struct ReactionRange
    {
        public ReactionRange(double min, double max)
        {
            Min = min;
            Max = max;
        }

        public double Min { get; }
        public double Max { get; }
    }

    // The computer duelist's reaction range per difficulty.
    public sealed class AiTuning
    {
        public ReactionRange Easy { get; private set; }
        public ReactionRange Normal { get; private set; }
        public ReactionRange Hard { get; private set; }

        public ReactionRange RangeOf(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy: return Easy;
                case AiDifficulty.Normal: return Normal;
                case AiDifficulty.Hard: return Hard;
                default: throw new ArgumentOutOfRangeException(nameof(difficulty));
            }
        }

        internal static AiTuning Read(SettingsReader r)
        {
            var tuning = new AiTuning
            {
                Easy = ReadRange(r, "easy"),
                Normal = ReadRange(r, "normal"),
                Hard = ReadRange(r, "hard"),
            };
            r.Check(tuning.Hard.Min <= tuning.Normal.Min && tuning.Normal.Min <= tuning.Easy.Min
                    && tuning.Hard.Max <= tuning.Normal.Max && tuning.Normal.Max <= tuning.Easy.Max,
                "hard", "ranges must get faster from easy to normal to hard");
            r.Done();
            return tuning;
        }

        private static ReactionRange ReadRange(SettingsReader parent, string key)
        {
            var r = parent.Section(key);
            var min = r.Number("min", 0.01, 10);
            var max = r.Number("max", 0.01, 10);
            r.Check(min <= max, "min", "must not exceed max");
            r.Done();
            return new ReactionRange(min, max);
        }
    }

    // First to winsNeeded rounds takes the match.
    public sealed class MatchTuning
    {
        public int WinsNeeded { get; private set; }

        internal static MatchTuning Read(SettingsReader r)
        {
            var tuning = new MatchTuning { WinsNeeded = r.Integer("winsNeeded", 1, 99) };
            r.Done();
            return tuning;
        }
    }

    // A reported reaction faster than the human floor is rejected as impossible (online play).
    public sealed class JudgeTuning
    {
        public double HumanFloorSeconds { get; private set; }

        internal static JudgeTuning Read(SettingsReader r)
        {
            var tuning = new JudgeTuning { HumanFloorSeconds = r.Number("humanFloorSeconds", 0, 1) };
            r.Done();
            return tuning;
        }
    }

    // Online hosting.
    public sealed class OnlineTuning
    {
        // Slack after the longest a round can take for the clients' reports to arrive.
        public double ReportGraceSeconds { get; private set; }

        internal static OnlineTuning Read(SettingsReader r)
        {
            var tuning = new OnlineTuning { ReportGraceSeconds = r.Number("reportGraceSeconds", 0, 60) };
            r.Done();
            return tuning;
        }
    }
}
