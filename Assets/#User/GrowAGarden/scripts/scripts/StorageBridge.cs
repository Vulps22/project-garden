using SomniumSpace.Bridge;
using System;

namespace GrowAGarden
{
    /// <summary>
    /// Somnium's per-player local storage, behind this game's own vocabulary. This is the only
    /// file allowed to name SomniumBridge.LocalStorage.
    ///
    /// It exists for the same reason WorldBridge and PlayerBridge do — one seam over the SDK, so
    /// SDK churn touches one file rather than every script that remembers something. GameSettings
    /// and TutorialManager each had their own copy of the same try/catch and the same latch, which
    /// is two places to get the failure handling right and two places for it to drift.
    ///
    /// **Nothing here throws.** The read and write halves of LocalStorage are not confirmed
    /// in-world — the SDK's XML documents only the housekeeping calls (JsonExists, DeleteJson,
    /// GetAllJsonNames) and not ReadJson or WriteJson. Both callers reach this from Awake, where an
    /// unguarded throw takes the whole component down: the narrator goes silent, or the settings
    /// object dies and with it the trim, in both cases with no clue why. So a failure here is
    /// logged and swallowed, and the caller carries on with defaults.
    /// </summary>
    public static class StorageBridge
    {
        /// <summary>
        /// Whether storage has thrown yet this session. Latched, never reset.
        ///
        /// **Shared across every caller, where each used to latch for itself.** What throws is the
        /// API rather than any one key, so the first failure is evidence about all of them — and
        /// retrying is how a client log reaches hundreds of megabytes, a line per button press.
        /// </summary>
        public static bool HasFailed { get; private set; }

        /// <summary>Whether anything is stored under this key. False if storage has already failed.</summary>
        public static bool Exists(string key, string context)
        {
            if (HasFailed) return false;

            try
            {
                return SomniumBridge.LocalStorage.JsonExists(key);
            }
            catch (Exception e)
            {
                Fail("Exists", key, context, e);
                return false;
            }
        }

        /// <summary>
        /// What is stored under this key, or null if there is nothing there, storage has already
        /// failed, or this read is the one that fails. Null means "use your defaults" in all four
        /// cases, which is why the caller does not have to tell them apart.
        ///
        /// The new() constraint is the SDK's, not ours — ISomniumWorldsLocalStorage.ReadJson wants
        /// something it can construct before it fills it in.
        /// </summary>
        public static T Read<T>(string key, string context) where T : class, new()
        {
            if (HasFailed) return null;

            try
            {
                return SomniumBridge.LocalStorage.ReadJson<T>(key);
            }
            catch (Exception e)
            {
                Fail("Read", key, context, e);
                return null;
            }
        }

        /// <summary>Stores a value under this key. Returns false if it did not persist.</summary>
        public static bool Write<T>(string key, T value, string context)
        {
            if (HasFailed) return false;

            try
            {
                SomniumBridge.LocalStorage.WriteJson(key, value);
                return true;
            }
            catch (Exception e)
            {
                Fail("Write", key, context, e);
                return false;
            }
        }

        private static void Fail(string op, string key, string context, Exception e)
        {
            HasFailed = true;
            Logger.Error($"{op}() '{context}' — LocalStorage '{key}' failed; nothing will persist this " +
                         $"session: {e.GetType().Name}: {e.Message}");
        }
    }
}
