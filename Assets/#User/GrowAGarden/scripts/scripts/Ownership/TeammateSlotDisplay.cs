using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// One wall parchment. Purely local and purely visual — shown/hidden/relabeled by
    /// PlotTeammateRoster, never spawned or despawned, never networked. Grabbing it is a plain
    /// XRGrabInteractable, not NetworkGrabbable: carrying it out of the hut is a local gesture
    /// that asks the master to remove a teammate (see the hut-exit revoke trigger), not a
    /// networked object of its own — see plot-ownership.md's "never network the scrolls inside
    /// the shed" rule.
    /// </summary>
    public class TeammateSlotDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _nameText;
        [SerializeField] private XRGrabInteractable _grabInteractable;

        /// <summary>The Plot this slot currently represents, or null while hidden.</summary>
        public PlotLeaseManager Plot { get; private set; }

        /// <summary>The teammate id this slot currently represents, or null while hidden.</summary>
        public string TeammateId { get; private set; }

        public bool IsHeld => _grabInteractable != null && _grabInteractable.isSelected;

        private Vector3 _restPosition;
        private Quaternion _restRotation;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
        }

        public void Show(PlotLeaseManager plot, string teammateId, string displayName)
        {
            Plot = plot;
            TeammateId = teammateId;
            if (_nameText != null) _nameText.text = displayName;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            Plot = null;
            TeammateId = null;
            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
            gameObject.SetActive(false);
        }

        private void OnValidate()
        {
            if (_nameText == null) _nameText = GetComponentInChildren<TextMeshPro>(true);
            if (_grabInteractable == null) _grabInteractable = GetComponentInChildren<XRGrabInteractable>(true);
        }
    }
}
