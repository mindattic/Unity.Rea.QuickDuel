using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTests
{
    // The settings registry's validation as the editor sees it (UQD-T51): the tuning.json that ships in
    // StreamingAssets loads through the same DirectorySettingsSource a build reads, and a bad copy of it is
    // refused with every problem named by file and key. (The rules' own cases run in Core.Tests.)
    public class SettingsRegistryValidationTests
    {
        private static string ShippedFolder => Path.Combine(Application.streamingAssetsPath, TuningRegistry.Folder);

        private static string ShippedText => File.ReadAllText(Path.Combine(ShippedFolder, TuningRegistry.File));

        private static string[] Errors(string text) =>
            Assert.Throws<SettingsException>(() =>
                TuningRegistry.Load(new MemorySettingsSource().Set(TuningRegistry.File, text))).Errors.ToArray();

        [Test]
        public void TheShippedFileLoadsAndNamesAUsableDefaultProfile()
        {
            var registry = TuningRegistry.Load(new DirectorySettingsSource(ShippedFolder));
            Assert.That(registry.Profiles, Is.Not.Empty);
            Assert.That(registry.Default, Is.Not.Null);
            Assert.That(registry.Has("standard"), Is.True);
            Assert.That(registry.Default.Match.WinsNeeded, Is.GreaterThanOrEqualTo(1));
            Assert.That(registry.Default.Duel.MinStandoffSeconds, Is.LessThanOrEqualTo(registry.Default.Duel.MaxStandoffSeconds));
            Assert.That(registry.Default.Taunts, Is.Not.Empty);
        }

        [Test]
        public void AMissingFileIsReportedAsNotFound()
        {
            var e = Assert.Throws<SettingsException>(() =>
                TuningRegistry.Load(new DirectorySettingsSource(Path.Combine(Path.GetTempPath(), "quickduel-no-such-folder"))));
            Assert.That(e.Errors.Single(), Does.StartWith(TuningRegistry.File + ": not found"));
        }

        [Test]
        public void TextThatIsNotJsonOrNotAnObjectIsRefused()
        {
            Assert.That(Errors("{ nope").Single(), Does.Contain("is not valid JSON"));
            Assert.That(Errors("[1, 2]").Single(), Does.Contain("must hold one JSON object"));
        }

        [Test]
        public void ADefaultThatNamesNoProfileIsRefused()
        {
            var bad = Regex.Replace(ShippedText, "\"default\":\\s*\"[^\"]*\"", "\"default\": \"missing\"");
            Assume.That(bad, Is.Not.EqualTo(ShippedText), "the shipped file's default line changed shape");
            Assert.That(Errors(bad).Any(e => e.Contains("default")), Is.True);
        }

        [Test]
        public void AWrongFormatIsRefused()
        {
            var bad = Regex.Replace(ShippedText, "\"format\":\\s*\"[^\"]*\"", "\"format\": \"something.else\"");
            Assume.That(bad, Is.Not.EqualTo(ShippedText), "the shipped file's format line changed shape");
            Assert.That(Errors(bad).Any(e => e.Contains("format")), Is.True);
        }

        [Test]
        public void AnOutOfRangeFigureIsNamedByKey()
        {
            var bad = Regex.Replace(ShippedText, "\"winsNeeded\":\\s*\\d+", "\"winsNeeded\": 0");
            Assume.That(bad, Is.Not.EqualTo(ShippedText), "the shipped file's winsNeeded changed shape");
            Assert.That(Errors(bad).Any(e => e.Contains("winsNeeded")), Is.True);
        }
    }
}
