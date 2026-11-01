// The online duel on the wire (UQD-T14): a host and one joined client, each a duelist. The host draws the round's
// lengths and both peers get the same ones; each plays the round on its own clock and reports only its reaction;
// the host judges with DuelJudge and both peers record the same verdict. The protocol itself is tested without
// Unity: dotnet test Core.slnx (Tests/Core.Tests/OnlineDuelTests.cs).
using System.Collections;
using NUnit.Framework;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Rea.QuickDuel.Online.Tests
{
    public class OnlineDuelTests : NetSessionTest
    {
        private GameObject prefab;

        public OnlineDuelTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated() => prefab = CreatePrefab<OnlineDuel>("OnlineDuel");

        private IEnumerator Seated(System.Action<OnlineDuel, OnlineDuel> found)
        {
            yield return StartAndJoinAll();
            var onHost = Spawn<OnlineDuel>(prefab);
            onHost.Configure(new DuelSettings
            {
                CountdownSeconds = 0.2,
                MinStandoffSeconds = 0.3,
                MaxStandoffSeconds = 0.6,
                StrikeTimeoutSeconds = 1.0,
                SettleSeconds = TuningStore.Profile.Duel.SettleSeconds
            }, TuningStore.Profile.Match.WinsNeeded, TuningStore.Profile.Judge.HumanFloorSeconds,
                TuningStore.Profile.Online.ReportGraceSeconds);
            var onClient = WaitForCopy(Client, onHost);
            yield return onClient;
            onHost.Seat(Server.LocalClientId, Client.LocalClientId);
            yield return WaitUntil(() => onClient.Result.RightClient == Client.LocalClientId, "the client never learnt the seats");
            found(onHost, onClient.Result);
        }

        [UnityTest]
        public IEnumerator BothDuelistsGetTheHostsStandoff_AndTheFasterReportedReactionWinsOnBothPeers()
        {
            OnlineDuel host = null, client = null;
            yield return Seated((h, c) => { host = h; client = c; });

            Assert.That(host.BeginRound(), Is.True);
            yield return WaitUntil(() => host.Local != null && host.Local.Round == 1, "the host's duelist never began round 1");
            yield return WaitUntil(() => client.Local != null && client.Local.Round == 1, "the client's duelist never began round 1");
            Assert.That(host.LocalSide, Is.EqualTo(Side.Left));
            Assert.That(client.LocalSide, Is.EqualTo(Side.Right));
            Assert.That(client.Local.StandoffSeconds, Is.EqualTo(host.Host.StandoffSeconds), "the client plays the host's standoff");
            Assert.That(host.BeginRound(), Is.False, "one round at a time");

            // Each strikes at a time stamped on its own clock: the client is faster, whatever the delivery order.
            host.Strike(host.Local.Duel.CueAt + 0.35);
            client.Strike(client.Local.Duel.CueAt + 0.25);

            yield return WaitUntil(() => client.LastJudgedRound == 1, "the client never got the verdict");
            yield return WaitUntil(() => host.LastJudgedRound == 1, "the host never got the verdict");
            foreach (var peer in new[] { host, client })
            {
                Assert.That(peer.LastResult.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
                Assert.That(peer.LastResult.Winner, Is.EqualTo(Side.Right));
                Assert.That(peer.LastResult.LeftReaction, Is.EqualTo(0.35).Within(1e-6));
                Assert.That(peer.LastResult.RightReaction, Is.EqualTo(0.25).Within(1e-6));
                Assert.That(peer.Match.WinsOf(Side.Right), Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator AnEarlyStrikeLoses_AndASilentDuelistCountsAsNoStrike()
        {
            OnlineDuel host = null, client = null;
            yield return Seated((h, c) => { host = h; client = c; });

            Assert.That(host.BeginRound(), Is.True);
            yield return WaitUntil(() => client.Local != null && client.Local.Round == 1, "the client never began round 1");
            client.Strike(client.Local.Duel.CueAt - 0.1);

            yield return WaitUntil(() => client.LastJudgedRound == 1, "the client never got the verdict");
            Assert.That(client.LastResult.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(client.LastResult.Winner, Is.EqualTo(Side.Left));
            Assert.That(client.LastResult.LeftReaction, Is.Null, "the host never struck");
        }

        [UnityTest]
        public IEnumerator AReportFromAConnectionThatIsNotSeatedIsIgnored()
        {
            OnlineDuel host = null, client = null;
            yield return Seated((h, c) => { host = h; client = c; });
            host.Seat(Server.LocalClientId, 999);   // the joined client is now a spectator

            Assert.That(host.BeginRound(), Is.True);
            yield return WaitUntil(() => client.LastJudgedRound == 0 && client.RightClient == 999, "the client never learnt it was unseated");
            yield return WaitUntil(() => host.Local != null && host.Local.Round == 1, "the host's duelist never began round 1");
            client.ReportReaction(1, true, 0.2);
            host.Strike(host.Local.Duel.CueAt + 0.3);

            yield return WaitUntil(() => client.LastJudgedRound == 1, "the client never got the verdict", timeoutSeconds: 10f);
            Assert.That(client.LastResult.Winner, Is.EqualTo(Side.Left), "only the seated host's report counted");
            Assert.That(client.LastResult.RightReaction, Is.Null);
            Assert.That(client.Local, Is.Null, "a spectator plays no local duel");
        }
    }
}
