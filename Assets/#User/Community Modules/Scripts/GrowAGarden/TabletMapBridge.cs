using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Vulps.GrowAGarden
{
    /// <summary>The one place that knows the layout of Somnium's tablet map; every lookup fails quietly back to Somnium's own map.</summary>
    public static class TabletMapBridge
    {
        private const string CONTENT_PATH = "Tablet.XR(Clone)/Canvases/Tablet.XR.Tabs/Tablet.XR.MapTab/ContentView/SomniumMap/MapView/Viewport/Content";

        private static readonly List<GameObject> _hidden = new List<GameObject>();
        private static Image _background;
        private static Color _backgroundColour;
        private static RectTransform _resized;
        private static Vector2 _originalSize;

        /// <summary>The map's scrolling, zooming content panel under the local player's rig, or null when it is not there.</summary>
        public static RectTransform FindMapContent(Transform rigRoot)
        {
            if (rigRoot == null) return null;
            Transform content = rigRoot.Find(CONTENT_PATH);
            if (content == null && rigRoot.parent != null) content = rigRoot.parent.Find("XR.Body/" + CONTENT_PATH);
            return content as RectTransform;
        }

        /// <summary>Where Somnium has put the local player's map dot, in the content panel's space; null when the dot is missing.</summary>
        public static Vector3? LocalPlayerMarker(RectTransform content)
        {
            Transform you = content != null ? content.Find("MapPlayer_You") : null;
            return you != null ? you.localPosition : (Vector3?)null;
        }

        /// <summary>The font Somnium's map labels use, so our labels match and are sure to render in the client.</summary>
        public static TMP_FontAsset MapFont(RectTransform content)
        {
            if (content == null) return null;
            Transform map = content.parent != null && content.parent.parent != null ? content.parent.parent.parent : null;
            TextMeshProUGUI text = map != null ? map.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            return text != null ? text.font : null;
        }

        /// <summary>Somnium's own "you" dot sprite, for drawing our dot in the same style.</summary>
        public static Sprite PlayerDotSprite(RectTransform content)
        {
            if (content == null) return null;
            Transform dot = content.Find("MapPlayer_You/Dot");
            Image image = dot != null ? dot.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        /// <summary>Hides Somnium's mainland picture, landmarks, parcels and the local player's dot, keeping the panel's scroll and zoom.</summary>
        public static void HideSomniumContents(RectTransform content)
        {
            if (content == null) return;

            if (_background == null && content.TryGetComponent(out Image background))
            {
                _background = background;
                _backgroundColour = background.color;
            }
            if (_background != null) _background.color = Color.clear;

            Hide(content.Find("ViewLayers/Landmarks"));
            Hide(content.Find("ViewLayers/Parcels"));
            Hide(content.Find("MapPlayer_You"));
        }

        /// <summary>The visible window the content panel scrolls behind.</summary>
        public static RectTransform ViewportOf(RectTransform content) => content != null ? content.parent as RectTransform : null;

        /// <summary>Grows the content panel so Somnium's scrolling is no longer clamped to its mainland picture.</summary>
        public static void SetContentSize(RectTransform content, Vector2 size)
        {
            if (content == null) return;
            if (_resized == null)
            {
                _resized = content;
                _originalSize = content.sizeDelta;
            }
            if (content.sizeDelta != size) content.sizeDelta = size;
        }

        /// <summary>Puts back everything <see cref="HideSomniumContents"/> hid.</summary>
        public static void RestoreSomniumContents()
        {
            foreach (GameObject go in _hidden)
                if (go != null) go.SetActive(true);
            _hidden.Clear();

            if (_background != null) _background.color = _backgroundColour;
            _background = null;

            if (_resized != null) _resized.sizeDelta = _originalSize;
            _resized = null;
        }

        /// <summary>Names of what Somnium has under its players container, for learning how other players appear.</summary>
        public static string DescribePlayersContainer(RectTransform content)
        {
            Transform players = content != null ? content.Find("MapPointsContainers/PlayersViewContainer") : null;
            if (players == null) return "no PlayersViewContainer";

            var names = new System.Text.StringBuilder();
            foreach (Transform child in players) names.Append(child.name).Append(child.gameObject.activeSelf ? "(on) " : "(off) ");
            return players.childCount + " children: " + names;
        }

        private static void Hide(Transform t)
        {
            if (t == null || !t.gameObject.activeSelf) return;
            t.gameObject.SetActive(false);
            if (!_hidden.Contains(t.gameObject)) _hidden.Add(t.gameObject);
        }
    }
}
