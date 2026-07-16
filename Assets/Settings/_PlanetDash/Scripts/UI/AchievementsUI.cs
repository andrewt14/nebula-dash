using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Minimal read-only achievements viewer for the start screen. Loads the
// same Resources/Achievements assets AchievementManager tracks in the
// game scene and shows locked/unlocked state from the shared PlayerPrefs
// keys — no separate progress tracking here.
public class AchievementsUI : MonoBehaviour
{
    public GameObject panel;
    public Transform listContainer;
    public GameObject entryTemplate;
    public Button openButton;
    public Button closeButton;
    // Plays through this GameObject's own AudioSource (a sibling of
    // MainMenuManager/CharacterSelector on the MenuManager object).
    public AudioClip clickSound;
    private AudioSource sfxSource;

    void Start()
    {
        sfxSource = GetComponent<AudioSource>();
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (panel != null) panel.SetActive(false);
        BuildList();
    }

    private static readonly Color UnlockedAccent = new Color(0.25f, 1f, 0.6f);
    private static readonly Color LockedAccent = new Color(0.35f, 0.38f, 0.46f);

    void BuildList()
    {
        if (entryTemplate == null || listContainer == null) return;

        AchievementData[] achievements = Resources.LoadAll<AchievementData>("Achievements");
        foreach (AchievementData a in achievements)
        {
            GameObject entry = Instantiate(entryTemplate, listContainer);
            entry.SetActive(true);

            bool unlocked = AchievementManager.IsUnlocked(a.id);
            Color accent = unlocked ? UnlockedAccent : LockedAccent;

            Transform titleT = entry.transform.Find("TitleText");
            if (titleT != null)
            {
                TextMeshProUGUI title = titleT.GetComponent<TextMeshProUGUI>();
                title.text = a.title;
                title.color = unlocked ? accent : Color.white;
            }

            Transform descT = entry.transform.Find("DescText");
            if (descT != null)
                descT.GetComponent<TextMeshProUGUI>().text = a.description;

            Transform badgeT = entry.transform.Find("StatusBadge");
            if (badgeT != null)
            {
                TextMeshProUGUI badge = badgeT.GetComponent<TextMeshProUGUI>();
                badge.text = unlocked ? "DONE" : "LOCKED";
                badge.color = accent;
            }

            Transform accentT = entry.transform.Find("Accent");
            if (accentT != null)
                accentT.GetComponent<Image>().color = accent;
        }
    }

    void Open()
    {
        if (panel != null) panel.SetActive(true);
        if (sfxSource != null && clickSound != null)
            sfxSource.PlayOneShot(clickSound, 0.6f);
    }

    void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (sfxSource != null && clickSound != null)
            sfxSource.PlayOneShot(clickSound, 0.6f);
    }
}
