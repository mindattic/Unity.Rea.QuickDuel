using System;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The corner arts (UQD-T43), as obj_cornerart_left and obj_cornerart_right drew them: on start each slides in from a
    // whole sprite width off its edge of the screen, closing 1 + 0.05 of the distance left a step, until it is in place
    // at the top corner, 24 original pixels down, and stays there for the rest of the scene (CornerArt). Presentation only.
    //
    // Everything steps at the original's 60 a second counted off the controller's clock, not rendered frames, from the
    // moment the scene starts. The arts are drawn at the original's pixel scale against the real screen edges, the
    // corners of the screen as the original's 640 x 480 room was its screen. They were Draw GUI objects at depth -101, so
    // they go over everything the room drew (the fighters, the blood, the splatters, the flash) and under the letterbox
    // and the loading fade (-10000).
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelCornerArt : MonoBehaviour
    {
        // spr_cornerart_left2 and spr_cornerart_right2. Written by DuelSceneBuilder.
        [SerializeField] private Sprite leftArt;
        [SerializeField] private Sprite rightArt;

        private DuelController controller;
        private DuelView view;
        private VisualElement left;
        private VisualElement right;

        public void SetSprites(Sprite leftSprite, Sprite rightSprite)
        {
            leftArt = leftSprite;
            rightArt = rightSprite;
        }

        public Sprite LeftArt => leftArt;
        public Sprite RightArt => rightArt;

        // The clock reading the slide counts its steps from.
        public double StartedAt { get; private set; }

        // Both arts' room x now (the left one's from the left edge, the right one's from the room's own left edge).
        public double LeftX { get; private set; } = -CornerArt.SpriteWidth;
        public double RightX { get; private set; } = CornerArt.RoomWidth;

        // in_position: both arts are home.
        public bool InPosition { get; private set; }

        // The drawn arts, for tests and screenshots.
        public VisualElement Left => left;
        public VisualElement Right => right;

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();
            StartedAt = controller.Now;

            left = Art("CornerArtLeft", leftArt);
            right = Art("CornerArtRight", rightArt);
            // Over the stage, the splatters and the flash, under the letterbox (and the loading fade, which brings itself
            // to the front).
            var root = view.Root;
            var at = view.LetterboxTop != null && view.LetterboxTop.parent == root ? root.IndexOf(view.LetterboxTop) : root.childCount;
            root.Insert(at, left);
            root.Insert(at + 1, right);

            Advance();
            Draw();
        }

        private static VisualElement Art(string name, Sprite sprite)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.width = CornerArt.SpriteWidth * DuelView.PixelScale;
            element.style.height = CornerArt.SpriteHeight * DuelView.PixelScale;
            // The room's top, 24 original pixels down: the stage is centred on Root, so its top is half the stage up.
            element.style.top = Length.Percent(50);
            element.style.marginTop = -DuelView.StageHeight / 2f + CornerArt.Y * DuelView.PixelScale;
            element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            element.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
            return element;
        }

        private void Update()
        {
            Advance();
            Draw();
        }

        private void Advance()
        {
            var tick = CornerArt.TickAt(controller.Now - StartedAt);
            LeftX = CornerArt.LeftXAt(tick);
            RightX = CornerArt.RightXAt(tick);
            InPosition = CornerArt.InPosition(tick);
        }

        // The left art's left edge LeftX from the screen's left edge; the right art's right edge as far from the screen's
        // right edge as its own right edge (RightX + 152) is from the room's (640).
        private void Draw()
        {
            left.style.left = (float)LeftX * DuelView.PixelScale;
            right.style.right = (float)(CornerArt.RoomWidth - CornerArt.SpriteWidth - RightX) * DuelView.PixelScale;
        }
    }
}
