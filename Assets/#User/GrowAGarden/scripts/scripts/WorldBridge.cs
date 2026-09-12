using CommunityModules;
using Fusion;
using Fusion.Addons.Physics;
using System;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Creating, placing, removing and taking control of networked objects — the bridge between
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
        /// How long to wait for Fusion to hand over state authority before giving up.
        ///
        /// One number for the whole world, and it lives here because here is the only layer every
        /// caller can reach. It was four serialized fields on four classes, all set to 2, none of
        /// which had been chosen — each existed only because this method used to demand an
        /// argument.
        /// </summary>
        public const float AuthorityTimeout = 2f;

        /// <summary>The local peer has joined and the scene's network objects are registered.</summary>
        public static bool IsNetworkReady => SceneNetworking.IsNetworkReady;

        /// <summary>Raised at the moment IsNetworkReady becomes true.</summary>
        public static event Action NetworkReady
        {
            add { SceneNetworking.OnLocalPlayerJoined += value; }
            remove { SceneNetworking.OnLocalPlayerJoined -= value; }
        }

        /// <summary>Whether Fusion can create an object right now — see docs/runtime-spawn.md.</summary>
        public static bool CanSpawn
        {
            get
            {
                NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
                return runner != null
                    && runner.IsRunning
                    && runner.LocalPlayer.IsRealPlayer
                    && SceneNetworking.IsNetworkReady;
            }
        }

        /// <summary>The spawned object with this network id, or null.</summary>
        public static NetworkObject Find(uint rawId)
        {
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null || rawId == 0) return null;
            return runner.TryFindObject(new NetworkId { Raw = rawId }, out NetworkObject obj) ? obj : null;
        }

        /// <summary>The component of type T on the object with this network id, or null.</summary>
        public static T Find<T>(uint rawId) where T : Component
        {
            NetworkObject obj = Find(rawId);
            return obj == null ? null : obj.GetComponent<T>();
        }

        /// <summary>
        /// Removes an object only if this client is the one simulating it, and says nothing
        /// either way. Returns whether it acted.
        ///
        /// For the shape where every client runs the same line and exactly one is meant to act —
        /// a plant ending, a produce being sold. <see cref="Despawn"/> is the wrong call there: it
        /// warns when it cannot act, which on that shape is every proxy, every time, correctly.
        /// Named for state authority rather than ownership because they are different questions —
        /// authority stays with whoever last grabbed the object, and OwnerId does not.
        /// </summary>
        public static bool DespawnIfStateAuthority(NetworkObject obj)
        {
            if (obj == null || !obj.HasStateAuthority) return false;

            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null) return false;

            runner.Despawn(obj);
            return true;
        }

        /// <summary>
        /// Requests state authority over an object and waits <see cref="AuthorityTimeout"/>
        /// seconds for it, reporting the outcome through <paramref name="granted"/>.
        ///
        ///     bool ok = false;
        ///     yield return WorldBridge.TakeAuthority(obj, r => ok = r);
        ///     if (!ok) { ...caller's own refusal...; yield break; }
        /// </summary>
        public static IEnumerator TakeAuthority(NetworkObject obj, Action<bool> granted)
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
                while (obj != null && !obj.HasStateAuthority && waited < AuthorityTimeout)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }
            }

            granted?.Invoke(obj != null && obj.HasStateAuthority);
        }
    }
}
