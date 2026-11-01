using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // Mocks whoever struck before the cue with one line from the old Android build of Bushido Coda, drawn where and as the
    // GameMaker build drew its own "Premature Strike!" (UQD-T39): centred on Get Ready's line, white, in fnt_oldenglish
    // outlined 1, at default_alpha, until the next round. The wording stays the Android list (BIBLE).
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelTaunt : MonoBehaviour
    {
        // The Android build's taunt lines are tuning data (Settings/tuning.json, "taunts").
        public static string[] Lines => TuningStore.Profile.Taunts;

        private DuelController controller;
        private DuelView view;
        private OutlinedText text;

        public string Current => text.style.display != DisplayStyle.None ? text.text : "";

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();
            text = view.CreateLabel("Taunt", OriginalType.Banner, new Vector2(0, DuelView.BannerY));
            text.style.display = DisplayStyle.None;
            controller.RoundStarted += () => text.style.display = DisplayStyle.None;
            controller.Resolved += OnResolved;
            // The loading fade (UQD-T42) clears the aftermath's text under its full image.
            var fade = GetComponent<DuelFade>();
            if (fade != null) fade.BattleReset += () => text.style.display = DisplayStyle.None;
        }

        private void OnResolved(DuelResult result)
        {
            if (!VerdictBanner.ShowsPremature(result))
                return;

            text.text = Lines[Random.Range(0, Lines.Length)];
            text.style.display = DisplayStyle.Flex;
        }
    }
}
