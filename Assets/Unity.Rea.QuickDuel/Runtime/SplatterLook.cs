using System;
using UnityEngine;

namespace Unity.Rea.QuickDuel
{
    // One of the original's ten splatters as the Duel scene carries it (UQD-T40): its imported sprite and its bounding box,
    // the pixels a click must land in to wipe it. obj_splatter's mouse events used the sprite's rectangle mask, which
    // GameMaker sizes automatically to the opaque pixels (colkind 1, bboxmode 0); DuelSceneBuilder measures the same box
    // from the image (it matches every bbox_* the original's .sprite.gmx records). Written by the builder, never by hand.
    [Serializable]
    public sealed class SplatterLook
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private RectInt bounds;

        public SplatterLook(Sprite sprite, RectInt bounds)
        {
            this.sprite = sprite;
            this.bounds = bounds;
        }

        public Sprite Sprite => sprite;

        // In the sprite's own pixels, from its top left (as GameMaker's bbox_left and bbox_top), width and height inclusive.
        public RectInt Bounds => bounds;
    }
}
