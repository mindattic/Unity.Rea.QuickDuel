using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // The original's type (UQD-T36): the five bitmap fonts load as font assets with every printable glyph, the duel's
    // text is draw_text_outline's outlined text in the font OriginalType names, and the outlined text really draws the
    // original's glyphs, white over a black outline.
    public class OriginalTextTests
    {
        [Test]
        public void EveryOriginalFontLoadsWithItsGlyphs()
        {
            foreach (var id in OriginalType.Fonts)
            {
                var font = OriginalFonts.Load(id);
                Assert.That(font, Is.Not.Null, id);
                Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), id);
                Assert.That(font.atlasTextures, Has.Length.EqualTo(1), id);
                Assert.That(font.atlasTextures[0], Is.Not.Null, id);
                Assert.That(font.atlasTextures[0].filterMode, Is.EqualTo(FilterMode.Point), id + " keeps its pixels sharp");
                Assert.That(font.characterTable, Has.Count.EqualTo(96), id);
                for (var c = ' '; c <= '~'; c++)
                    Assert.That(font.characterLookupTable.ContainsKey(c), Is.True, $"{id} '{c}'");
            }
            Assert.That(OriginalFonts.Load(OriginalType.OldEnglish).faceInfo.lineHeight, Is.EqualTo(63f));
            Assert.That(OriginalFonts.Load(OriginalType.Basic).faceInfo.lineHeight, Is.EqualTo(17f));
        }

        [UnityTest]
        public IEnumerator TheDuelsTextIsTheOriginalsOutlinedType()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            var view = Object.FindFirstObjectByType<DuelView>();
            Assert.That(view, Is.Not.Null);

            AssertStyle(view.Root.Q<OutlinedText>("Countdown"), OriginalType.Countdown);
            AssertStyle(view.Root.Q<OutlinedText>("Headline"), OriginalType.Banner);
            AssertStyle(view.Root.Q<OutlinedText>("Prompt"), OriginalType.Hud);
            AssertStyle(view.Root.Q<OutlinedText>("LeftScore"), OriginalType.Hud);
            AssertStyle(view.Root.Q<OutlinedText>("Taunt"), OriginalType.Banner);
            AssertStyle(view.Root.Q<OutlinedText>("Wins"), OriginalType.Banner);
            AssertStyle(view.Root.Q<OutlinedText>("WinnerName"), OriginalType.Name);

            var prompt = view.Root.Q<OutlinedText>("Prompt");
            Assert.That(prompt.FillLayer.text, Is.EqualTo(view.PromptText));
            Assert.That(prompt.OutlineLayers.All(l => l.text == view.PromptText), Is.True, "every outline copy draws the same text");
            Assert.That(prompt.resolvedStyle.fontSize, Is.EqualTo(17f * OutlinedText.PixelScale).Within(0.01f), "the basic face at its native size");
            Assert.That(prompt.FillLayer.resolvedStyle.color, Is.EqualTo(Color.white), "draw_text_outline fills white");
            Assert.That(prompt.OutlineLayers[0].resolvedStyle.color, Is.EqualTo(Color.black), "over a black outline");
            Assert.That(prompt.FillLayer.resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(OriginalFonts.Load(OriginalType.Basic)));
        }

        private static void AssertStyle(OutlinedText text, TypeStyle style)
        {
            Assert.That(text, Is.Not.Null, style.ToString());
            Assert.That(text.Style, Is.EqualTo(style), text.name);
            var offsets = OriginalType.OutlineOffsets(style.Outline);
            Assert.That(text.OutlineLayers, Has.Count.EqualTo(offsets.Count), text.name);
            for (var i = 0; i < offsets.Count; i++)
            {
                var shift = text.OutlineLayers[i].style.translate.value;
                Assert.That(shift.x.value, Is.EqualTo(offsets[i].X * OutlinedText.PixelScale).Within(0.001f), $"{text.name} copy {i}");
                Assert.That(shift.y.value, Is.EqualTo(offsets[i].Y * OutlinedText.PixelScale).Within(0.001f), $"{text.name} copy {i}");
            }
            Assert.That(text.IndexOf(text.FillLayer), Is.EqualTo(text.childCount - 1), "the fill draws last, over its outline");
            Assert.That(text.resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(OriginalFonts.Load(style.Font)), text.name);
        }

        [UnityTest]
        public IEnumerator OutlinedTextDrawsWhiteGlyphsOverABlackOutline()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device to draw text with.");

            var target = new RenderTexture(512, 256, 0, RenderTextureFormat.ARGB32);
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<PanelSettings>("UI/DuelPanelSettings").themeStyleSheet;
            panel.targetTexture = target;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            var host = new GameObject("OutlinedTextProbe", typeof(UIDocument));
            var document = host.GetComponent<UIDocument>();
            document.panelSettings = panel;
            var text = new OutlinedText(OriginalType.Countdown) { text = "Get Ready" };
            text.style.position = Position.Absolute;
            text.style.left = 8;
            text.style.top = 8;
            text.style.width = 496;
            text.style.height = 240;
            document.rootVisualElement.Add(text);

            for (var i = 0; i < 5; i++) yield return null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;

            int white = 0, black = 0;
            foreach (var p in pixels.GetPixels32())
            {
                if (p.a < 200) continue;
                if (p.r > 230 && p.g > 230 && p.b > 230) white++;
                else if (p.r < 25 && p.g < 25 && p.b < 25) black++;
            }
            Screenshots.Save("outlined-text", pixels);

            Object.Destroy(host);
            Object.Destroy(panel);
            Object.Destroy(pixels);
            target.Release();

            Assert.That(white, Is.GreaterThan(500), "the glyphs are drawn, white");
            Assert.That(black, Is.GreaterThan(500), "around a black outline");
        }
    }
}
