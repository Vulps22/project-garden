using Fusion;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Simple component to instantiate network prefab
    /// </summary>
    public class NetworkSpawn : MonoBehaviour
    {
        [Header("Version [2026, 05, 01]")]
        [Tooltip("The prefab must be listed in the SceneNetworking component")]
        [SerializeField] NetworkObject _networkPrefab;
        [SerializeField] private Transform _targetLocation;

        /// <summary>
        /// Spawn an instance of the network prefab
        /// - The network prefab must be listed in the SceneNetworking component
        ///
        /// Every step reports, because the original said nothing at all: a spawn that worked and a
        /// click that never arrived produced an identical (empty) client log, and the only way to
        /// tell them apart was to look for the object in-world. The button exists to be diagnosed
        /// with, so it is worth more logging than a shipping path would carry.
        /// </summary>
        public void Spawn()
        {
            Logger.Info($"Spawn() '{gameObject.name}' — clicked; prefab='{(_networkPrefab == null ? "<none>" : _networkPrefab.name)}' target='{(_targetLocation == null ? "<none>" : _targetLocation.name)}'");

            if (_networkPrefab == null)
            {
                Logger.Error($"Spawn() '{gameObject.name}' — no _networkPrefab assigned; nothing to spawn");
                return;
            }

            if (_targetLocation == null)
            {
                Logger.Error($"Spawn() '{gameObject.name}' — no _targetLocation assigned; nowhere to spawn '{_networkPrefab.name}'");
                return;
            }

            SceneNetworking net = SceneNetworking.Instance;
            if (net == null)
            {
                Logger.Error($"Spawn() '{gameObject.name}' — SceneNetworking.Instance is null; the session has not built the prefab table yet");
                return;
            }

            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null)
            {
                Logger.Error($"Spawn() '{gameObject.name}' — no NetworkRunner; not in a session");
                return;
            }

            // Asked and reported rather than gated on, so a click made too early still shows up as
            // a Fusion throw below with these values beside it. Runtime-spawn §4b is why they are
            // worth printing at all: IsRunning false or IsRealPlayer false is the state in which
            // Runner.Spawn() instantiates the prefab and *then* throws out of GetNextId(),
            // orphaning a GameObject that nothing will ever clean up.
            Logger.Log($"Spawn() '{gameObject.name}' — runner.IsRunning={runner.IsRunning} LocalPlayer.IsRealPlayer={runner.LocalPlayer.IsRealPlayer} IsMasterClient={SceneNetworking.IsMasterClient}");

            if (!net.NetworkPrefabs.TryGetValue(_networkPrefab, out NetworkPrefabId prefabId))
            {
                Logger.Error($"Spawn() '{gameObject.name}' — '{_networkPrefab.name}' is not registered on SceneNetworking; add it to _networkPrefabs");
                return;
            }

            Vector3 position = _targetLocation.position;
            NetworkObject spawned;
            try
            {
                spawned = runner.Spawn(prefabId, position);
            }
            catch (System.Exception e)
            {
                // Caught for the same reason BuyPoint.SpawnFreshSeed() catches: ExceptionAlarm
                // blacks the world out on any exception from this assembly, and a test button that
                // kills the lights is a worse diagnostic than one that says what went wrong.
                Logger.Error($"Spawn() '{gameObject.name}' — Fusion threw spawning '{_networkPrefab.name}': {e.Message}");
                return;
            }

            if (spawned == null)
            {
                Logger.Error($"Spawn() '{gameObject.name}' — Fusion refused to spawn '{_networkPrefab.name}' at {position}");
                return;
            }

            Logger.Info($"Spawn() '{gameObject.name}' — spawned '{spawned.name}' id={spawned.Id} authority={spawned.HasStateAuthority} at {spawned.transform.position} (asked for {position})");
        }

    }
}
