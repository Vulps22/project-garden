using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GrowAGarden
{
    public class Debug_EconomyBoost : MonoBehaviour, IXRSelectFilter
    {
        public static Debug_EconomyBoost Instance;

        [SerializeField] private XRGrabInteractable _grab;
        public bool used = false;

        public bool canProcess => true;

        private void Awake()
        {
            Instance = this;
            _grab.selectEntered.AddListener(OnGrabbed);
        }

        public void OnGrabbed(SelectEnterEventArgs args)
        {
            PlayerBalance player = EconomyManager.Instance.GetLocalPlayer();
            EconomyManager.Instance.AddBalance(player.GetID(), 10000000);
            used = true;
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            return !used;
        }
    }
}
