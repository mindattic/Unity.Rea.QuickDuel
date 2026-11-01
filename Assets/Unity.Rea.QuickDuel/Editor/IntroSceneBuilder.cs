using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Rea.QuickDuel.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTools
{
    // Generates Assets/Scenes/Intro.unity (UQD-T44), the game's first scene, so it is never hand-edited YAML. Run from
    // the menu or in batch mode: Unity -batchmode -quit -executeMethod Unity.Rea.QuickDuel.EditorTools.IntroSceneBuilder.Build
    public static class IntroSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Intro.unity";

        // Where the intro leads: the original's mainmenu (UQD-T45).
        public const string NextScene = MainMenuSceneBuilder.SceneName;

        [MenuItem("QuickDuel/Rebuild Intro Scene")]
        public static void Build()
        {
            RenderPipelineBuilder.Ensure();
            DuelPanelBuilder.Ensure();
            OriginalFontBuilder.Ensure();
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            cameraObject.AddComponent<AudioListener>();

            var manifest = Manifest();
            var clouds = new List<Sprite>();
            for (var i = 1; i <= IntroScene.CloudKinds; i++)
                clouds.Add(Sprite(manifest, $"spr_cloud{i}"));
            var intro = new GameObject("Intro");
            intro.AddComponent<IntroController>().SetAssets(NextScene, Sprite(manifest, "bg_mapscreen"), clouds,
                DuelSceneBuilder.LoadingImages(), manifest.IntroText);

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Register(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"QuickDuel: wrote {ScenePath}");
        }

        // A manifest that fails validation stops the build rather than shipping a scene with a guess.
        private static AssetManifest Manifest() =>
            AssetManifest.Load(new DirectorySettingsSource(Path.GetFullPath(OriginalAssetGenerator.DataFolder)));

        private static Sprite Sprite(AssetManifest manifest, string id)
        {
            var path = $"{OriginalAssetGenerator.GameFolder}/{manifest.Sprite(id).Frames[0]}";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException($"QuickDuel: the intro has no sprite at {path}.");
            return sprite;
        }
    }

    // The player's scenes, in the order the game runs them: the intro first (UQD-T44), the main menu (UQD-T45), then the
    // duel.
    public static class BuildScenes
    {
        public static readonly string[] Order = { IntroSceneBuilder.ScenePath, MainMenuSceneBuilder.ScenePath, DuelSceneBuilder.ScenePath };

        // Adds `path` to the build settings, keeping the scenes this game knows in Order and any others after them.
        public static void Register(string path)
        {
            var paths = EditorBuildSettings.scenes.Select(s => s.path).Where(p => p != path).Append(path).Distinct().ToList();
            var known = Order.Where(paths.Contains);
            var others = paths.Where(p => Array.IndexOf(Order, p) < 0);
            EditorBuildSettings.scenes = known.Concat(others).Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        }
    }
}
