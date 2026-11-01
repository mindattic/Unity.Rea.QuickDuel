using System;
using System.Collections.Generic;
using System.IO;
using Unity.Rea.QuickDuel.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTools
{
    // Generates Assets/Scenes/MainMenu.unity (UQD-T45), the scene the intro leads to, so it is never hand-edited YAML.
    // Run from the menu or in batch mode:
    // Unity -batchmode -quit -executeMethod Unity.Rea.QuickDuel.EditorTools.MainMenuSceneBuilder.Build
    public static class MainMenuSceneBuilder
    {
        public const string SceneName = "MainMenu";
        public const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";

        // Where New Game and Versus lead: the original's gamescreen.
        public const string NextScene = "Duel";

        [MenuItem("QuickDuel/Rebuild Main Menu Scene")]
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
            var layout = new MainMenu(0, AiDifficulty.Normal, new System.Random(0));
            var buttons = new List<Sprite>();
            var buttonsOver = new List<Sprite>();
            foreach (var button in layout.Buttons)
            {
                buttons.Add(Sprite(manifest, button.Sprite));
                buttonsOver.Add(Sprite(manifest, button.OverSprite));
            }

            var menu = new GameObject("MainMenu");
            menu.AddComponent<MainMenuController>().SetAssets(NextScene, Sprite(manifest, "bg_splatter_scroll"),
                Sprite(manifest, "spr_titlescreen"), DuelSceneBuilder.LoadingImages(), buttons, buttonsOver,
                Clip(manifest, "sfx_flick"), Clip(manifest, "sfx_click"));
            // The credits screen (UQD-T47): the original's credits.txt, read by the menu when it starts, and the author.
            var creditsPath = $"{OriginalAssetGenerator.GameFolder}/{manifest.Credits}";
            var creditsText = AssetDatabase.LoadAssetAtPath<TextAsset>(creditsPath);
            if (creditsText == null)
                throw new InvalidOperationException($"QuickDuel: the main menu has no credits text at {creditsPath}.");
            CreditsRoll.FromText(manifest.Author, creditsText.text); // a credits file that does not read stops the build
            menu.GetComponent<MainMenuController>().SetCredits(creditsText, manifest.Author);
            // obj_mouse (UQD-T46): the katana cursor in place of the system cursor.
            menu.AddComponent<KatanaCursor>().SetSprite(Sprite(manifest, "spr_katana_cursor"));

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
                throw new InvalidOperationException($"QuickDuel: the main menu has no sprite at {path}.");
            return sprite;
        }

        private static AudioClip Clip(AssetManifest manifest, string id)
        {
            var path = $"{OriginalAssetGenerator.GameFolder}/{manifest.Sound(id).Path}";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                throw new InvalidOperationException($"QuickDuel: the main menu has no sound at {path}.");
            return clip;
        }
    }
}
