using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel
{
    // GameMaker's is_singleplayer global (UQD-T45): how the main menu said to play, which the Duel scene takes, once, as
    // it builds its session. New Game leaves a vs-AI mode at the difficulty chosen on the menu, Versus two players. A
    // Duel scene loaded without coming through the menu plays two players, as it always has. The difficulty last chosen
    // is kept for the menu's next visit.
    public static class DuelModeHandoff
    {
        private static DuelMode? mode;

        // The mode waiting for the Duel scene, if any.
        public static DuelMode? Pending => mode;

        // The computer's difficulty the menu last showed; Normal to start with.
        public static AiDifficulty Difficulty { get; set; } = AiDifficulty.Normal;

        public static void Give(DuelMode duelMode) => mode = duelMode;

        // The mode given, or `otherwise` when none was; either way none is left waiting.
        public static DuelMode TakeOr(DuelMode otherwise)
        {
            var taken = mode ?? otherwise;
            mode = null;
            return taken;
        }

        public static void Clear()
        {
            mode = null;
            Difficulty = AiDifficulty.Normal;
        }
    }
}
