using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Unity.Rea.QuickDuel.EditorTools
{
    // The player's icon (UQD-T50): the original's bushidocoda.ico (Global Game Settings' Windows icon), its 64 x 64 frame
    // as Art/Frame/bushidocoda.png, set as the default icon for every platform. Set in code, not by hand in Player
    // Settings; QuickDuelBuild runs it before every build, and it leaves Player Settings alone when the icon is already set.
    // Run from the menu or in batch mode: -executeMethod Unity.Rea.QuickDuel.EditorTools.PlayerIconBuilder.Build
    public static class PlayerIconBuilder
    {
        public const string IconPath = OriginalAssetGenerator.GameFolder + "/Art/Frame/bushidocoda.png";

        [MenuItem("QuickDuel/Set Player Icon")]
        public static void Build() => Ensure();

        // Sets the default icon to the original's, unless it already is; the icon set, or null when the file is missing.
        public static Texture2D Ensure()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogError($"QuickDuel: the original's icon is missing at {IconPath}");
                return null;
            }

            var current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (current.Length == 1 && current[0] == icon)
                return icon;

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            return icon;
        }

        // Whether the default icon is the original's.
        public static bool IsSet() =>
            PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any).Any(t => t != null
                && AssetDatabase.GetAssetPath(t) == IconPath);
    }
}
