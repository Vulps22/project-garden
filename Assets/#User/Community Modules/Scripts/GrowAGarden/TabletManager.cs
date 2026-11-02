using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vulps.GrowAGarden
{
    /// <summary>The one caller of <see cref="TabletBridge"/>: what our code may do with Somnium's tablet. See docs/tablet.md.</summary>
    public static class TabletManager
    {
        public static Transform FindTablet(Transform rigRoot) => TabletBridge.FindTablet(rigRoot);
        public static Button AddHomeButton(Transform tablet, string name, Sprite icon, string label) => TabletBridge.AddHomeButton(tablet, name, icon, label);
        public static RectTransform AddPage(Transform tablet, string name) => TabletBridge.AddPage(tablet, name);
        public static void ShowHome(Transform tablet, bool show) => TabletBridge.ShowHome(tablet, show);
        public static bool SomniumPageOpen(Transform tablet, Transform ours) => TabletBridge.SomniumPageOpen(tablet, ours);
        public static TMP_FontAsset Font(Transform tablet) => TabletBridge.Font(tablet);
        public static Sprite PanelSprite(Transform tablet) => TabletBridge.PanelSprite(tablet);
        public static Button[] SettingsSections(Transform tablet) => TabletBridge.SettingsSections(tablet);
        public static Button AddSettingsSection(Transform tablet, string name, string label, Texture icon) => TabletBridge.AddSettingsSection(tablet, name, label, icon);
        public static void SetSelectorHighlight(Button section, bool on) => TabletBridge.SetSelectorHighlight(section, on);
        public static RectTransform AddSettingsPage(Transform tablet, string name) => TabletBridge.AddSettingsPage(tablet, name);
        public static void ShowSettingsSection(Transform tablet, Transform ourPanel, Button lit) => TabletBridge.ShowSettingsSection(tablet, ourPanel, lit);
        public static bool SettingsSectionShowing(Transform tablet, Button section) => TabletBridge.SettingsSectionShowing(tablet, section);
        public static Sprite SettingsSectionSprite(Transform tablet) => TabletBridge.SettingsSectionSprite(tablet);
        public static Toggle AddSettingsToggle(Transform tablet, Transform parent, string name, string label) => TabletBridge.AddSettingsToggle(tablet, parent, name, label);
        public static void ShowToggle(Toggle toggle, bool on, bool usable) => TabletBridge.ShowToggle(toggle, on, usable);
    }
}
