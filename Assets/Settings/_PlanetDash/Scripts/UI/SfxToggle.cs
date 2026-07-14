using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Same pattern as MusicToggle.cs, for sound effects — persists via
// PlayerPrefs "SoundEnabled" and mutes AudioManager's sfxSource directly
// so every existing PlaySomething() call needs no changes.
public class SfxToggle : MonoBehaviour
{
    public Button toggleButton;
    public TextMeshProUGUI label;
    public Image icon;
    public Sprite onSprite;
    public Sprite offSprite;

    private bool sfxOn;

    void Start()
    {
        sfxOn = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;
        ApplyToSource();
        UpdateVisual();
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
    }

    void Toggle()
    {
        sfxOn = !sfxOn;
        PlayerPrefs.SetInt("SoundEnabled", sfxOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToSource();
        UpdateVisual();
    }

    void ApplyToSource()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSfxMuted(!sfxOn);
    }

    void UpdateVisual()
    {
        if (label != null)
            label.text = sfxOn ? "SFX: ON" : "SFX: OFF";
        if (icon != null)
            icon.sprite = sfxOn ? onSprite : offSprite;
    }
}
