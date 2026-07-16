using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Minimal read-only level-progress viewer for the start screen, mirroring
// AchievementsUI's structure/entry template. Shows the named zone pool
// (ZoneManager.ZoneNamePool) with locked/unlocked state driven by the
// "BestZoneLevel" PlayerPrefs value ZoneManager persists as the run
// advances through zones.
public class LevelsUI : MonoBehaviour
{
    public GameObject panel;
    public Transform listContainer;
    public GameObject entryTemplate;
    public Button openButton;
    public Button closeButton;
    public AudioClip clickSound;
    private AudioSource sfxSource;

    private static readonly Color UnlockedAccent = new Color(0.25f, 1f, 0.6f);
    private static readonly Color LockedAccent = new Color(0.35f, 0.38f, 0.46f);

    void Start()
    {
        sfxSource = GetComponent<AudioSource>();
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (panel != null) panel.SetActive(false);
        BuildList();
    }

    void BuildList()
    {
        if (entryTemplate == null || listContainer == null) return;

        int bestLevel = PlayerPrefs.GetInt("BestZoneLevel", 0);
        string[] names = ZoneManager.ZoneNamePool;

        for (int i = 0; i < names.Length; i++)
        {
            GameObject entry = Instantiate(entryTemplate, listContainer);
            entry.SetActive(true);

            bool unlocked = i <= bestLevel;
            Color accent = unlocked ? UnlockedAccent : LockedAccent;

            Transform titleT = entry.transform.Find("TitleText");
            if (titleT != null)
            {
                TextMeshProUGUI title = titleT.GetComponent<TextMeshProUGUI>();
                title.text = "LEVEL " + (i + 1) + ": " + names[i];
                title.color = unlocked ? accent : Color.white;
            }

            Transform descT = entry.transform.Find("DescText");
            if (descT != null)
                descT.GetComponent<TextMeshProUGUI>().text = unlocked
                    ? "Reached"
                    : "Reach it in a run to unlock";

            Transform badgeT = entry.transform.Find("StatusBadge");
            if (badgeT != null)
            {
                TextMeshProUGUI badge = badgeT.GetComponent<TextMeshProUGUI>();
                badge.text = unlocked ? "UNLOCKED" : "LOCKED";
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
