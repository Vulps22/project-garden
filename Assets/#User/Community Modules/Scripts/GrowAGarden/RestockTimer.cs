using UnityEngine;
using UnityEngine.UI;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden
{
    /// <summary>A button on the tablet's sidebar, under World Spawn, counting down to the next stall restock. See docs/shop.md.</summary>
    public class RestockTimer : MonoBehaviour
    {
        private const string BUTTON_NAME = "Tablet.XR.QuickActionBtn - Restock (Grow A Garden)";

        [Tooltip("The button's icon (Textures/Restock.png).")]
        [SerializeField] private Texture2D _icon;

        private Transform _tablet;
        private Button _button;
        private float _nextLookup;
        private float _nextTabletCheck;
        private int _shown = int.MinValue;

        private void Update()
        {
            if (_button == null || TabletReplaced()) { _button = null; TryBuild(); return; }

            float seconds = WorldManager.SecondsToRestock;
            int whole = seconds < 0f ? -1 : Mathf.CeilToInt(seconds);
            if (whole == _shown) return;
            _shown = whole;
            TabletManager.SetQuickActionLabel(_button, whole < 0 ? "Restock --:--" : $"Restock {whole / 60}:{whole % 60:00}");
        }

        private void TryBuild()
        {
            if (Time.time < _nextLookup) return;
            _nextLookup = Time.time + 2f;
            if (PlayerManager.LocalPlayerRig.Root == null) return;

            _tablet = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
            if (_tablet == null) return;

            Sprite icon = _icon == null ? null : Sprite.Create(_icon, new Rect(0, 0, _icon.width, _icon.height), new Vector2(0.5f, 0.5f));
            _button = TabletManager.AddQuickAction(_tablet, BUTTON_NAME, icon, "Restock --:--");
            if (_button == null)
            {
                Logger.Warn($"TryBuild() '{gameObject.name}' — the tablet's sidebar is not where docs/tablet.md says (or not built yet); trying again");
                _nextLookup = Time.time + 5f;
                return;
            }
            _shown = int.MinValue;
            Logger.Info($"TryBuild() '{gameObject.name}' — restock timer on the tablet's sidebar");
        }

        /// <summary>Somnium made a new tablet (an instance change can): the old one's build is no longer what the player holds.</summary>
        private bool TabletReplaced()
        {
            if (Time.time < _nextTabletCheck) return false;
            _nextTabletCheck = Time.time + 2f;
            Transform now = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
            if (now == null || now == _tablet) return false;
            Logger.Info($"TabletReplaced() '{gameObject.name}' — a new tablet; building again");
            return true;
        }
    }
}
