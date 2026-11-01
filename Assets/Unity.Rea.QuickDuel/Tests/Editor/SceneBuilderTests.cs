using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTests
{
    // The built scenes (UQD-T51): each committed scene is opened (additively, then closed) and checked for the
    // controller and assets its builder wires, and the build order puts the intro first, then the menu, then the
    // duel. These check the builders' committed output, not a fresh run: the builders write fixed paths under
    // Assets/Scenes and register them in the build settings, so running them from a test would rewrite the
    // committed scenes (follow-up: let a builder target another path so a test can run it into a temporary scene).
    public class SceneBuilderTests
    {
        private EditorBuildSettingsScene[] savedBuildScenes;

        [SetUp]
        public void Remember() => savedBuildScenes = EditorBuildSettings.scenes;

        [TearDown]
        public void Restore() => EditorBuildSettings.scenes = savedBuildScenes;

        private static void InScene<T>(string path, System.Action<T> check) where T : Component
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var found = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<T>(true)).FirstOrDefault();
                Assert.That(found, Is.Not.Null, $"{path} has no {typeof(T).Name}");
                check(found);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EveryBuiltSceneExistsOnDiskWithAnOrthographicMainCamera()
        {
            foreach (var path in BuildScenes.Order)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, $"{path} is missing");
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var camera = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
                    Assert.That(camera, Is.Not.Null, $"{path} has no camera");
                    Assert.That(camera.CompareTag("MainCamera"), Is.True, path);
                    Assert.That(camera.orthographic, Is.True, path);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void TheIntroSceneHoldsItsControllerAndPointsAtTheMenu()
        {
            InScene<IntroController>(IntroSceneBuilder.ScenePath, intro =>
                Assert.That(intro.NextScene, Is.EqualTo(MainMenuSceneBuilder.SceneName)));
        }

        [Test]
        public void TheMainMenuSceneHoldsItsControllerAndTheKatanaCursor()
        {
            InScene<MainMenuController>(MainMenuSceneBuilder.ScenePath, menu =>
                Assert.That(menu.GetComponent<KatanaCursor>(), Is.Not.Null));
        }

        [Test]
        public void TheDuelSceneHoldsTheControllerAndEveryDuelPart()
        {
            InScene<DuelController>(DuelSceneBuilder.ScenePath, duel =>
            {
                foreach (var part in new[] { typeof(DuelSession), typeof(DuelFade), typeof(DuelVerdict) })
                    Assert.That(duel.GetComponent(part), Is.Not.Null, $"the duel scene's controller has no {part.Name}");
            });
        }

        [Test]
        public void TheBuildOrderIsIntroMenuDuel()
        {
            Assert.That(BuildScenes.Order, Is.EqualTo(new[]
            {
                "Assets/Scenes/Intro.unity", "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Duel.unity",
            }));
        }

        [Test]
        public void RegisteringAScenePutsTheKnownOnesInOrderAndKeepsStrangersAfterThem()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Other.unity", true),
                new EditorBuildSettingsScene(DuelSceneBuilder.ScenePath, true),
            };
            BuildScenes.Register(IntroSceneBuilder.ScenePath);
            BuildScenes.Register(MainMenuSceneBuilder.ScenePath);
            BuildScenes.Register(MainMenuSceneBuilder.ScenePath); // twice: no duplicate
            Assert.That(EditorBuildSettings.scenes.Select(s => s.path), Is.EqualTo(new[]
            {
                IntroSceneBuilder.ScenePath, MainMenuSceneBuilder.ScenePath, DuelSceneBuilder.ScenePath,
                "Assets/Scenes/Other.unity",
            }));
            Assert.That(EditorBuildSettings.scenes.All(s => s.enabled), Is.True);
        }
    }
}
