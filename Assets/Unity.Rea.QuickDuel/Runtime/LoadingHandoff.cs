namespace Unity.Rea.QuickDuel
{
    // GameMaker's fade_asset global (UQD-T44): the loading image a room faded in on its way out, which the next room's
    // fade opens under. The intro gives it as it leaves; the scene it goes to takes it, once, as it starts.
    public static class LoadingHandoff
    {
        private static int? image;

        // The image waiting for the next scene (0 to 13 for spr_loading1 to 14), if any.
        public static int? Pending => image;

        public static void Give(int loadingImage) => image = loadingImage;

        public static bool TryTake(out int loadingImage)
        {
            loadingImage = image ?? 0;
            var had = image.HasValue;
            image = null;
            return had;
        }

        public static void Clear() => image = null;
    }
}
