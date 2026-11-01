using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.Rea.QuickDuel.EditorTools
{
    /// <summary>
    /// Builds QuickDuel's render pipeline (UQD-T29): the project had no render pipeline package at all
    /// (the default built-in pipeline, no URP in <c>Packages/manifest.json</c>), the one standards gap
    /// AGENTS.md rule 6 calls for ("Modern Unity only ... URP behind a pipeline-agnostic layer"). The
    /// whole game is UI (<see cref="DuelView"/> builds its UI Toolkit tree in code, UQD-T31) over a plain
    /// camera backdrop; neither UI Toolkit's own shaders nor the camera's clear colour are tied to URP,
    /// so no game shader or material in the repo names it; this creates <see cref="PipelinePath"/> with its renderer
    /// (<see cref="RendererPath"/>) and assigns it as the default pipeline and on every quality level.
    /// It only creates what is missing and only assigns what differs, so rerunning it (and every import
    /// after it) leaves the committed assets and settings untouched. Run from the menu or in batch mode
    /// via <c>-executeMethod Unity.Rea.QuickDuel.EditorTools.RenderPipelineBuilder.BuildPipeline</c>;
    /// <see cref="DuelSceneBuilder"/> runs it first so the scene is never generated without a pipeline.
    /// </summary>
    public static class RenderPipelineBuilder
    {
        public const string Folder = "Assets/Unity.Rea.QuickDuel/Rendering";
        public const string PipelinePath = Folder + "/QuickDuelURP.asset";
        public const string RendererPath = Folder + "/QuickDuelURPRenderer.asset";
        private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("QuickDuel/Build Render Pipeline")]
        public static void BuildPipeline() => Ensure();

        /// <summary>Creates the pipeline and renderer if missing and makes them the active pipeline everywhere.</summary>
        public static UniversalRenderPipelineAsset Ensure()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = CreatePipeline();
            }

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
            }

            // URP writes its own MSAA count into the active quality level whenever it renders, so a
            // level whose antiAliasing differs is rewritten by every play or render; matching them
            // here keeps QualitySettings stable (the pipeline's 1 sample is antiAliasing 0).
            var antiAliasing = pipeline.msaaSampleCount > 1 ? pipeline.msaaSampleCount : 0;
            var current = QualitySettings.GetQualityLevel();
            for (var level = 0; level < QualitySettings.count; level++)
            {
                // QualitySettings only exposes the active level for writing.
                QualitySettings.SetQualityLevel(level, false);
                if (QualitySettings.renderPipeline != pipeline)
                {
                    QualitySettings.renderPipeline = pipeline;
                }

                if (QualitySettings.antiAliasing != antiAliasing)
                {
                    QualitySettings.antiAliasing = antiAliasing;
                }
            }

            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        private static UniversalRenderPipelineAsset CreatePipeline()
        {
            Directory.CreateDirectory(Folder);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                // As URP's own "URP Asset (with Universal Renderer)" create menu does: the default
                // post-process data, then the package's shaders and materials filled into every null
                // resource slot. QuickDuel has no sprites or 2D lights (its whole screen is UI Toolkit
                // over a camera backdrop), so the plain forward Universal Renderer is used, not the 2D renderer.
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                var serialized = new SerializedObject(renderer);
                serialized.FindProperty("postProcessData").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(renderer);
            }

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            AssetDatabase.SaveAssets();
            return pipeline;
        }
    }
}
