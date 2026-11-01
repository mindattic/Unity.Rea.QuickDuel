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
    // Generates Assets/Scenes/Duel.unity so the scene is never hand-edited YAML. Run from the menu or
    // in batch mode: Unity -batchmode -quit -executeMethod Unity.Rea.QuickDuel.EditorTools.DuelSceneBuilder.Build
    public static class DuelSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Duel.unity";

        [MenuItem("QuickDuel/Rebuild Duel Scene")]
        public static void Build()
        {
            RenderPipelineBuilder.Ensure();
            DuelPanelBuilder.Ensure();
            OriginalFontBuilder.Ensure();
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Black round the 640 x 480 frame (UQD-T50).
            camera.backgroundColor = Color.black;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            cameraObject.AddComponent<AudioListener>();

            var duel = new GameObject("Duel");
            duel.AddComponent<DuelController>();
            duel.AddComponent<DuelInput>();
            duel.AddComponent<DuelQuit>();
            var view = duel.AddComponent<DuelView>();
            view.SetArenas(Arenas());
            view.SetFighters(Fighters());
            duel.AddComponent<DuelTaunt>();
            duel.AddComponent<DuelBlood>().SetSprites(BloodDrops(), Splatters(), Frames(Manifest(), "spr_flash", "the flash")[0]);
            // obj_weapon (UQD-T41): the Myrmidon's shield (each fighter's own weapon comes with its look) and the four
            // sounds it chooses from as it lands.
            duel.AddComponent<DuelWeapons>().SetAssets(
                Frames(Manifest(), "spr_weapon_myrmidon_shield", "the shield")[0],
                Frames(Manifest(), "spr_weapon_myrmidon_shield_ground", "the shield")[0],
                new[] { Clip("sfx_hitground1"), Clip("sfx_hitground2"), Clip("sfx_hitground3"), Clip("sfx_hitground4") });
            // obj_fade_gamescreen (UQD-T42): the 14 loading images the round transition draws one of.
            duel.AddComponent<DuelFade>().SetImages(LoadingImages());
            // gameclock_startup (UQD-T66): each local round begins by itself after startup_time, or at once on a click.
            duel.AddComponent<DuelStartup>();
            // obj_cornerart_left and right (UQD-T43): the "2" corner arts they draw, sliding in on start.
            duel.AddComponent<DuelCornerArt>().SetSprites(
                Frames(Manifest(), "spr_cornerart_left2", "the corner art")[0],
                Frames(Manifest(), "spr_cornerart_right2", "the corner art")[0]);
            // obj_time (UQD-T50): the match clock dropping in at the top centre.
            duel.AddComponent<DuelClock>();
            // The arenas' ambient emitters (UQD-T49): the sprites of all eight, the original's unused three included.
            duel.AddComponent<DuelAmbient>().SetLooks(AmbientLooks());
            // obj_mouse (UQD-T46): the katana cursor in place of the system cursor.
            duel.AddComponent<KatanaCursor>().SetSprite(Frames(Manifest(), "spr_katana_cursor", "the cursor")[0]);
            duel.AddComponent<DuelSession>();
            // The victory and gameover rooms (UQD-T48): their backgrounds, the Retry / Continue button and its click, and
            // the scenes it leads to.
            duel.AddComponent<DuelVerdict>().SetAssets(
                Frames(Manifest(), VerdictScreen.VictoryBackground, "the verdict")[0],
                Frames(Manifest(), VerdictScreen.GameOverBackground, "the verdict")[0],
                Frames(Manifest(), "spr_btn_right4", "the verdict")[0],
                Frames(Manifest(), "spr_btn_right4_over", "the verdict")[0],
                Clip("sfx_click"),
                Path.GetFileNameWithoutExtension(MainMenuSceneBuilder.ScenePath),
                Path.GetFileNameWithoutExtension(ScenePath));
            // The original's F12 debug overlay (UQD-T50), the developer's toggle.
            duel.AddComponent<DuelDebugOverlay>();
            duel.AddComponent<DuelAudio>().SetClips(Clip("sfx_beep"), Clip("sfx_drumroll"), Clip("sfx_unsheath"), Clip("sfx_slash"),
                Clip("sfx_victory"), Clip("sfx_defeat"));
#if QUICKDUEL_MULTIPLAYER
            OnlineSceneBuilder.Build(scene, duel);
#endif

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Register(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"QuickDuel: wrote {ScenePath}");
        }

        // The 11 original arenas (UQD-T33) as the asset manifest lists them, each with its imported backdrop sprite.
        // A manifest that fails validation stops the build rather than shipping a scene with a guess.
        public static List<ArenaBackdrop> Arenas()
        {
            var manifest = Manifest();
            var arenas = new List<ArenaBackdrop>();
            foreach (var arena in manifest.Arenas)
                arenas.Add(new ArenaBackdrop(arena.Id, arena.Name, arena.Description, Frames(manifest, arena.Sprite, $"arena '{arena.Id}'")[0],
                    arena.Ambient));
            return arenas;
        }

        // The five original fighters (UQD-T34) as the asset manifest lists them (load_characters.gml's id order), each
        // with its imported idle, run, attack, defeat and weapon sprites. Cosmetic only: no stats travel.
        public static List<FighterLook> Fighters()
        {
            var manifest = Manifest();
            var fighters = new List<FighterLook>();
            foreach (var fighter in manifest.Fighters)
            {
                var owner = $"fighter '{fighter.Id}'";
                fighters.Add(new FighterLook(fighter.Id, fighter.Name,
                    Frames(manifest, fighter.Idle, owner)[0],
                    Frames(manifest, fighter.Run, owner)[0],
                    Frames(manifest, fighter.Attack, owner),
                    Frames(manifest, fighter.Defeat, owner),
                    Frames(manifest, fighter.Weapon, owner)[0],
                    Frames(manifest, fighter.GroundWeapon, owner)[0]));
            }
            return fighters;
        }

        // spr_blood_drop1 and 2 (UQD-T40), in that order.
        public static List<Sprite> BloodDrops()
        {
            var manifest = Manifest();
            return new List<Sprite>
            {
                Frames(manifest, "spr_blood_drop1", "the blood")[0],
                Frames(manifest, "spr_blood_drop2", "the blood")[0],
            };
        }

        // Every sprite an ambient effect draws (UQD-T49), each with all its frames, its size and its GameMaker origin.
        public static List<AmbientLook> AmbientLooks()
        {
            var manifest = Manifest();
            var looks = new List<AmbientLook>();
            var ids = new HashSet<string>();
            foreach (AmbientKind kind in Enum.GetValues(typeof(AmbientKind)))
                foreach (var id in AmbientKinds.SpritesOf(kind))
                {
                    if (!ids.Add(id)) continue;
                    var sprite = manifest.Sprite(id);
                    looks.Add(new AmbientLook(id, Frames(manifest, id, "the ambient effects"),
                        new Vector2(sprite.Width, sprite.Height), new Vector2(sprite.OriginX, sprite.OriginY)));
                }
            return looks;
        }

        // spr_loading1 to 14 (UQD-T42), in order.
        public static List<Sprite> LoadingImages()
        {
            var manifest = Manifest();
            var images = new List<Sprite>();
            for (var i = 1; i <= RoundFade.Images; i++)
                images.Add(Frames(manifest, $"spr_loading{i}", "the round transition")[0]);
            return images;
        }

        // spr_splatter1 to 10 (UQD-T40), in order, each with the box a click must land in: GameMaker's automatic
        // rectangle mask, the bounds of the image's opaque pixels, measured here from the file (it matches every
        // bbox_* the original's .sprite.gmx records).
        public static List<SplatterLook> Splatters()
        {
            var manifest = Manifest();
            var looks = new List<SplatterLook>();
            for (var i = 1; i <= SplatterField.Kinds; i++)
            {
                var id = $"spr_splatter{i}";
                var sprite = Frames(manifest, id, "the splatters")[0];
                looks.Add(new SplatterLook(sprite, OpaqueBounds($"{OriginalAssetGenerator.GameFolder}/{manifest.Sprite(id).Frames[0]}")));
            }
            return looks;
        }

        // The box, from the image's top left with width and height counting both edges, that holds every pixel with any
        // alpha, as GameMaker's automatic bounding box (tolerance 0).
        public static RectInt OpaqueBounds(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    throw new InvalidOperationException($"QuickDuel: could not read {path}.");
                var pixels = texture.GetPixels32();
                int w = texture.width, h = texture.height;
                int left = w, right = -1, top = h, bottom = -1;
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a == 0) continue;
                    var row = h - 1 - y; // GetPixels32 runs from the bottom row up
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, row);
                    bottom = Math.Max(bottom, row);
                }
                return right < 0 ? new RectInt(0, 0, w, h) : new RectInt(left, top, right - left + 1, bottom - top + 1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // A manifest that fails validation stops the build rather than shipping a scene with a guess.
        private static AssetManifest Manifest() =>
            AssetManifest.Load(new DirectorySettingsSource(Path.GetFullPath(OriginalAssetGenerator.DataFolder)));

        // Every imported frame of a manifest sprite, in order; a missing one stops the build.
        private static List<Sprite> Frames(AssetManifest manifest, string spriteId, string owner)
        {
            var frames = new List<Sprite>();
            foreach (var frame in manifest.Sprite(spriteId).Frames)
            {
                var path = $"{OriginalAssetGenerator.GameFolder}/{frame}";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                    throw new InvalidOperationException($"QuickDuel: {owner} has no sprite at {path}.");
                frames.Add(sprite);
            }
            return frames;
        }

        private static AudioClip Clip(string name)
        {
            var path = $"Assets/Unity.Rea.QuickDuel/Audio/{name}.ogg";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                Debug.LogWarning($"QuickDuel: missing {path}");
            return clip;
        }
    }
}
