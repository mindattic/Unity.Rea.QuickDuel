using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Rea.QuickDuel
{
    // One of the original fighters as the Duel scene carries it (UQD-T34): the manifest entry (Data/assets.json, from
    // load_characters.gml) with its sprites resolved to the imported assets. Cosmetic only (BIBLE section 3): a name and
    // art, no stats. Written by DuelSceneBuilder from the manifest, never by hand; DuelView draws the pair the match's
    // fighter roll lands on.
    [Serializable]
    public sealed class FighterLook
    {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite run;
        [SerializeField] private List<Sprite> attack = new List<Sprite>();
        [SerializeField] private List<Sprite> defeat = new List<Sprite>();
        [SerializeField] private Sprite weapon;
        [SerializeField] private Sprite groundWeapon;

        public FighterLook(string id, string name, Sprite idle, Sprite run, IEnumerable<Sprite> attack,
            IEnumerable<Sprite> defeat, Sprite weapon, Sprite groundWeapon)
        {
            this.id = id;
            this.name = name;
            this.idle = idle;
            this.run = run;
            this.attack = new List<Sprite>(attack ?? Array.Empty<Sprite>());
            this.defeat = new List<Sprite>(defeat ?? Array.Empty<Sprite>());
            this.weapon = weapon;
            this.groundWeapon = groundWeapon;
        }

        public string Id => id;
        public string Name => name;
        public Sprite Idle => idle;
        public Sprite Run => run;

        // The four-frame attack and defeat strips, in order.
        public IReadOnlyList<Sprite> Attack => attack;
        public IReadOnlyList<Sprite> Defeat => defeat;

        // The weapon in hand (thrown on defeat) and as it lies on the ground.
        public Sprite Weapon => weapon;
        public Sprite GroundWeapon => groundWeapon;
    }
}
