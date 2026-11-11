using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>One item in the Developer Market's grid. Tells the app which hand's ray is on it, and lights up while one is.</summary>
    public class MarketCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public int Index;
        public MarketApp App;
        public Image Background;
        public Color Normal;
        public Color Lit;

        private int _rays;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!HandOf(eventData, out bool right)) return;
            _rays++;
            Background.color = Lit;
            App.Hover(right, this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!HandOf(eventData, out bool right)) return;
            _rays = Mathf.Max(0, _rays - 1);
            if (_rays == 0) Background.color = Normal;
            App.Unhover(right, this);
        }

        /// <summary>A 2D player's mouse click: the item is asked for, and put down in front of them.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData is TrackedDeviceEventData) return;   // a VR ray pulls the trigger instead
            App.Click(this);
        }

        private void OnDisable()
        {
            _rays = 0;
            if (Background != null) Background.color = Normal;
        }

        /// <summary>Which hand's ray this is: XRI's UI events carry the interactor.</summary>
        private static bool HandOf(PointerEventData eventData, out bool right)
        {
            right = false;
            if (!(eventData is TrackedDeviceEventData tracked) || !(tracked.interactor is XRBaseInteractor interactor)) return false;
            if (interactor.handedness == InteractorHandedness.Right) { right = true; return true; }
            if (interactor.handedness == InteractorHandedness.Left) return true;
            right = interactor.name.Contains("Right");
            return right || interactor.name.Contains("Left");
        }
    }
}
