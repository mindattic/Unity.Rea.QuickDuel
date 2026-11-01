// The generated Duel scene's Host / Join menu (UQD-T21): it is there, the scene's NetBootstrap stays idle on its own
// (even headless, where it would otherwise serve), and Host / Leave put the scene online and back.
using System.Collections;
using NUnit.Framework;
using Unity.Multiplayer;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Rea.QuickDuel.Online.Tests
{
    public class OnlineDuelMenuTests
    {
        private DuelController controller;
        private OnlineDuelMenu menu;
        private NetBootstrap bootstrap;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;   // Start: the menu is built
            controller = Object.FindAnyObjectByType<DuelController>();
            menu = Object.FindAnyObjectByType<OnlineDuelMenu>();
            bootstrap = Object.FindAnyObjectByType<NetBootstrap>();
            Assert.That(menu, Is.Not.Null, "the Duel scene has no online menu");
            Assert.That(bootstrap, Is.Not.Null, "the Duel scene has no NetBootstrap");
        }

        [UnityTearDown]
        public IEnumerator Leave()
        {
            if (bootstrap != null && bootstrap.Session != null && bootstrap.Session.IsActive)
            {
                bootstrap.Leave();
                yield return NetAwait.Until(() => !bootstrap.Session.IsActive, "the session never ended");
            }
        }

        [UnityTest]
        public IEnumerator TheSceneStartsLocal_WithTheMenuShownAndNoSessionRunning()
        {
            yield return null;
            Assert.That(bootstrap.Session.IsActive, Is.False, "the scene must never host or serve by itself");
            Assert.That(controller.IsOnline, Is.False);
            Assert.That(menu.IsShown, Is.True);
            Assert.That(menu.Address, Is.EqualTo("127.0.0.1"));
        }

        [UnityTest]
        public IEnumerator HostPutsTheSceneOnline_AndLeaveTakesItBack()
        {
            Unity.Rea.QuickDuel.Tests.Screenshots.Capture("online-menu");
            bootstrap.Configure(c => c.Port = FreePort.Udp());
            Assert.That(menu.Host(), Is.True, menu.StatusText);
            yield return NetAwait.Until(() => controller.IsOnline, () => "the scene never went online: " + menu.StatusText);
            Assert.That(controller.LocalSide, Is.EqualTo(Core.Side.Left));
            Assert.That(controller.BeginRound(), Is.False, "no round before a player joins");
            yield return null;
            Assert.That(menu.StatusText, Does.StartWith("Hosting on port"));
            Assert.That(Object.FindAnyObjectByType<DuelView>().PromptText, Does.Contain("Waiting for a player to join"));
            Unity.Rea.QuickDuel.Tests.Screenshots.Capture("online-hosting");

            Assert.That(menu.Leave(), Is.True);
            yield return NetAwait.Until(() => !controller.IsOnline && !bootstrap.Session.IsActive, "the scene never went back to local play");
            yield return null;
            Assert.That(Object.FindAnyObjectByType<DuelSession>().ModeText, Is.Empty, "local play draws no mode line");
        }

        [UnityTest]
        public IEnumerator AJoinAddressThatDoesNotParseIsRefusedWithAReason()
        {
            Assert.That(menu.Join("not an address:port"), Is.False);
            yield return null;
            Assert.That(bootstrap.Session.IsActive, Is.False);
            Assert.That(menu.StatusText, Is.Not.Empty);
        }
    }
}
