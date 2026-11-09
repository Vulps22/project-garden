using Fusion;
using UnityEngine;

namespace Vulps.GrowAGarden
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

        [Tooltip("One line under the name on the stall sign: what makes this crop worth buying.")]
        public string tagline;

        [Tooltip("A sentence or two for the crop's Almanac page: what it is and what it does. Set in Init().")]
        public string description;

        [Tooltip("The crop's picture in the Almanac: its seed, as the Developer Market shows it. A texture, not a sprite: " +
                 "sprite references arrive empty in-world.")]
        public Texture2D picture;

        [Tooltip("The Seed_ prefab a shop slot spawns for this crop. Must also be listed on the " +
                 "SceneNetworking component, or Fusion has no id to spawn it by.")]
        public NetworkObject seedPrefab;

        [Tooltip("A voiceover played once to a player who walks up to a buy point holding this seed (sounds/Tips/tip_<seed>). " +
                 "Empty for most crops; only the very special ones have one. Set on the Seed_ prefab, not in Init().")]
        public AudioClip approachVoice;

        [Header("Stocking")]
        [Tooltip("Relative chance of a slot rolling this crop. Higher is more common. Set in " +
                 "Init() with the rest of the balance, not in the Inspector.")]
        public float spawnWeight = 1f;

        [Header("Economy")]
        public int buyPrice;
        public int sellValue;

        [Tooltip("How many times each of a bearing plant's sockets fruits before it is spent; the plant withers once all " +
                 "are. A crop's default: each plant starts from it and may later be rolled otherwise. 1 for rooted crops.")]
        public int harvestCount = 1;

        [Tooltip("How many fruit one plant gives in all, so a fruit's share of the seed's price is known. Set in Init().")]
        public int fruitPerPlant = 1;

        [Header("Upgrade")]
        [Tooltip("For an upgrade seed: the share of profit it adds to what its Plot grows once grown (0.05 is 5%). Set in Init().")]
        public float upgradeBonus;

        [Header("Buff")]
        [Tooltip("For a buffing seed: how many times bigger, and more profitable, a crop grown wholly under it is (docs/economy.md → Crop scale). Set in Init().")]
        public float buffMultiplier = 1f;

        /// <summary>Whether a stall may stock this crop right now. A crop that can only exist once in the world says no while it does.</summary>
        public virtual bool MayBeStocked => true;

        /// <summary>One fruit's profit: what it sells for less its share of the seed's price, never below 0.</summary>
        public float ProfitPerFruit => Mathf.Max(0f, sellValue - (float)buyPrice / Mathf.Max(1, fruitPerPlant));

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
