using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The original's 640 x 480 room as the frame of every scene (UQD-T50). The original's window kept the room's aspect
    // whatever its size (Global Game Settings: option_scale -1, keep aspect ratio; option_sizeable True), so the rebuild
    // draws each scene's tree on a fixed 1440 x 1080 frame (the room at 2.25 panel pixels a pixel) letterboxed into the
    // window, black around it, at any window size or shape.
    //
    // The panel (Resources/UI/DuelPanelSettings, built by DuelPanelBuilder) scales with the screen to fit 1440 x 1080
    // inside it (ScaleWithScreenSize, Expand), so the panel is always at least the frame in both directions and exactly
    // it in one; Fit pins a document's root to the frame, centred, clipping what strays past its edges, and paints the
    // rest of the panel black.
    public static class OriginalFrame
    {
        public const int RoomWidth = 640;
        public const int RoomHeight = 480;

        // Panel pixels an original pixel takes.
        public const float PixelScale = OutlinedText.PixelScale;

        public const float Width = RoomWidth * PixelScale;
        public const float Height = RoomHeight * PixelScale;

        // What shows around the frame.
        public static readonly Color Bars = Color.black;

        // Pins `root` (a UIDocument's root element) to the frame, centred on the panel, and blacks out the panel round it.
        public static void Fit(VisualElement root)
        {
            root.style.position = Position.Absolute;
            root.style.width = Width;
            root.style.height = Height;
            root.style.left = Length.Percent(50);
            root.style.top = Length.Percent(50);
            root.style.right = StyleKeyword.Auto;
            root.style.bottom = StyleKeyword.Auto;
            root.style.marginLeft = -Width / 2f;
            root.style.marginTop = -Height / 2f;
            root.style.overflow = Overflow.Hidden;
            if (root.panel != null)
                PaintBars(root.panel);
            root.RegisterCallback<AttachToPanelEvent>(e => PaintBars(e.destinationPanel));
        }

        private static void PaintBars(IPanel panel)
        {
            if (panel?.visualTree != null)
                panel.visualTree.style.backgroundColor = Bars;
        }
    }
}
