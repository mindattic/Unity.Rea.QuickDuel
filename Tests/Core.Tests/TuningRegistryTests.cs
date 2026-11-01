using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The tuning registry (UQD-T28): the shipped file loads with the original's figures, named profiles are
    // chosen by name, and a bad file reports every problem by file, key and line.
    public class TuningRegistryTests
    {
        // Line numbers below count from the opening brace as line 1.
        private static readonly string Good = @"{
  ""format"": ""quickduel.tuning"",
  ""version"": 1,
  ""default"": ""standard"",
  ""profiles"": {
    ""standard"": {
      ""duel"": { ""startupSeconds"": 3.0, ""countdownSeconds"": 3.0, ""minStandoffSeconds"": 3.0, ""maxStandoffSeconds"": 6.5, ""strikeTimeoutSeconds"": 3.0, ""settleSeconds"": 0.05 },
      ""ai"": { ""easy"": { ""min"": 0.45, ""max"": 0.70 }, ""normal"": { ""min"": 0.30, ""max"": 0.45 }, ""hard"": { ""min"": 0.20, ""max"": 0.30 } },
      ""match"": { ""winsNeeded"": 2 },
      ""judge"": { ""humanFloorSeconds"": 0.1 },
      ""online"": { ""reportGraceSeconds"": 2.0 },
      ""taunts"": [ ""Too Soon"", ""Fail"" ]
    },
    ""blitz"": {
      ""duel"": { ""startupSeconds"": 1.0, ""countdownSeconds"": 1.0, ""minStandoffSeconds"": 1.0, ""maxStandoffSeconds"": 2.0, ""strikeTimeoutSeconds"": 1.0, ""settleSeconds"": 0.05 },
      ""ai"": { ""easy"": { ""min"": 0.45, ""max"": 0.70 }, ""normal"": { ""min"": 0.30, ""max"": 0.45 }, ""hard"": { ""min"": 0.20, ""max"": 0.30 } },
      ""match"": { ""winsNeeded"": 1 },
      ""judge"": { ""humanFloorSeconds"": 0.1 },
      ""online"": { ""reportGraceSeconds"": 2.0 },
      ""taunts"": [ ""Oops"" ]
    }
  }
}".Replace("\r\n", "\n");

        private static MemorySettingsSource Memory(string text) => new MemorySettingsSource().Set(TuningRegistry.File, text);

        private static string[] Errors(string text) =>
            Assert.Throws<SettingsException>(() => TuningRegistry.Load(Memory(text))).Errors.ToArray();

        [Test]
        public void The_shipped_file_loads_with_the_figures_re_derived_from_the_original()
        {
            var p = TestTuning.Profile;
            Assert.That(p.Name, Is.EqualTo("standard"));
            Assert.That(p.Duel.StartupSeconds, Is.EqualTo(3.0));
            Assert.That(p.Duel.CountdownSeconds, Is.EqualTo(4.0));
            Assert.That(p.Duel.MinStandoffSeconds, Is.EqualTo(3.0));
            Assert.That(p.Duel.MaxStandoffSeconds, Is.EqualTo(6.5));
            Assert.That(p.Duel.StrikeTimeoutSeconds, Is.EqualTo(3.0));
            Assert.That(p.Duel.ToSettings().StrikeTimeoutSeconds, Is.EqualTo(3.0), "online rounds give up");
            Assert.That(p.Duel.ToLocalSettings().StrikeTimeoutSeconds, Is.EqualTo(double.PositiveInfinity),
                "a local round waits for the first strike, as gameclock_action did (UQD-T68)");
            Assert.That(p.Duel.ToLocalSettings().CountdownSeconds, Is.EqualTo(p.Duel.CountdownSeconds));
            Assert.That(p.Duel.SettleSeconds, Is.EqualTo(0.05));
            Assert.That((p.Ai.Easy.Min, p.Ai.Easy.Max), Is.EqualTo((0.45, 0.70)));
            Assert.That((p.Ai.Normal.Min, p.Ai.Normal.Max), Is.EqualTo((0.30, 0.45)));
            Assert.That((p.Ai.Hard.Min, p.Ai.Hard.Max), Is.EqualTo((0.20, 0.30)));
            Assert.That(p.Match.WinsNeeded, Is.EqualTo(2));
            Assert.That(p.Judge.HumanFloorSeconds, Is.EqualTo(0.1));
            Assert.That(p.Online.ReportGraceSeconds, Is.EqualTo(2.0));
            Assert.That(p.Taunts, Is.EqualTo(new[]
            {
                "Premature Strike", "Stumbled", "Impetuous Attack", "Clumsy Move", "Hurried Action",
                "Jumped the Gun", "Overzealous", "Too Soon", "Too Early", "Fail"
            }));
        }

        [Test]
        public void Profiles_are_chosen_by_name_and_the_default_is_named_in_the_file()
        {
            var registry = TuningRegistry.Load(Memory(Good));
            Assert.That(registry.Profiles.Select(p => p.Name), Is.EqualTo(new[] { "standard", "blitz" }));
            Assert.That(registry.Default.Name, Is.EqualTo("standard"));
            Assert.That(registry.Get("blitz").Match.WinsNeeded, Is.EqualTo(1));
            Assert.That(registry.Has("nope"), Is.False);
            Assert.Throws<KeyNotFoundException>(() => registry.Get("nope"));
        }

        [Test]
        public void Duel_settings_come_from_the_profile()
        {
            var settings = TuningRegistry.Load(Memory(Good)).Get("blitz").Duel.ToSettings();
            Assert.That(settings.CountdownSeconds, Is.EqualTo(1.0));
            Assert.That(settings.MaxStandoffSeconds, Is.EqualTo(2.0));
            Assert.That(settings.SettleSeconds, Is.EqualTo(0.05));
        }

        [Test]
        public void A_missing_file_is_reported_by_name()
        {
            var e = Assert.Throws<SettingsException>(() => TuningRegistry.Load(new MemorySettingsSource()));
            Assert.That(e.Errors, Is.EqualTo(new[] { "tuning.json: not found" }));
        }

        [Test]
        public void Text_that_is_not_json_names_its_line()
        {
            var errors = Errors("{\n  \"format\": \n}");
            Assert.That(errors, Has.Length.EqualTo(1));
            Assert.That(errors[0], Does.StartWith("tuning.json: is not valid JSON (line 3"));
        }

        [Test]
        public void A_value_out_of_range_names_file_key_and_line()
        {
            var errors = Errors(Good.Replace("\"winsNeeded\": 2", "\"winsNeeded\": 0"));
            Assert.That(errors, Is.EqualTo(new[]
            {
                "tuning.json.profiles.standard.match: 'winsNeeded' must be between 1 and 99 (is 0) (line 9)"
            }));
        }

        [Test]
        public void A_wrong_type_a_missing_key_and_an_unknown_key_are_each_reported()
        {
            var text = Good
                .Replace("\"humanFloorSeconds\": 0.1", "\"humanFloorSeconds\": \"fast\"")
                .Replace("\"reportGraceSeconds\": 2.0", "\"reportGraceSecs\": 2.0");
            var errors = Errors(text);
            Assert.That(errors, Has.Some.EqualTo("tuning.json.profiles.standard.judge: 'humanFloorSeconds' must be a number (is \"fast\") (line 10)"));
            Assert.That(errors, Has.Some.EqualTo("tuning.json.profiles.standard.online: 'reportGraceSeconds' is missing (expected a number)"));
            Assert.That(errors, Has.Some.EqualTo("tuning.json.profiles.standard.online: unknown key 'reportGraceSecs' (line 11); check its spelling"));
        }

        [Test]
        public void Every_problem_is_reported_in_one_load_across_profiles()
        {
            var text = Good
                .Replace("\"minStandoffSeconds\": 3.0", "\"minStandoffSeconds\": 9.0")
                .Replace("\"hard\": { \"min\": 0.20, \"max\": 0.30 }", "\"hard\": { \"min\": 0.50, \"max\": 0.40 }")
                .Replace("\"taunts\": [ \"Oops\" ]", "\"taunts\": []");
            var errors = Errors(text);
            Assert.That(errors, Has.Some.Contains("profiles.standard.duel: 'minStandoffSeconds' must not exceed maxStandoffSeconds"));
            Assert.That(errors, Has.Some.Contains("profiles.standard.ai.hard: 'min' must not exceed max"));
            Assert.That(errors, Has.Some.Contains("profiles.standard.ai: 'hard' ranges must get faster"));
            Assert.That(errors, Has.Some.Contains("profiles.blitz: 'taunts' must hold at least one line"));
        }

        [Test]
        public void The_default_must_name_a_profile_and_there_must_be_one()
        {
            Assert.That(Errors(Good.Replace("\"default\": \"standard\"", "\"default\": \"nope\"")),
                Has.Some.EqualTo("tuning.json: 'default' must name one of the profiles (standard, blitz) (is \"nope\") (line 4)"));
            Assert.That(Errors(Good.Replace("\"format\": \"quickduel.tuning\"", "\"format\": \"other\"")),
                Has.Some.Contains("'format' must be \"quickduel.tuning\""));
            Assert.That(Errors("{ \"format\": \"quickduel.tuning\", \"version\": 1, \"default\": \"a\", \"profiles\": {} }"),
                Has.Some.Contains("'profiles' must hold at least one profile"));
        }

        [Test]
        public void Underscore_keys_are_notes_and_are_skipped()
        {
            var registry = TuningRegistry.Load(Memory(Good.Replace("\"version\": 1,", "\"version\": 1, \"_note\": \"hello\",")));
            Assert.That(registry.Default.Name, Is.EqualTo("standard"));
        }

        [Test]
        public void The_profile_drives_the_core_types()
        {
            var p = TestTuning.Profile;
            var ai = new AiOpponent(AiDifficulty.Hard, p.Ai, new Random(1));
            Assert.That(ai.Range.Min, Is.EqualTo(0.20));
            Assert.That(() => new AiOpponent(AiDifficulty.Hard, null), Throws.ArgumentNullException);
            Assert.That(new Match(p.Match.WinsNeeded).WinsNeeded, Is.EqualTo(2));
        }
    }
}
