using Fusion;
using UnityEngine;
using Vulps.GrowAGarden.Interaction;

namespace Vulps.GrowAGarden.Modules
{
    /// <summary>
    /// A weed bloom leaves a splif behind when it is sold, on the ground in front of the sell point.
    /// Selling itself is untouched: this only listens for the sale. See docs/weed.md.
    /// </summary>
    [RequireComponent(typeof(SellableEntity))]
    public class BloomModule : BehaviourModule
    {
        [Tooltip("What a sold bloom leaves behind. Must be on SceneNetworking's prefab list.")]
        [SerializeField] private NetworkObject _splifPrefab;

        private SellableEntity _sellable;

        protected override void Awake()
        {
            base.Awake();
            _sellable = GetComponent<SellableEntity>();
            if (_sellable != null) _sellable.Sold += OnSold;
        }

        protected override void OnDestroy()
        {
            if (_sellable != null) _sellable.Sold -= OnSold;
            base.OnDestroy();
        }

        /// <summary>Every client hears the sale; only the master spawns, because a spawn is a fact.</summary>
        private void OnSold()
        {
            if (!IsMaster) return;
            if (_splifPrefab == null)
            {
                Logger.Warn($"OnSold() '{gameObject.name}' — no splif prefab set; nothing left behind");
                return;
            }
            if (!WorldManager.CanSpawn)
            {
                Logger.Warn($"OnSold() '{gameObject.name}' — cannot spawn yet; no splif left behind");
                return;
            }

            Transform at = _sellable.GivenBackAt;
            Vector3 position = at != null ? at.position : transform.position;
            Quaternion rotation = at != null ? at.rotation : Quaternion.identity;
            NetworkObject splif = WorldManager.Spawn(_splifPrefab, position, rotation, "splif from a sold bloom");
            Logger.Info($"OnSold() '{gameObject.name}' — left '{(splif != null ? splif.name : "nothing")}' at {position}{(at == null ? " (the sell point gave no drop point)" : "")}");
        }
    }
}
