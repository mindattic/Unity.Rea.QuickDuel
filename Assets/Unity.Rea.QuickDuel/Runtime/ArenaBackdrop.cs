using System;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;

namespace Unity.Rea.QuickDuel
{
    // One of the original arenas as the Duel scene carries it (UQD-T33): the manifest entry (Data/assets.json,
    // from load_arenas.gml) with its backdrop sprite resolved to the imported asset. Written by DuelSceneBuilder
    // from the manifest, never by hand; DuelView draws the one the match's arena roll lands on, and DuelAmbient plays its
    // ambient effect (UQD-T49).
    [Serializable]
    public sealed class ArenaBackdrop
    {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite sprite;
        [SerializeField] private string ambient = "none";

        public ArenaBackdrop(string id, string name, string description, Sprite sprite, AmbientKind ambient = AmbientKind.None)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.sprite = sprite;
            this.ambient = AmbientKinds.NameOf(ambient);
        }

        // The arena's ambient effect, by the name the manifest gives it; None for a name it does not know.
        public AmbientKind Ambient => AmbientKinds.TryParse(ambient, out var kind) ? kind : AmbientKind.None;

        public string Id => id;
        public string Name => name;
        public string Description => description;
        public Sprite Sprite => sprite;
    }
}
