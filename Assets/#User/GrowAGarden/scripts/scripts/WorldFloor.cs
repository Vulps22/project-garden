using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// Catches the local player when they fall out of the world, puts them back, and lets the
    /// narrator explain what just happened.
    ///
    /// **Why this exists when the uploader already has a respawn height.** The World Uploader's
    /// `RespawnHeightY` is set to -100 and Somnium's own respawn will fire there regardless — but it
    /// is a black box: it gives no callback, no event and no say in what happens, so there is
    /// nothing to hang the tutorial line off and nothing to do to the player's cargo. Ours sits
    /// above theirs, which means ours always wins and theirs stays as the backstop for anything
    /// that gets past it.
    ///
    /// Entirely local. Where the local player is, and what they are told about it, is nobody else's
    /// business — no RPCs, no master decisions. If this later grows the "you lose the seeds you were
    /// carrying" half of the script, *that* part is a fact the master decides and does not belong
    /// in here; see CLAUDE.md's tier table.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldFloor : MonoBehaviour
    {
        [Tooltip("Fall to or below this Y and you are caught. Must stay above the World Uploader's " +
                 "RespawnHeightY (-100) or Somnium gets there first and this never runs.")]
        [SerializeField] private float _floorY = -75f;

        [Tooltip("Where a caught player is put back. Seeded at the world spawn point; move the " +
                 "marker rather than editing numbers if 'back to the garden' means somewhere else.")]
        [SerializeField] private Transform _returnTo;

        [Tooltip("Metres above the floor the player must climb back to before this can fire again. " +
                 "Stops a rescue that somehow lands low from catching itself in a loop.")]
        [SerializeField] private float _rearmMargin = 5f;

        [Tooltip("Seconds between cleanup sweeps for grabbables that have fallen below the floor. " +
                 "Master only. Not per-frame: the sweep enumerates the scene, and nothing down " +
                 "there is going anywhere.")]
        [SerializeField] private float _sweepInterval = 0.5f;

        [Tooltip("Seconds to wait for state authority before giving up on despawning one object. " +
                 "Same shape as PlotStateManager's, and for the same reason — whoever last waved a " +
                 "hand near it may still own it.")]
        [SerializeField] private float _authorityTimeout = 2f;

        /// <summary>
        /// True from crossing the floor until the player is safely back above it. Not just "while
        /// teleporting": the callback can be slow, and a fall at terminal covers a lot of Y in the
        /// meantime, so re-arming on the callback would allow a second rescue mid-rescue.
        /// </summary>
        private bool _caught;

        private void OnValidate()
        {
            // The uploader's own floor is -100. Sitting at or below it would mean Somnium's respawn
            // fires first and nothing here ever runs — silently, because the player *does* get
            // rescued, just not by us.
            if (_floorY <= -100f) _floorY = -75f;
        }

        private float _nextSweepAt;

        private void Update()
        {
            Sweep();

            Transform head = PlayerManager.LocalPlayerHead;
            if (head == null) return;

            float y = head.position.y;

            if (_caught)
            {
                if (y > _floorY + _rearmMargin) _caught = false;
                return;
            }

            if (y > _floorY) return;

            _caught = true;
            Rescue(y);
        }

        /// <summary>
        /// The trash can. Anything grabbable that has fallen past the floor is destroyed, which is
        /// what procgen-islands.md means by the floor doubling as the cleanup mechanism for
        /// everything dropped, thrown or abandoned.
        ///
        /// Master only, because what exists is a fact the master decides — see CLAUDE.md's tier
        /// table. Every other client will see the despawn arrive.
        /// </summary>
        private void Sweep()
        {
            if (!PlayerManager.IsMaster) return;
            if (Time.time < _nextSweepAt) return;
            _nextSweepAt = Time.time + Mathf.Max(0.1f, _sweepInterval);

            var grabbables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < grabbables.Length; i++)
            {
                XRGrabInteractable grab = grabbables[i];
                if (grab == null) continue;
                if (grab.transform.position.y > _floorY) continue;

                // Never take something out of a hand. HolderId is the replicated answer to "is
                // anyone holding this" — isSelected would be true on exactly one machine and this
                // sweep runs on the master, which is usually not that machine. A held object below
                // the floor is somebody mid-rescue; their own WorldFloor lets go of it first, and
                // it becomes eligible the moment they do.
                var held = grab.GetComponent<IHeldObject>();
                if (held != null && !string.IsNullOrEmpty(held.HolderId)) continue;

                StartCoroutine(Discard(grab.gameObject));
            }
        }

        /// <summary>
        /// Authority first, then despawn. A dropped object is usually master-owned already —
        /// AuthorityController hands unheld world objects back — but "usually" is not "always", and
        /// Despawn without authority does nothing at all. Lifted from
        /// PlotStateManager.TakeOwnershipAndDespawn, which learned this the same way.
        /// </summary>
        private IEnumerator Discard(GameObject go)
        {
            if (go == null) yield break;

            // Stop it being grabbed on the way out. A player reaching into the void for something
            // that is about to stop existing is the one case that would strand a hand holding a
            // destroyed object.
            new XRGrabInteractableRef(go).SetEnabled(false);

            NetworkObject obj = go.GetComponent<NetworkObject>();
            if (obj == null)
            {
                // Not networked, so nothing to agree about.
                Logger.Info($"Discard() '{go.name}' — below the floor and not networked; destroyed locally");
                Destroy(go);
                yield break;
            }

            bool granted = false;
            yield return WorldBridge.TakeAuthority(obj, _authorityTimeout, r => granted = r);

            if (obj == null) yield break;

            if (!granted)
            {
                Logger.Warn($"Discard() '{go.name}' — no authority within {_authorityTimeout}s; " +
                            $"it stays below the floor and the next sweep will try again");
                new XRGrabInteractableRef(go).SetEnabled(true);
                yield break;
            }

            Logger.Info($"Discard() '{go.name}' — fell past the floor at y={go.transform.position.y:F1}, despawned");
            WorldBridge.Despawn(obj, $"Discard() '{go.name}'");
        }

        /// <summary>
        /// Lets go of whatever this player is carrying, so it is left behind at the bottom of the
        /// world instead of riding the teleport home.
        ///
        /// This is the whole of the script's "teleported right back to the garden without any of
        /// the seeds you found along the way" — Somnium's own respawn moves the player and nothing
        /// else, so losing the cargo only happens because of this.
        ///
        /// isSelected is the right question here despite CLAUDE.md's rule against local state
        /// driving networked decisions, and XRGrabInteractableRef says why: it is true only on the
        /// machine whose hand is actually on the object, and that machine is the one entitled to
        /// say "I am putting this down". The drop then travels as a grabber RPC like any other.
        /// </summary>
        private void DropEverythingHeld()
        {
            var grabbables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            int dropped = 0;

            for (int i = 0; i < grabbables.Length; i++)
            {
                XRGrabInteractable grab = grabbables[i];
                if (grab == null || !grab.isSelected) continue;

                StartCoroutine(ReleaseThenRearm(grab));
                dropped++;
            }

            if (dropped > 0)
                Logger.Info($"DropEverythingHeld() '{gameObject.name}' — let go of {dropped} held object(s) below the floor");
        }

        /// <summary>
        /// Disabling the interactable is what forces the deselect; re-enabling a frame later leaves
        /// the object grabbable again. That matters for the case where the sweep never gets to it —
        /// a permanently ungrabbable object lost in the void is a worse outcome than a grabbable
        /// one, and this way the failure is only that the seed survives.
        /// </summary>
        private static IEnumerator ReleaseThenRearm(XRGrabInteractable grab)
        {
            grab.enabled = false;
            yield return null;
            if (grab != null) grab.enabled = true;
        }

        private void Rescue(float fellTo)
        {
            if (_returnTo == null)
            {
                Logger.Error($"Rescue() '{gameObject.name}' — no return point set; the player is below " +
                             $"the floor at y={fellTo:F1} and Somnium's respawn is now the only thing " +
                             $"that will save them");
                return;
            }

            if (!PlayerManager.CanDriveLocalPlayer)
            {
                Logger.Error($"Rescue() '{gameObject.name}' — no Motion feature; cannot teleport from y={fellTo:F1}");
                return;
            }

            Vector3 target = _returnTo.position;
            Vector3 direction = _returnTo.eulerAngles;

            Logger.Info($"Rescue() '{gameObject.name}' — player fell to y={fellTo:F1}, returning to {target}");

            // Before the teleport, not after. XRI keeps a held object with the hand, so a seed
            // still selected when the player moves arrives in the garden with them — which is the
            // opposite of the rule, and would quietly make falling free.
            DropEverythingHeld();

            // The tip goes in the callback, not on the next line. DoTeleportToPoint is asynchronous
            // — that is what the 'done' parameter is for — and starting 20 seconds of narration
            // about having been teleported while the teleport is still in flight gets the order
            // backwards for no reason, when the API hands us the right moment for free.
            PlayerManager.TeleportLocalPlayer(target, direction, () =>
            {
                Logger.Info($"Rescue() '{gameObject.name}' — teleport complete, playing the falling tip");
                TutorialManager.GetInstance()?.TriggerTooltip(TutorialManager.Tooltips.Tips.Falling);
            });
        }
    }
}
