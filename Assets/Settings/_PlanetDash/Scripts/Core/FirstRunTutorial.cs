using UnityEngine;
using System.Collections;

// One-time onboarding for first-time players: at the very start of their
// first ever run, walks through swipe up/down/left-right/turn with a big
// on-screen prompt paired with a strong (repeated-pulse) haptic buzz for
// each, so the physical gesture reads as clearly as the text. Never shown
// again after the first run. Self-bootstraps at scene load (same pattern
// as AchievementManager/MagnetEffect) rather than needing scene wiring.
public class FirstRunTutorial : MonoBehaviour
{
    private const string ShownKey = "TutorialShown";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (GameManager.Instance == null) return;
        if (PlayerPrefs.GetInt(ShownKey, 0) == 1) return;
        new GameObject("FirstRunTutorial").AddComponent<FirstRunTutorial>();
    }

    private static readonly Color HintColor = new Color(0.3f, 0.9f, 1f);
    private static readonly Color TurnHintColor = new Color(1f, 0.6f, 0.1f);

    IEnumerator Start()
    {
        // Marked immediately (not after the sequence finishes) so backing
        // out mid-run still counts as "seen it" rather than replaying the
        // whole tutorial every time someone quits early.
        PlayerPrefs.SetInt(ShownKey, 1);
        PlayerPrefs.Save();

        yield return new WaitForSeconds(1f);
        yield return Prompt("SWIPE UP TO JUMP", HintColor);
        yield return new WaitForSeconds(1.6f);
        yield return Prompt("SWIPE DOWN TO SLIDE", HintColor);
        yield return new WaitForSeconds(1.6f);
        yield return Prompt("SWIPE LEFT / RIGHT TO MOVE", HintColor);
        yield return new WaitForSeconds(1.6f);
        yield return Prompt("AT A TURN, SWIPE THAT DIRECTION", TurnHintColor);

        Destroy(gameObject);
    }

    IEnumerator Prompt(string text, Color tint)
    {
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowTopBanner(text, 1.4f, tint);
        yield return BigHaptic();
    }

    // A single Handheld.Vibrate() reads as a short, easy-to-miss buzz —
    // three quick pulses read as a much stronger "pay attention" cue for
    // a tutorial moment specifically, without needing a native haptics
    // plugin for intensity control.
    IEnumerator BigHaptic()
    {
        for (int i = 0; i < 3; i++)
        {
            Handheld.Vibrate();
            yield return new WaitForSeconds(0.12f);
        }
    }
}
