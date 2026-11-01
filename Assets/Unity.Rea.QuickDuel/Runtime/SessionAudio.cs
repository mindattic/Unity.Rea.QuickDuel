using System;
using Unity.Shared.Core;
using UnityEngine;

namespace Unity.Rea.QuickDuel
{
    // Honours the player's shared Unity.Shared profile audio settings for the whole game: the listener plays at the
    // profile's sound-effect volume (every QuickDuel sound is a sound effect; the original has no music), the volume a
    // player keeps the same in every MindAttic game (UQD-T74), and silent when the profile mutes sound effects. That is
    // also how the CLI mute reaches it: launched with -muted or MINDATTIC_CLI_MUTED=1, Unity.Shared's bootstrap turns on
    // its session mute before the first scene loads, and the profile's CommonSettings then report sound effects muted.
    // AudioListener.volume is never saved, and settings.json is never written.
    public static class SessionAudio
    {
        // True once Apply found the profile's sound effects muted (the CLI mute, or the player's own setting).
        public static bool Muted { get; private set; }

        // The listener volume Apply set from the profile; null when no profile could be read (the listener is left as
        // it was).
        public static float? Volume { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnLoad() => Apply();

        // Reads the current profile's audio settings and sets the listener's volume from them. Returns Muted.
        public static bool Apply()
        {
            Volume = ProfileVolume();
            Muted = Volume.HasValue && Volume.Value <= 0f;
            if (Volume.HasValue)
                AudioListener.volume = Volume.Value;
            if (Muted)
                Debug.Log("QuickDuel: audio muted for this session (Unity.Shared profile mute; -muted / MINDATTIC_CLI_MUTED=1).");
            return Muted;
        }

        // The profile's sound-effect volume, 0 when it mutes them; null without a readable profile.
        private static float? ProfileVolume()
        {
            if (!Profiles.IsConfigured)
                return null;
            try
            {
                var common = Profiles.Current.Settings.Common;
                return common.MuteSfx ? 0f : Mathf.Clamp01(common.SfxVolume);
            }
            catch (Exception e)
            {
                // A profile folder the disk refuses must not stop the game; it just plays with sound.
                Debug.LogWarning($"QuickDuel: could not read the profile's audio settings ({e.Message}); sound stays on.");
                return null;
            }
        }
    }
}
