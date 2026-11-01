using System.IO;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;

namespace Unity.Rea.QuickDuel
{
    // The game's tuning registry as loaded from the build's data file (StreamingAssets/Settings/tuning.json,
    // read by Core's TuningRegistry through a DirectorySettingsSource). Loaded once, on first use; a file that
    // fails validation logs every problem and stops the game from starting rather than playing with a guess.
    // Tests can set Current to their own registry.
    public static class TuningStore
    {
        private static TuningRegistry current;

        public static string Folder => Path.Combine(Application.streamingAssetsPath, TuningRegistry.Folder);

        // The loaded registry (loading it on first use).
        public static TuningRegistry Current
        {
            get => current ?? (current = Load());
            set => current = value;
        }

        // The profile the game plays: the file's default.
        public static TuningProfile Profile => Current.Default;

        public static TuningRegistry Load()
        {
            try
            {
                return TuningRegistry.Load(new DirectorySettingsSource(Folder));
            }
            catch (SettingsException e)
            {
                Debug.LogError(e.Message);
                throw;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear() => current = null;
    }
}
