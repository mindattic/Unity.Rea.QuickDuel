// Online PvP in the Duel scene (UQD-T21): two Duel rigs, one on the host's session and one on the joined client's,
// each driven by its own OnlineDuelLobby exactly as the scene's Host / Join menu drives it, so DuelController,
// DuelView and DuelSession play the OnlineDuel instead of their local MatchSession.
using System.Collections;
using NUnit.Framework;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Rea.QuickDuel.Online.Tests
{
    public class OnlineDuelSceneTests : NetSessionTest
    {
        private GameObject prefab;
        private GameObject hostRig;
        private GameObject clientRig;

        public OnlineDuelSceneTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            prefab = CreatePrefab<OnlineDuel>("OnlineDuel");
            prefab.AddComponent<NetKey>();
        }

        protected override IEnumerator OnSessionsTearDown()
        {
            if (hostRig != null) Object.Destroy(hostRig);
            if (clientRig != null) Object.Destroy(clientRig);
            yield return null;
        }

        private static GameObject Rig(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<DuelController>();
            go.AddComponent<DuelView>();
            go.AddComponent<DuelSession>();
            return go;
        }

        private IEnumerator Online(System.Action<DuelController, DuelController, OnlineDuelLobby> found)
        {
            hostRig = Rig("HostDuel");
            clientRig = Rig("ClientDuel");
            var host = hostRig.GetComponent<DuelController>();
            var client = clientRig.GetComponent<DuelController>();
            yield return null;   // Start: the views and labels are built

            var hostLobby = DisposeAtTearDown(new OnlineDuelLobby(Server, prefab, host));
            DisposeAtTearDown(new OnlineDuelLobby(Client, prefab, client));
            yield return StartAndJoinAll();
            yield return WaitUntil(() => host.IsOnline && client.IsOnline, "the duel never reached both rigs");
            yield return WaitUntil(() => host.Online.Seated && client.Online.Seated, "the joined player was never seated");

            hostLobby.Duel.Configure(new DuelSettings
            {
                CountdownSeconds = 0.2,
                MinStandoffSeconds = 0.3,
                MaxStandoffSeconds = 0.6,
                StrikeTimeoutSeconds = 1.0,
                SettleSeconds = TuningStore.Profile.Duel.SettleSeconds
            }, TuningStore.Profile.Match.WinsNeeded, TuningStore.Profile.Judge.HumanFloorSeconds,
                TuningStore.Profile.Online.ReportGraceSeconds);
            found(host, client, hostLobby);
        }

        [UnityTest]
        public IEnumerator TheHostStartsTheRound_AndBothScenesShowTheHostsVerdictAndScore()
        {
            DuelController host = null, client = null;
            yield return Online((h, c, _) => { host = h; client = c; });
            var hostView = hostRig.GetComponent<DuelView>();
            var clientView = clientRig.GetComponent<DuelView>();
            var hostSession = hostRig.GetComponent<DuelSession>();
            var clientSession = clientRig.GetComponent<DuelSession>();

            Assert.That(host.LocalSide, Is.EqualTo(Side.Left));
            Assert.That(host.Phase, Is.EqualTo(DuelPhase.Ready));
            Assert.That(hostSession.ModeText, Does.StartWith("Online PvP (hosting)"));
            Assert.That(clientSession.ModeText, Does.StartWith("Online PvP (joined)"));
            Assert.That(hostView.PromptText, Does.Contain("Press Space or Start to duel"));
            Assert.That(clientView.PromptText, Does.Contain("Waiting for the host"));
            Assert.That(hostSession.CycleMode(), Is.False, "the mode is the local game's, not the online duel's");

            Assert.That(client.BeginRound(), Is.False, "only the host begins rounds");
            Assert.That(host.BeginRound(), Is.True);
            yield return WaitUntil(() => host.Phase == DuelPhase.Strike && client.Phase == DuelPhase.Strike,
                () => $"both rounds never reached the cue (host {host.Phase}, client {client.Phase})");
            yield return null;   // the views redraw
            Assert.That(client.LocalSide, Is.EqualTo(Side.Right));
            Assert.That(hostView.Inverted, Is.True, "the cue is the backdrop's invert");
            Assert.That(clientView.Inverted, Is.True);

            // Each strikes on its own clock from its own cue; which key does not matter, the seat is the machine's.
            host.Strike(Side.Left, host.CueAt + 0.30);
            client.Strike(Side.Left, client.CueAt + 0.20);

            yield return WaitUntil(() => host.Result != null && client.Result != null, "a verdict never reached both scenes");
            yield return null;   // the views redraw
            foreach (var (controller, view, session) in new[] { (host, hostView, hostSession), (client, clientView, clientSession) })
            {
                Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));
                Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
                Assert.That(controller.Result.LeftReaction, Is.EqualTo(0.30).Within(1e-6));
                Assert.That(controller.Result.RightReaction, Is.EqualTo(0.20).Within(1e-6));
                Assert.That(view.WinsText, Is.EqualTo("Wins!"));
                Assert.That(view.WinsSide, Is.EqualTo(Side.Left), "the host's verdict puts the banner on the loser's side");
                Assert.That(view.Motion(Side.Left).Pose, Is.EqualTo(FighterPose.Defeated));
                Assert.That(view.Motion(Side.Right).Pose, Is.EqualTo(FighterPose.Attacking));
                Assert.That(session.Match.WinsOf(Side.Right), Is.EqualTo(1));
                Assert.That(session.ScoreText(Side.Right), Does.Contain("1/"));
            }
            Assert.That(hostSession.ScoreText(Side.Left), Does.StartWith("Left (you)"));
            Assert.That(clientSession.ScoreText(Side.Right), Does.StartWith("Right (you)"));
            Assert.That(host.CanBeginOnlineRound, Is.True, "the host can start the next round");
        }

        [UnityTest]
        public IEnumerator WhenThePlayerLeaves_TheirSeatEmpties_AndTheirSceneIsLocalAgain()
        {
            DuelController host = null, client = null;
            OnlineDuelLobby hostLobby = null;
            yield return Online((h, c, l) => { host = h; client = c; hostLobby = l; });

            Assert.That(Client.Leave(), Is.True);
            yield return WaitUntil(() => !client.IsOnline, "the client's scene never went back to local play");
            yield return WaitUntil(() => !hostLobby.Duel.Seated, "the host never freed the leaver's seat");
            Assert.That(host.IsOnline, Is.True, "the host keeps hosting");
            Assert.That(host.BeginRound(), Is.False, "no round without an opponent");
            yield return null;   // the view redraws
            Assert.That(hostRig.GetComponent<DuelView>().PromptText, Does.Contain("Waiting for a player to join"));

            // Back to local play: the local game runs on as before.
            Assert.That(client.Phase, Is.EqualTo(DuelPhase.Ready));
            Assert.That(clientRig.GetComponent<DuelSession>().ModeText, Is.Empty, "local play draws no mode line");
            Assert.That(client.BeginRound(), Is.True);
        }
    }
}
