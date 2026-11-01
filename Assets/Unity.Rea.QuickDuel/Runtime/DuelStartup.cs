using Unity.Rea.QuickDuel.Core;
using UnityEngine;

namespace Unity.Rea.QuickDuel
{
    // The startup before each local round (UQD-T66), as gameclock_startup ran it: the countdown begins by itself
    // DuelTuning.StartupSeconds (startup_time, 3 s) after the scene starts for the first round, and after the round
    // transition's loading image is gone for every later one (RoundStartup); a left click (or a pad's South, the pad's
    // stand-in) during the startup begins it at once (TrySkip, called by DuelInput). It issues the same BeginRound command
    // anything else would, on the local machine's behalf, and decides nothing about the round itself.
    //
    // A finished match never starts another here: the verdict screen's button decides what follows (UQD-T48). Online play
    // has no original; its host still begins each round itself (DuelInput's Space / Start).
    [RequireComponent(typeof(DuelController))]
    public sealed class DuelStartup : MonoBehaviour
    {
        // Whether a Duel scene's startup runs by itself. The PlayMode tests turn it off for the scenes they drive round by
        // round themselves, and back on to test the startup; the game never changes it.
        public static bool RunsByItself { get; set; } = true;

        private DuelController controller;
        private DuelVerdict verdict;

        // The clock reading the scene started at: where the first round's startup counts from (juststarted).
        public double SceneStartedAt { get; private set; }

        // Whether this scene's startup runs by itself; RunsByItself when the scene starts.
        public bool Auto { get; set; }

        // The startup's length, startup_time (tuning).
        public double StartupSeconds => TuningStore.Profile.Duel.StartupSeconds;

        // When the next round's countdown begins by itself, on the controller's clock.
        public double BeginsAt => RoundStartup.BeginsAt(SceneStartedAt, controller.Round > 0 ? controller.EndedAt : null, StartupSeconds);

        // gameclock.mode == "startup": a local round may begin now (by itself once BeginsAt comes, or at once by a click).
        public bool IsRunning
        {
            get
            {
                if (controller.IsOnline || !controller.CanBeginRound) return false;
                if (controller.Match.IsOver || (verdict != null && verdict.Shown)) return false;
                var ended = controller.Round > 0 ? controller.EndedAt : null;
                return RoundStartup.IsRunning(ended, controller.Now);
            }
        }

        private void Awake()
        {
            controller = GetComponent<DuelController>();
            verdict = GetComponent<DuelVerdict>();
            Auto = RunsByItself;
        }

        private void Start() => SceneStartedAt = controller.Now;

        private void Update()
        {
            if (Auto && IsRunning && controller.Now >= BeginsAt)
                controller.BeginRound();
        }

        // obj_game's global left press in the startup: mode = "countdown" at once. False when no startup is running.
        public bool TrySkip() => IsRunning && controller.BeginRound();
    }
}
