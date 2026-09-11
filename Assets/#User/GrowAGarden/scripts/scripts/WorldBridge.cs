using Fusion;
using Fusion.Addons.Physics;
using System;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The one crossing point between this game and everything underneath it — Fusion, and the
    /// Somnium SDK's SceneNetworking.
    ///
    /// It exists because the same twenty-five lines were written five times. Plant.SpawnProduce
    /// and PlotStateManager.SpawnPlant were the same function with a different error string, and
    /// the explicit pose write was missing from *both* — a carrot grew perfectly and appeared
    /// nowhere near its plot, and it had to be fixed twice by hand. Taking authority before acting
    /// on an object was written out four times, each with its own _authorityTimeout field.
    ///
    /// The second reason is the one that matters: the SDK is an unstable dependency and should
    /// touch a small surface. Community Modules V2 to V3 forced a re-vendor of everything built on
    /// it, and the ProSDK ships as opaque DLLs, so a deprecation does not arrive as a compile
    /// error — it arrives as a world that does not work.
    ///
    /// This is deliberately not a MonoBehaviour. Nothing here holds state, and making it a
    /// component would mean a serialized reference on every prefab that spawns anything, which is
    /// the coupling this is meant to remove rather than relocate. When these decisions grow tests,
    /// the methods below are the seam to put an interface behind.
    ///
    /// What does NOT belong here: Unity itself. transform, Rigidbody and XRI have decades of
    /// stability behind them, and hiding them buys nothing and costs readability.
    /// </summary>
    public static class WorldBridge
    {
        /// <summary>
        /// Spawns a prefab master-owned, at a pose that actually survives.
        ///
        /// Always SharedModeStateAuthMasterClient, so the client that decides a thing exists is the
        /// client that owns it — which is what closed the gap behind most of the 2026-09-02 bugs,
        /// since broadcastState() self-gates on HasStateAuthority.
        ///
        /// The try/catch is not defensive dressing. Spawning before Fusion has a player index makes
        /// it instantiate the prefab and *then* throw from Simulation.GetNextId(), which leaves an
        /// orphan nothing will ever clean up and — because the throw carries GrowAGarden in its
        /// stack — turns the sun off through ExceptionAlarm. A returned null the caller can handle
        /// is better than a black world.
        ///
        /// <paramref name="context"/> is the caller's own name for the log line, since a failure
        /// here is otherwise anonymous.
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
        /// Puts a freshly spawned object where it is meant to be.
        ///
        /// Two steps, because the pose handed to Runner.Spawn() is used only for the local
        /// instantiation and is not networked at all.
        ///
        /// The second step is the one that took two uploads to find. A kinematic networked
        /// rigidbody is driven *by* its network state, so writing transform.position on it is
        /// overwritten on the next tick and the object reappears wherever its state says — which
        /// for something spawned a moment ago is the origin. Seeds never showed this because a
        /// seed is non-kinematic and physics-driven, so a transform write flows through. Produce is
        /// kinematic until it is harvested, and it was the first thing this game ever spawned that
        /// was. Teleport() is Fusion's own answer: it moves the body *and* its state.
        /// </summary>
        public static void Place(NetworkObject spawned, Vector3 position, Quaternion rotation)
        {
            spawned.transform.SetPositionAndRotation(position, rotation);

            var body = spawned.GetComponent<NetworkRigidbody3D>();
            if (body != null) body.Teleport(position, rotation);
        }

        /// <summary>
        /// Removes an object from the world, and says so when it cannot.
        ///
        /// Fusion's own Despawn does nothing at all without state authority, silently, and that
        /// silence cost real time. An abstraction that inherited it would be worse than no
        /// abstraction — so this one refuses loudly and returns false.
        ///
        /// Take authority first with <see cref="TakeAuthority"/> if the caller might not have it.
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
        /// Asks for state authority over an object and waits for it, up to a timeout.
        ///
        /// Authority is the master's channel for acting on anything it did not spawn — selling,
        /// recalling, despawning a seed that has been planted — and a client cannot simply assume
        /// it, because NetworkGrabbable hands authority to whoever's hand came *near* the object.
        ///
        /// Only the waiting is shared. What to do once granted, and what to say when it is not,
        /// stays with the caller: a sale rejects the seller, a socket leaves the object where it
        /// is, a plot gives up and lets the seed linger. Those are genuinely different answers, so
        /// the result comes back through <paramref name="granted"/> rather than this deciding.
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

                // obj is re-checked every iteration because waiting takes frames and the object can
                // be despawned inside them — a seed sold, a produce binned by the world floor. Once
                // Unity has destroyed it, reading HasStateAuthority throws rather than returning
                // false, so the null test has to come first.
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
