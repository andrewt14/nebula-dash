using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

// Runs its Awake before every other script in the scene so the RNG
// reseed below always lands before anything (ground pits, spawners)
// makes its first Random call.
[DefaultExecutionOrder(-1000)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static string LastDeathCause = "";
    public bool isGameOver = false;
    public GameObject deathScreen;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI bestScoreText;

    public bool isInvincible = false;
    public TextMeshProUGUI invincibleTimerText;

    private Coroutine invincibilityCoroutine;

public void ActivateInvincibility(float duration)
{
    // Re-picking up the orb mid-window should refresh the timer, not
    // stack a second coroutine racing the first to clear isInvincible.
    if (invincibilityCoroutine != null)
        StopCoroutine(invincibilityCoroutine);
    invincibilityCoroutine = StartCoroutine(InvincibilityCoroutine(duration));

    MagnetEffect magnet = MagnetEffect.Get();
    if (magnet != null) magnet.Activate(duration);
}

IEnumerator InvincibilityCoroutine(float duration)
{
    isInvincible = true;
    if (ScorePopup.Instance != null)
        ScorePopup.Instance.ShowPopup(
            "INVINCIBLE!", Vector3.zero);

    float remaining = duration;
    if (invincibleTimerText != null)
        invincibleTimerText.gameObject.SetActive(true);
    while (remaining > 0f)
    {
        if (invincibleTimerText != null)
            invincibleTimerText.text =
                "INVINCIBLE " + Mathf.CeilToInt(remaining) + "s";
        remaining -= Time.deltaTime;
        yield return null;
    }

    if (invincibleTimerText != null)
        invincibleTimerText.gameObject.SetActive(false);
    isInvincible = false;
    invincibilityCoroutine = null;
}

    // Every run reseeds to the same value, so the entire obstacle
    // sequence (lane picks, formations, safe gaps, pit placement) plays
    // out identically run to run — a learnable, discernible pattern
    // instead of a fresh random layout every attempt.
    public const int RunSeed = 190310;

    void Awake()
    {
        Instance = this;
        Random.InitState(RunSeed);
    }

public void TriggerDeath()
{
    if (isGameOver || isInvincible) return;
    // Guard immediately — obstacles call this every frame while
    // overlapping the player, and death effects must fire only once.
    isGameOver = true;

    // Records which hazard actually called this, so a reported "died from
    // nothing" has a concrete cause to check. Editor-only: building a
    // StackTrace is expensive under IL2CPP and needs the managed metadata
    // that release stripping is free to discard, and this is a debug aid
    // that ships to no one.
#if UNITY_EDITOR
    var trace = new System.Diagnostics.StackTrace(1, false);
    var caller = trace.GetFrame(0)?.GetMethod();
    LastDeathCause = caller != null
        ? caller.DeclaringType + "." + caller.Name : "unknown";
    Debug.Log("TriggerDeath caused by: " + LastDeathCause);
#endif

    PlayerController pc =
        FindObjectOfType<PlayerController>();
    if (pc != null) pc.isAlive = false;

    if (DifficultyManager.Instance != null)
        DifficultyManager.Instance.enabled = false;

    if (ScreenShake.Instance != null)
        ScreenShake.Instance.Shake(0.35f, 0.3f);

    // Every hazard (boulder, alien, wall, comet, falling through a
    // ground break) funnels through here, so this is the one place that
    // needs to play the death sound rather than each obstacle doing it
    // separately — deathSound was already wired up but never triggered.
    if (AudioManager.Instance != null)
        AudioManager.Instance.PlayDeath();

    Handheld.Vibrate();

    StartCoroutine(DeathHitStop());
    StartCoroutine(ShowDeathScreen());
}

// Brief freeze-frame on impact sells the hit, and restoring the
// timescale here also cleans up any boulder slow-mo that was still
// active when the player died.
IEnumerator DeathHitStop()
{
    Time.timeScale = 0f;
    yield return new WaitForSecondsRealtime(0.09f);
    Time.timeScale = 1f;
    Time.fixedDeltaTime = 0.02f;
}

    IEnumerator ShowDeathScreen()
    {
        // Wait for death animation
        yield return new WaitForSeconds(0.5f);

        int score = 0;
        if (DifficultyManager.Instance != null)
            score = DifficultyManager.Instance.GetScore();

        int best = PlayerPrefs.GetInt("HighScore", 0);
        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt("HighScore", best);
        }

        if (finalScoreText != null)
            finalScoreText.text = score.ToString();
        if (bestScoreText != null)
            bestScoreText.text = "BEST: " + best;

        if (deathScreen != null)
            deathScreen.SetActive(true);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene("MainMenu");
    }
}