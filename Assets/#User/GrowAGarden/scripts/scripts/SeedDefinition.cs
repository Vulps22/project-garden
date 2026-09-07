using Fusion;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// One crop's numbers, hardcoded in Init() so balance changes are code changes rather than
    /// Inspector edits. The same definition is referenced by all three of a crop's prefabs — the
    /// seed, the plant and the produce — so there is one place per crop to read and to change.
    ///
    /// The multi-phase `phases` list is gone. Phases existed because one object had to be a seed, a
    /// vine and a fruit in turn; now the plant owns its own growth and the produce owns its own
    /// ripening, and each is a single duration.
    /// </summary>
    public abstract class SeedDefinition : MonoBehaviour
    {
        public string seedId;
        public string displayName;

        [Tooltip("The Seed_ prefab a shop slot spawns for this crop. Must also be listed on the " +
                 "SceneNetworking component, or Fusion has no id to spawn it by.")]
        public NetworkObject seedPrefab;

        [Header("Stocking")]
        [Tooltip("Relative chance of a slot rolling this crop. Higher is more common. Set in " +
                 "Init() with the rest of the balance, not in the Inspector.")]
        public float spawnWeight = 1f;

        [Header("Economy")]
        public int buyPrice;
        public int sellValue;

        [Header("Timing, in seconds")]
        [Tooltip("How long the plant body takes to grow before it bears.")]
        public float growthDuration = 10f;

        [Tooltip("How long the produce takes to ripen once borne. A rooted crop does its growing " +
                 "as a plant, so its produce arrives all but ready.")]
        public float ripenDuration = 1f;

        [Tooltip("How long a bearing plant takes to wither once it is spent. Ignored by rooted " +
                 "plants, which are replaced by their produce the moment they are grown.")]
        public float witherDuration = 30f;

        private void OnEnable()
        {
            Init();
            Logger.Log($"SeedDefinition.OnEnable() '{seedId}' — buy={buyPrice} sell={sellValue} grow={growthDuration}s");
        }

        private void OnValidate() => Init();

        protected abstract void Init();
    }
}
