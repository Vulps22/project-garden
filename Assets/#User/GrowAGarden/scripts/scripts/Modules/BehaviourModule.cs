using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The bespoke layer. A module sits on top of a produce's standard behaviours and does
    /// something particular to that crop — a fence that fences, a construct that harvests, a
    /// claim that claims — driven by the condition of the produce it is attached to.
    ///
    /// This is the extension point for the rare crop. Most additions are another plant whose
    /// produce is a thing you carry and sell, and those need nothing here. A module lands every
    /// tenth or twentieth crop, and that is the right frequency for something that is genuinely
    /// new behaviour rather than new content.
    ///
    /// The suffix is deliberate and names the layer. `-Entity` is generic infrastructure that
    /// attaches to anything and knows nothing about crops (SellableEntity, ReturnableEntity);
    /// `-Controller` owns a property (KinematicController, AuthorityController); `-Module` is
    /// per-crop and sits on top of both.
    ///
    /// Two rules, and both are the project's rather than this class's:
    ///
    /// **A module reads; it does not own.** It acts on the produce's condition and must not write
    /// anything that already has an owner — isKinematic belongs to KinematicController, HolderId
    /// to the grabber RPC, whether it can be sold to SellableEntity. A module is the most likely
    /// place to break that, because it is the layer written in a hurry for one crop.
    ///
    /// **A decision inside a module is master-only.** Spawning, claiming, changing a balance and
    /// altering what is planted where are facts, and facts come from the master; a module that
    /// decides on every client is four clients deciding separately, which this project has a name
    /// for. There is deliberately no automatic gate here — a module that only changes how
    /// something looks should run everywhere — so the gate goes in the module that needs it.
    /// </summary>
    [RequireComponent(typeof(Produce))]
    public abstract class BehaviourModule : MonoBehaviour
    {
        /// <summary>The produce this module is bolted to. Read its condition; do not drive it.</summary>
        protected Produce Produce { get; private set; }

        /// <summary>True when this client is the one allowed to decide things. See the class note.</summary>
        protected static bool IsMaster => SceneNetworking.IsMasterClient;

        protected virtual void Awake()
        {
            Produce = GetComponent<Produce>();
            if (Produce == null)
            {
                Logger.Error($"Awake() '{gameObject.name}' — {GetType().Name} has no Produce to read; module disabled");
                enabled = false;
                return;
            }

            // Subscribing in the base is the point of the base. LifecycleChanged carries no
            // payload precisely so a subscriber can read whatever it needs from the source, and a
            // module that hand-rolled this would eventually forget to unsubscribe.
            Produce.LifecycleChanged += OnLifecycleChanged;
        }

        protected virtual void OnDestroy()
        {
            if (Produce != null) Produce.LifecycleChanged -= OnLifecycleChanged;
        }

        /// <summary>
        /// The produce changed — it was harvested, held, dropped, sold, ripened. Read what
        /// matters from Produce and act. Called on every client.
        /// </summary>
        protected virtual void OnLifecycleChanged() { }
    }
}
