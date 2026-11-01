using System;

namespace Unity.Rea.QuickDuel.Core
{
    // Which of the original arenas (AssetManifest.Arenas, from load_arenas.gml) a round is fought on (UQD-T33).
    // The authority (MatchSession locally, OnlineDuelHost online) draws one roll per round and hands it out with
    // the round; every view maps the same roll onto the same arena with Pick, so peers always agree on the backdrop
    // without the rules having to know the arena list.
    //
    // The original rolled once per game, irandom_range(0, 9) in choose_random_arena.gml, over ids where South Ridge
    // was given Sozza's Point's id (5): Sozza's Point could never be drawn and only 10 of the 11 arenas were
    // reachable. That defect is fixed here: every arena in the list is drawn, and a fresh one each round.
    public static class ArenaDraw
    {
        // A fresh roll for a round: any non-negative int.
        public static int Roll(Random random) =>
            (random ?? throw new ArgumentNullException(nameof(random))).Next();

        // The index (0 .. count-1) a roll lands on among `count` arenas; every index is reachable.
        public static int Pick(int roll, int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "There must be at least one arena to pick from.");
            var index = roll % count;
            return index < 0 ? index + count : index;
        }
    }
}
