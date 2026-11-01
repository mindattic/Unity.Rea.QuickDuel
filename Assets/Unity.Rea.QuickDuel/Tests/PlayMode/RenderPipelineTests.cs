using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Rea.QuickDuel.Tests
{
    // UQD-T29: the project had no render pipeline package at all (the built-in pipeline, no SRP asset),
    // so this failed before URP was added and assigned. It stays type-agnostic (no reference to the URP
    // package) because the game's own code never names a pipeline either; only RenderPipelineBuilder
    // (Editor) and this check know a pipeline asset exists at all.
    public class RenderPipelineTests
    {
        [Test]
        public void QuickDuelRunsOnARenderPipelineAsset()
        {
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.Not.Null,
                "No render pipeline asset is active; QuickDuel should run on URP, not the built-in pipeline.");
        }

        [Test]
        public void EveryQualityLevelUsesTheSameRenderPipelineAsset()
        {
            var expected = GraphicsSettings.defaultRenderPipeline;
            Assert.That(expected, Is.Not.Null, "GraphicsSettings has no default render pipeline assigned.");

            var current = QualitySettings.GetQualityLevel();
            try
            {
                for (var level = 0; level < QualitySettings.count; level++)
                {
                    QualitySettings.SetQualityLevel(level, false);
                    var perLevel = QualitySettings.renderPipeline;
                    Assert.That(perLevel == null || perLevel == expected, Is.True,
                        $"Quality level {level} ({QualitySettings.names[level]}) runs a different render pipeline asset than the default.");
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(current, false);
            }
        }
    }
}
