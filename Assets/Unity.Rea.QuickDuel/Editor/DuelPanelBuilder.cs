using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel.EditorTools
{
    /// <summary>
    /// Builds the PanelSettings asset DuelView's UIDocument renders through (UQD-T31): the UI was legacy
    /// uGUI Text off <c>Resources.GetBuiltinResource("LegacyRuntime.ttf")</c> with a hand-built Canvas;
    /// it is now UI Toolkit, styled by <c>Resources/UI/Duel.uss</c> and set in the original's bitmap fonts
    /// (<c>Resources/UI/Fonts/fnt_*.asset</c>, UQD-T36), read by <see cref="DuelView"/> through <c>Resources.Load</c>
    /// the same way <see cref="DuelAudio"/> reads its clips. Like <see cref="RenderPipelineBuilder"/>, this
    /// only creates the asset when it is missing (or brings its scaling into line), so rerunning it leaves the
    /// committed asset untouched. Run from the menu or in batch mode via
    /// <c>-executeMethod Unity.Rea.QuickDuel.EditorTools.DuelPanelBuilder.BuildPanel</c>; <see cref="DuelSceneBuilder"/>
    /// runs it first so the scene is never generated without a panel for its UIDocument to use.
    /// Keeps Unity's default runtime theme style sheet (auto-assigned the first time any PanelSettings is
    /// created in a project with none, copying it to Assets/UI Toolkit/): DuelView itself is plain
    /// VisualElement/Label styled by its own Duel.uss and would need no theme, but OnlineDuelMenu's Host /
    /// Join panel (UQD-T21) is built from themed built-in controls (Button, TextField), which look and lay
    /// out wrong -- no default padding, border or background -- without one.
    /// </summary>
    public static class DuelPanelBuilder
    {
        public const string Folder = "Assets/Unity.Rea.QuickDuel/Resources/UI";
        public const string PanelPath = Folder + "/DuelPanelSettings.asset";
        public const string ResourcePath = "UI/DuelPanelSettings";

        [MenuItem("QuickDuel/Build UI Panel Settings")]
        public static void BuildPanel() => Ensure();

        /// <summary>The original's 640 x 480 room at 2.25 panel pixels a pixel (UQD-T50): the frame every scene
        /// draws on (<see cref="OriginalFrame"/>).</summary>
        public static readonly Vector2Int ReferenceResolution = new Vector2Int((int)OriginalFrame.Width, (int)OriginalFrame.Height);

        /// <summary>Creates the panel settings asset if missing, scaling with the screen so the whole 1440 x 1080 frame
        /// fits the window (Expand: the panel is never smaller than the frame either way), the room letterboxed into any window size as the original's kept its aspect
        /// (UQD-T50). An existing asset with another scaling is brought into line; one already right is left
        /// untouched.</summary>
        public static PanelSettings Ensure()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel != null)
            {
                if (panel.scaleMode != PanelScaleMode.ScaleWithScreenSize || panel.referenceResolution != ReferenceResolution
                    || panel.screenMatchMode != PanelScreenMatchMode.Expand)
                {
                    Frame(panel);
                    EditorUtility.SetDirty(panel);
                    AssetDatabase.SaveAssets();
                }
                return panel;
            }

            Directory.CreateDirectory(Folder);
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "DuelPanelSettings";
            Frame(panel);
            panel.clearColor = false;
            // panel.themeStyleSheet is left at whatever PanelSettings.OnEnable already auto-assigned
            // (Unity's own default runtime theme) -- see the class doc for why this panel keeps it.

            AssetDatabase.CreateAsset(panel, PanelPath);
            AssetDatabase.SaveAssets();
            return panel;
        }

        private static void Frame(PanelSettings panel)
        {
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = ReferenceResolution;
            // Expand, not Shrink: Shrink crops the panel to the frame in one direction, so a wide window cut the room's
            // top and bottom off instead of barring its sides.
            panel.screenMatchMode = PanelScreenMatchMode.Expand;
        }
    }
}
