using System;
using System.IO;
using UnityEngine;

namespace Unity.Rea.QuickDuel.Tests
{
    // Captures the whole screen -- the UI Toolkit panel DuelView draws (UQD-T31: it's a screen-space
    // overlay, not tied to any one camera the way the old uGUI Screen Space - Camera canvas was) plus the
    // main camera's backdrop behind it -- to a PNG in the folder named by QUICKDUEL_SHOTS. Does nothing
    // when the variable is unset or there's no graphics device.
    //
    // ScreenCapture.CaptureScreenshotAsTexture (grab the pixels now, encode and write them synchronously)
    // would be the more correct capture, but it requires calling it only after a `yield return new
    // WaitForEndOfFrame()`, or it throws (confirmed: "failed to generate texture ... Was method called
    // before the 'end of frame' state was reached?", caught by LogAssert and failing the test). And
    // `WaitForEndOfFrame` itself does not complete at all under `-runTests` in this project's headless
    // batch-mode environment (confirmed: every test after it stalls forever) -- there is no Game view for
    // an "end of frame" to mean anything to present. So this stays the original, fire-and-forget
    // `ScreenCapture.CaptureScreenshot`: its file write happens asynchronously at a later end of frame it
    // doesn't wait for either, so a capture can show a later frame than the one at the call site, or (in a
    // true batch-mode run with nothing to present a frame to) not be written at all -- same as before
    // UQD-T31. It is a best-effort, opt-in diagnostic (QUICKDUEL_SHOTS), not asserted on by any test.
    public static class Screenshots
    {
        public static string Capture(string name)
        {
            var dir = Environment.GetEnvironmentVariable("QUICKDUEL_SHOTS");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return null;

            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"QuickDuel screenshot: {path}");
            return path;
        }

        // Writes pixels already read back (a render texture's) to a PNG in the same folder, synchronously.
        public static string Save(string name, Texture2D pixels)
        {
            var dir = Environment.GetEnvironmentVariable("QUICKDUEL_SHOTS");
            if (string.IsNullOrEmpty(dir) || pixels == null)
                return null;

            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Debug.Log($"QuickDuel screenshot: {path}");
            return path;
        }
    }
}
