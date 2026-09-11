using Fusion;
using Fusion.Addons.Physics;
using System;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Creating, placing, removing and taking control of networked objects — the gateway between
    /// this game and Fusion for anything that exists in the shared world.
    ///
    /// Player identity, session state and messaging are not here.
    /// </summary>
    public static class WorldBridge
    {
        /// <summary>
        /// Spawns a registered prefab master-owned and places it. Returns null on any failure,
        /// having logged it against <paramref name="context"/>.
        ///
        /// Spawning before Fusion has a player index instantiates the prefab and then throws,
        /// leaving an orphan and blacking the world out through ExceptionAlarm — hence the catch.
        /// </summary>
        public static NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, string context)
        {
            if (prefab == null)
            {
                Logger.Error($"Spawn() '{context}' — no prefab given");
                return null;
            }

            SceneNetworking net = SceneNetworking.Instance;
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (net == null || runner == null) return null;

            if (!net.NetworkPrefabs.TryGetValue(prefab, out NetworkPrefabId prefabId))
            {
                Logger.Error($"Spawn() '{context}' — '{prefab.name}' is not registered on SceneNetworking");
                return null;
            }

            NetworkObject spawned;
            try
            {
                spawned = runner.Spawn(prefabId, position, rotation, null, null,
                                       NetworkSpawnFlags.SharedModeStateAuthMasterClient);
            }
            catch (Exception e)
            {
                Logger.Error($"Spawn() '{context}' — Fusion threw spawning '{prefab.name}': {e.Message}");
                return null;
            }

            if (spawned == null) return null;

            Place(spawned, position, rotation);
            return spawned;
        }

        /// <summary>
        /// Moves a spawned object to a pose, in both the transform and its network state.
        ///
        /// Runner.Spawn's pose is local-only and never replicated, and a kinematic
        /// NetworkRigidbody3D is driven by its network state — so a transform write alone is
        /// overwritten on the next tick and the object snaps back to the origin. Teleport moves
        /// both.
        /// </summary>
        public static void Place(NetworkObject spawned, Vector3 position, Quaternion rotation)
        {
            spawned.transform.SetPositionAndRotation(position, rotation);

            var body = spawned.GetComponent<NetworkRigidbody3D>();
            if (body != null) body.Teleport(position, rotation);
        }

        /// <summary>
        /// Removes an object from the world. Returns false and warns if it could not — Fusion's own
        /// Despawn does nothing without state authority and says nothing about it.
        ///
        /// For callers that may not hold authority, <see cref="TakeAuthority"/> first. For code
        /// that runs on every client and expects only one to act, test HasStateAuthority instead:
        /// this warns, and would warn on every peer.
        /// </summary>
        public static bool Despawn(NetworkObject obj, string context)
        {
            if (obj == null) return false;

            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null)
            {
                Logger.Warn($"Despawn() '{context}' — no runner; '{obj.name}' left standing");
                return false;
            }

            if (!obj.HasStateAuthority)
            {
                Logger.Warn($"Despawn() '{context}' — no state authority over '{obj.name}'; Fusion would have ignored this silently");
                return false;
            }

            runner.Despawn(obj);
            return true;
        }

        /// <summary>
        /// Requests state authority over an object and waits up to <paramref name="timeout"/>
        /// seconds for it, reporting the outcome through <paramref name="granted"/>.
        ///
        ///     bool ok = false;
        ///     yield return WorldBridge.TakeAuthority(obj, _authorityTimeout, r => ok = r);
        ///     if (!ok) { ...caller's own refusal...; yield break; }
        /// </summary>
        public static IEnumerator TakeAuthority(NetworkObject obj, float timeout, Action<bool> granted)
        {
            if (obj == null)
            {
                granted?.Invoke(false);
                yield break;
            }

            if (!obj.HasStateAuthority)
            {
                obj.RequestStateAuthority();

                // Null-tested first each pass: the object can be despawned while we wait, and
                // reading HasStateAuthority on a destroyed one throws rather than returning false.
                float waited = 0f;
                while (obj != null && !obj.HasStateAuthority && waited < timeout)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }
            }

            granted?.Invoke(obj != null && obj.HasStateAuthority);
        }
    }
}
