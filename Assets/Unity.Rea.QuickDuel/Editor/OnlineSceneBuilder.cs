#if QUICKDUEL_MULTIPLAYER
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Editor;
using Unity.Netcode;
using Unity.Rea.QuickDuel.Online;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Rea.QuickDuel.EditorTools
{
    // The online half of the generated Duel scene (UQD-T21): the OnlineDuel network prefab and its prefab list, the
    // scene's NetworkManager (Unity Transport and a NetBootstrap, through Unity.Multiplayer's own NetSceneSetup) and
    // the Host / Join menu on the Duel object, wired in code. Run by DuelSceneBuilder; never by hand.
    public static class OnlineSceneBuilder
    {
        public const string Folder = "Assets/Unity.Rea.QuickDuel/Online";
        public const string PrefabPath = Folder + "/OnlineDuel.prefab";
        public const string PrefabListPath = Folder + "/QuickDuelNetPrefabs.asset";

        public static void Build(Scene scene, GameObject duel)
        {
            var prefab = EnsurePrefab();
            var list = EnsurePrefabList(prefab);

            var setup = NetSceneSetup.Run(scene, new NetSceneSetupOptions { PrefabList = list, RegisterProjectPrefabs = false });
            ConfigureBootstrap(setup.Bootstrap);

            duel.AddComponent<OnlineDuelMenu>().Wire(setup.Bootstrap, prefab);
        }

        // The bootstrap never starts a session by itself: not on a scene load, and above all not in a headless run
        // (batch-mode tests and builds load this scene too), where its default would serve as a dedicated server.
        // Hosting and joining are the menu's. One opponent, joined by address, so no join-code service is tried.
        private static void ConfigureBootstrap(NetBootstrap bootstrap)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("autoStart").boolValue = false;
            serialized.FindProperty("serveWhenHeadless").boolValue = false;
            serialized.FindProperty("signInAtStartup").boolValue = false;
            serialized.FindProperty("maxPlayers").intValue = 2;
            serialized.FindProperty("joinCodes").intValue = (int)JoinCodeProvider.None;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject EnsurePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<OnlineDuel>() == null || prefab.GetComponent<NetKey>() == null)
            {
                var go = new GameObject("OnlineDuel");
                go.AddComponent<NetworkObject>();
                go.AddComponent<NetKey>();
                go.AddComponent<OnlineDuel>();
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Object.DestroyImmediate(go);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            EnsureGlobalObjectIdHash(prefab);
            return prefab;
        }

        // NGO derives a prefab's GlobalObjectIdHash (what every peer matches a spawn by) in NetworkObject.OnValidate,
        // which the editor only runs when the prefab is touched in its UI; a prefab made in batch mode keeps 0.
        private static void EnsureGlobalObjectIdHash(GameObject prefab)
        {
            var networkObject = prefab.GetComponent<NetworkObject>();
            var validate = typeof(NetworkObject).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            validate?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            AssetDatabase.SaveAssets();
            var hash = new SerializedObject(networkObject).FindProperty("GlobalObjectIdHash");
            if (hash == null || hash.longValue == 0)
                Debug.LogError($"QuickDuel: {PrefabPath} has no GlobalObjectIdHash; open it in the editor once and save.");
        }

        private static NetPrefabList EnsurePrefabList(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetPrefabList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetPrefabList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }
            list.Add(prefab);
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }
    }
}
#endif
