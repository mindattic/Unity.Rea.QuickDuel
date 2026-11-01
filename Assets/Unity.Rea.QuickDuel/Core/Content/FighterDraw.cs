using System;

namespace Unity.Rea.QuickDuel.Core
{
    // Which of the original fighters (AssetManifest.Fighters, from load_characters.gml) stands in each seat (UQD-T34).
    // Fighters are cosmetic (BIBLE section 3): nothing about a round depends on them. The authority (MatchSession
    // locally, OnlineDuelHost online) draws one roll per match and hands it out with every round; every view maps the
    // same roll onto the same pair with Pick, so peers always agree on who is on the field without the rules having to
    // know the fighter list.
    //
    // One roll carries both seats: the left seat is the roll's lowest "digit" in base `count`, the right seat the next
    // one, so each seat is drawn independently and uniformly and both can be the same fighter (then they are tinted red
    // and blue, FighterLineup.Tint). The original had a character select for the player and choose_opponent's random
    // pick for the computer; both screens are dropped, so both seats are drawn.
    public static class FighterDraw
    {
        // A fresh roll for a match: any non-negative int.
        public static int Roll(Random random) =>
            (random ?? throw new ArgumentNullException(nameof(random))).Next();

        // The index (0 .. count-1) of the fighter `roll` puts in `side`'s seat among `count` fighters. Every pair,
        // the same fighter twice included, is reachable; a negative roll is read as its unsigned bits.
        public static int Pick(int roll, Side side, int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "There must be at least one fighter to pick from.");
            long value = (uint)roll;
            if (side == Side.Right)
                value /= count;
            return (int)(value % count);
        }

        // Whether `roll` puts the same fighter in both seats.
        public static bool SameFighter(int roll, int count) =>
            Pick(roll, Side.Left, count) == Pick(roll, Side.Right, count);
    }
}
