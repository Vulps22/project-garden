using TMPro;
using UnityEngine;

namespace Vulps.GrowAGarden.Map
{
    /// <summary>The callsite for everything about Somnium's tablet map; forwards to <see cref="TabletMapBridge"/>.</summary>
    public static class MapManager
    {
        /// <summary>The map's content panel under the local player's rig, or null.</summary>
        public static RectTransform FindMapContent(Transform rigRoot) => TabletMapBridge.FindMapContent(rigRoot);

        /// <summary>The font Somnium's map labels use.</summary>
        public static TMP_FontAsset MapFont(RectTransform content) => TabletMapBridge.MapFont(content);

        /// <summary>Somnium's "you" dot sprite.</summary>
        public static Sprite PlayerDotSprite(RectTransform content) => TabletMapBridge.PlayerDotSprite(content);

        /// <summary>Where Somnium has put the local player's dot, in the panel's space.</summary>
        public static Vector3? LocalPlayerMarker(RectTransform content) => TabletMapBridge.LocalPlayerMarker(content);

        /// <summary>The visible window the content panel scrolls behind.</summary>
        public static RectTransform ViewportOf(RectTransform content) => TabletMapBridge.ViewportOf(content);

        /// <summary>Grows the content panel so scrolling is not clamped to Somnium's picture.</summary>
        public static void SetContentSize(RectTransform content, Vector2 size) => TabletMapBridge.SetContentSize(content, size);

        /// <summary>Hides Somnium's mainland map and the local player's dot.</summary>
        public static void HideSomniumContents(RectTransform content) => TabletMapBridge.HideSomniumContents(content);

        /// <summary>Puts Somnium's map back as it was.</summary>
        public static void RestoreSomniumContents() => TabletMapBridge.RestoreSomniumContents();

        /// <summary>Temporary: logs the tablet's structure and what changes in it, given the map's content panel.</summary>
        public static void ProbeTablet(RectTransform content) => TabletProbe.Probe(content);

        /// <summary>Looks everywhere for a tablet, for the 2D one. Developer only.</summary>
        public static void SearchForTablet() => TabletProbe.Search();

        /// <summary>What Somnium has in its other-players list, for the log.</summary>
        public static string DescribePlayersContainer(RectTransform content) => TabletMapBridge.DescribePlayersContainer(content);
    }
}
