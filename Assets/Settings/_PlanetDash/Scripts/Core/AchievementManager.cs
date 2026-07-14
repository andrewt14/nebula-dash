using UnityEngine;

// Loads every AchievementData under Resources/Achievements, checks live
// game stats each frame, and persists newly unlocked ones via PlayerPrefs
// (key: "Ach_<id>" = 1). Add more achievements later by creating new
// AchievementData assets in that folder — no code changes needed here.
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;
    // Incremented by Boulder.cs whenever one passes the player unkilled.
    public static int BouldersDodged = 0;

    private AchievementData[] achievements;
    private DifficultyManager dm;

    void Awake()
    {
        Instance = this;
        achievements = Resources.LoadAll<AchievementData>("Achievements");
    }

    void Start()
    {
        dm = FindObjectOfType<DifficultyManager>();
    }

    void Update()
    {
        if (dm == null || achievements == null) return;

        foreach (AchievementData a in achievements)
        {
            if (IsUnlocked(a.id)) continue;

            float progress = 0f;
            if (a.condition == AchievementData.ConditionType.ScoreReached)
                progress = dm.score;
            else if (a.condition == AchievementData.ConditionType.SurvivalTime)
                progress = dm.runTime;
            else if (a.condition == AchievementData.ConditionType.BouldersDodged)
                progress = BouldersDodged;

            if (progress >= a.targetValue)
                Unlock(a.id, a.title);
        }
    }

    void Unlock(string id, string title)
    {
        PlayerPrefs.SetInt("Ach_" + id, 1);
        PlayerPrefs.Save();

        Handheld.Vibrate();

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowTopBanner(
                "🏆 ACHIEVEMENT: " + title, 2.5f,
                new Color(1f, 0.85f, 0.2f));
    }

    public static bool IsUnlocked(string id)
    {
        return PlayerPrefs.GetInt("Ach_" + id, 0) == 1;
    }
}
