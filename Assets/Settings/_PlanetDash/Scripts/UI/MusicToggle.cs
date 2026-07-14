using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Persists the music on/off preference across sessions via PlayerPrefs.
// AudioManager reads "MusicEnabled" at Start to decide whether to play
// backgroundMusic in the game scene.
public class MusicToggle : MonoBehaviour
{
    public Button toggleButton;
    public TextMeshProUGUI label;
    // Optional icon swap (sounds-on/sounds-off) — used when the button is
    // icon-only rather than text-labeled.
    public Image icon;
    public Sprite onSprite;
    public Sprite offSprite;
    // Optional — if this scene has its own music source (e.g. the start
    // screen), toggling flips it live instead of only affecting the next
    // scene load.
    public AudioSource musicSource;

    private bool musicOn;

    void Start()
    {
        // AudioManager builds its music AudioSource at runtime (Awake),
        // so it can't be wired as a scene reference in the editor — fall
        // back to it automatically when no explicit source is assigned.
        if (musicSource == null && AudioManager.Instance != null)
            musicSource = AudioManager.Instance.MusicSource;

        musicOn = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        ApplyToSource();
        UpdateVisual();
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
    }

    void Toggle()
    {
        musicOn = !musicOn;
        PlayerPrefs.SetInt("MusicEnabled", musicOn ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToSource();
        UpdateVisual();
    }

    void ApplyToSource()
    {
        if (musicSource == null) return;
        if (musicOn && !musicSource.isPlaying) musicSource.Play();
        else if (!musicOn && musicSource.isPlaying) musicSource.Pause();
    }

    void UpdateVisual()
    {
        if (label != null)
            label.text = musicOn ? "MUSIC: ON" : "MUSIC: OFF";
        if (icon != null)
            icon.sprite = musicOn ? onSprite : offSprite;
    }
}
