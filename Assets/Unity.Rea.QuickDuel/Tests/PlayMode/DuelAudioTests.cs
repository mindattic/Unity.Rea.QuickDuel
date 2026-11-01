using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    public class DuelAudioTests
    {
        [UnityTest]
        public IEnumerator ARoundPlaysBeepsDrumrollUnsheathAndSlash()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            var controller = Object.FindFirstObjectByType<DuelController>();
            var audio = controller.GetComponent<DuelAudio>();
            Assert.That(audio, Is.Not.Null, "Duel scene has no DuelAudio.");
            controller.Settings = new DuelSettings
            {
                CountdownSeconds = 2.9, MinStandoffSeconds = 0.2, MaxStandoffSeconds = 0.3, StrikeTimeoutSeconds = 1, SettleSeconds = TuningStore.Profile.Duel.SettleSeconds
            };

            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Standoff);
            Assert.That(audio.DrumrollPlaying, Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            Assert.That(audio.DrumrollPlaying, Is.False);
            controller.Strike(Side.Left, controller.CueAt + 0.2);
            controller.Strike(Side.Right, controller.CueAt + 0.25);

            // Left struck faster, so determine_winner.gml's victory sting follows the slash.
            Assert.That(audio.Played, Is.EqualTo(new[]
            {
                DuelAudio.Beep, DuelAudio.Beep, DuelAudio.Beep, DuelAudio.Drumroll, DuelAudio.Unsheath, DuelAudio.Slash,
                DuelAudio.Victory
            }));
        }

        [UnityTest]
        public IEnumerator TheRightDuelistWinningPlaysDefeatNotVictory()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            var controller = Object.FindFirstObjectByType<DuelController>();
            var audio = controller.GetComponent<DuelAudio>();
            controller.Settings = new DuelSettings
            {
                CountdownSeconds = 2.9, MinStandoffSeconds = 0.2, MaxStandoffSeconds = 0.3, StrikeTimeoutSeconds = 1, SettleSeconds = TuningStore.Profile.Duel.SettleSeconds
            };

            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            controller.Strike(Side.Right, controller.CueAt + 0.2);
            controller.Strike(Side.Left, controller.CueAt + 0.25);

            Assert.That(audio.Played, Does.Contain(DuelAudio.Defeat));
            Assert.That(audio.Played, Does.Not.Contain(DuelAudio.Victory));
        }

        [UnityTest]
        public IEnumerator NoStrikeStaysSilentOnVictoryAndDefeat()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            var controller = Object.FindFirstObjectByType<DuelController>();
            var audio = controller.GetComponent<DuelAudio>();
            controller.Settings = new DuelSettings
            {
                CountdownSeconds = 2.9, MinStandoffSeconds = 0.2, MaxStandoffSeconds = 0.3, StrikeTimeoutSeconds = 0.1, SettleSeconds = TuningStore.Profile.Duel.SettleSeconds
            };

            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);

            Assert.That(audio.Played, Does.Not.Contain(DuelAudio.Victory));
            Assert.That(audio.Played, Does.Not.Contain(DuelAudio.Defeat));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator TheSceneCarriesTheBushidoCodaClips()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            var sources = Object.FindFirstObjectByType<DuelAudio>();
            var so = new UnityEditor.SerializedObject(sources);
            foreach (var field in new[] { "beep", "drumroll", "unsheath", "slash", "victory", "defeat" })
                Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
#endif
    }
}
