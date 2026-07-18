using UnityEngine;
using UnityEngine.UI;

// Main-menu settings popup — same open/close pattern as AchievementsUI's
// panel. Replaces the two standalone always-on-screen Music/SFX toggle
// buttons with a single gear button that opens a panel containing both
// (the toggles themselves are unchanged MusicToggle/SfxToggle components,
// just reparented under this panel instead of sitting loose on the HUD).
public class SettingsPanel : MonoBehaviour
{
    public GameObject panel;
    public Button openButton;
    public Button closeButton;

    void Start()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (panel != null) panel.SetActive(false);
    }

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }
}
