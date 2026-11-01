using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Rea.QuickDuel
{
    // One sprite an ambient effect draws (UQD-T49), as the Duel scene carries it: its GameMaker name, its imported frames
    // in order, its size and its origin in the original's pixels (from the top left, as xorig / yorigin), the point it is
    // drawn, turned and mirrored about. Written by DuelSceneBuilder from the asset manifest, never by hand.
    [Serializable]
    public sealed class AmbientLook
    {
        [SerializeField] private string id;
        [SerializeField] private List<Sprite> frames = new List<Sprite>();
        [SerializeField] private Vector2 size;
        [SerializeField] private Vector2 origin;

        public AmbientLook(string id, IEnumerable<Sprite> frames, Vector2 size, Vector2 origin)
        {
            this.id = id;
            this.frames = new List<Sprite>(frames ?? throw new ArgumentNullException(nameof(frames)));
            this.size = size;
            this.origin = origin;
        }

        public string Id => id;
        public IReadOnlyList<Sprite> Frames => frames;
        public Vector2 Size => size;
        public Vector2 Origin => origin;
    }
}
