using UnityEngine;

// Add more achievements later by creating new assets of this type under
// Resources/Achievements/ — AchievementManager and AchievementsUI both
// load the whole folder at runtime, no code changes needed.
[CreateAssetMenu(fileName = "Achievement", menuName = "NebulaDash/Achievement")]
public class AchievementData : ScriptableObject
{
    public enum ConditionType { ScoreReached, SurvivalTime, BouldersDodged }

    [Tooltip("Stable key used for PlayerPrefs persistence — don't change after release.")]
    public string id;
    public string title;
    [TextArea] public string description;
    public ConditionType condition;
    public float targetValue;
}
