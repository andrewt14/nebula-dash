using UnityEngine;
using TMPro;
using System.Collections;

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance;

    [Header("Score")]
    public float score = 0f;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI multiplierText;

    [Header("Difficulty")]
    public float currentDifficulty = 1f;
    // Raised from 140 so the ramp keeps climbing over a full-length run
    // instead of plateauing after ~30-40s — spawn-interval floors
    // (minBoulderInterval/minMeteoriteInterval) and the hazard-occupancy
    // checks in ObjectSpawner/MeteoriteSpawner are what keep things from
    // overlapping as spacing tightens, not the difficulty ceiling itself.
    public float maxDifficulty = 260f;
    // Front-loaded ramp: difficulty climbs fast in the opening seconds
    // then decelerates as it approaches maxDifficulty, so real
    // challenge arrives within ~20-30s and later increases naturally
    // space out instead of a flat linear crawl.
    public float earlyRampRate = 10f;
    public float lateRampRate = 1f;
    // Ramp rate eases in smoothly over this many seconds instead of
    // snapping straight to earlyRampRate on frame 1 — that instant jump
    // was the source of the jarring speed/score spike at run start.
    public float rampWarmupDuration = 2f;
    // Kept for scene serialization compatibility; no longer used.
    public float difficultyIncreaseRate = 1f;

    [Header("Speed Limits")]
    public float minRunSpeed = 12f;
    public float maxRunSpeed = 200f;
    public float minMeteoriteInterval = 0.2f;
    public float minBoulderInterval = 0.8f;
    // UFO/AlienWall/Strafing/AlienRunner intervals used to stay fixed at
    // their Inspector base value for the whole run — only boulder/
    // meteorite/orb actually tightened with difficulty, so those hazard
    // types never got denser late-run. Same base-minus-difficulty shape
    // as the boulder interval below, each tuned to reach its own floor
    // by the same difficulty~160 reference point.
    public float minUfoInterval = 3f;
    public float minAlienWallInterval = 4f;
    public float minStrafingInterval = 2.5f;
    public float minAlienInterval = 3f;

    [Header("Multiplier")]
    public float scoreMultiplier = 1f;
    private float multiplierTimer = 0f;

    [Header("Tension / Release Pacing")]
    // Alternating waves of denser obstacles (tension) and brief
    // sparser breathers (release), layered on top of the base
    // difficulty so pacing reads as rhythm, not a straight line.
    public float tensionDuration = 9f;
    public float releaseDuration = 4f;
    public float pacingBlend = 1.2f;                       // ease time between phases
    [Range(0.4f, 1f)] public float tensionSpacing = 0.65f; // interval mult at peak tension
    [Range(1f, 1.8f)] public float releaseSpacing = 1.35f; // interval mult at full release
    // Seconds of actual play (only counts while alive). Used as the
    // progression clock for zones and obstacle unlocks, so those stay
    // correctly paced independent of the (deliberately slow) score value.
    public float runTime = 0f;
    private float pacingWave = 0f;                         // 0 = release, 1 = tension

    [Header("References")]
    public MeteoriteSpawner meteoriteSpawner;
    public ObjectSpawner objectSpawner;

    private PlayerController pc;
    // Last values actually pushed to the HUD, so Update can skip the
    // string build + TMP mesh rebuild on frames where nothing changed.
    private int lastShownScore = -1;
    private int lastMultiplierSecs = -1;
    private float lastMultiplierValue = -1f;

    void Awake()
    {
        Instance = this;
        pc = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        // Count down multiplier
        if (multiplierTimer > 0f)
        {
            multiplierTimer -= Time.deltaTime;
            if (multiplierTimer <= 0f)
                scoreMultiplier = 1f;
        }

        // Show/hide multiplier text
        if (multiplierText != null)
        {
            if (scoreMultiplier > 1f)
            {
                if (!multiplierText.gameObject.activeSelf)
                    multiplierText.gameObject.SetActive(true);
                // Only rebuild the string when a displayed value actually
                // changes. Assigning .text re-generates the whole TMP mesh
                // even when the content is identical, so doing it every
                // frame cost a string alloc plus a mesh rebuild at 60fps.
                int secs = Mathf.CeilToInt(multiplierTimer);
                if (secs != lastMultiplierSecs ||
                    !Mathf.Approximately(scoreMultiplier, lastMultiplierValue))
                {
                    lastMultiplierSecs = secs;
                    lastMultiplierValue = scoreMultiplier;
                    multiplierText.text = "x" + scoreMultiplier +
                        " (" + secs + "s)";
                }
            }
            else if (multiplierText.gameObject.activeSelf)
            {
                multiplierText.gameObject.SetActive(false);
                lastMultiplierSecs = -1;
            }
        }

        if (pc != null && !pc.isAlive) return;

        // Base rate cut hard (was 10) so ~10k takes a couple minutes of
        // real play while keeping the difficulty-driven acceleration.
        score += Time.deltaTime * 0.7f *
                 currentDifficulty * scoreMultiplier;

        // Same reasoning as the multiplier text above — the score only
        // changes by a whole digit a few times a second, but this ran a
        // ToString() alloc and a full TMP mesh rebuild every single frame.
        if (scoreText != null)
        {
            int shownScore = Mathf.FloorToInt(score);
            if (shownScore != lastShownScore)
            {
                lastShownScore = shownScore;
                scoreText.text = shownScore.ToString();
            }
        }

        runTime += Time.deltaTime;

        // No upper gate — Mathf.Lerp clamps its t to 1, so once
        // currentDifficulty passes maxDifficulty this naturally settles
        // into a steady lateRampRate/sec crawl forever instead of fully
        // stopping. Individual systems (runSpeed, spawn intervals) still
        // have their own floors/ceilings, so this can't break anything —
        // it just keeps the run from ever going fully static.
        float warmup = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(runTime / Mathf.Max(0.01f, rampWarmupDuration)));
        float ramp = Mathf.Lerp(earlyRampRate, lateRampRate,
            currentDifficulty / maxDifficulty) * warmup;
        currentDifficulty += ramp * Time.deltaTime;

        ApplyDifficulty();
    }

    void ApplyDifficulty()
    {
        // Coefficients are tuned so speed/spawn-rate reach their caps
        // around currentDifficulty ~160 (roughly 2.5-3 minutes at the
        // default difficultyIncreaseRate) instead of within the first
        // 30-40 seconds, so the ramp reads as gradual over a full run.
        if (pc != null)
        {
            pc.runSpeed = Mathf.Min(maxRunSpeed,
                minRunSpeed + currentDifficulty * 1.32f);
        }

        // Tension/release oscillator: full tension for tensionDuration,
        // then a release breather, easing between the two.
        float cycle = tensionDuration + releaseDuration;
        float phase = cycle > 0f ? runTime % cycle : 0f;
        float pacingTarget = phase < tensionDuration ? 1f : 0f;
        pacingWave = Mathf.MoveTowards(pacingWave, pacingTarget,
            Time.deltaTime / Mathf.Max(0.01f, pacingBlend));
        // <1 tightens spawns (tension), >1 opens them up (release). A
        // storm tightens further on top of the tension/release wave, so
        // it reads as mechanically harder, not just louder/darker.
        float spacingMult = Mathf.Lerp(
            releaseSpacing, tensionSpacing, pacingWave);
        spacingMult *= 1f - WeatherManager.StormIntensity * 0.25f;

        if (meteoriteSpawner != null)
        {
            meteoriteSpawner.spawnInterval = Mathf.Max(
                minMeteoriteInterval,
                (2f - currentDifficulty * 0.011f) * spacingMult);
        }

        if (objectSpawner != null)
        {
            objectSpawner.spawnInterval = Mathf.Max(
                0.3f, (1.5f - currentDifficulty * 0.0075f) * spacingMult);
            objectSpawner.boulderInterval = Mathf.Max(
                minBoulderInterval,
                (4f - currentDifficulty * 0.02f) * spacingMult);
            objectSpawner.ufoInterval = Mathf.Max(
                minUfoInterval,
                (9f - currentDifficulty * 0.0375f) * spacingMult);
            objectSpawner.alienWallInterval = Mathf.Max(
                minAlienWallInterval,
                (10f - currentDifficulty * 0.0375f) * spacingMult);
            objectSpawner.strafingInterval = Mathf.Max(
                minStrafingInterval,
                (6f - currentDifficulty * 0.021875f) * spacingMult);
            objectSpawner.alienInterval = Mathf.Max(
                minAlienInterval,
                (8f - currentDifficulty * 0.03125f) * spacingMult);
        }

        if (LaneSpacingManager.Instance != null)
        {
            LaneSpacingManager.Instance.SetDifficulty(currentDifficulty);
            // Extra breathing room (more safe gaps) during release.
            LaneSpacingManager.Instance.safeGapChance +=
                (1f - pacingWave) * 0.08f;
        }
    }

    public void ActivateMultiplier(float multiplier, float duration)
    {
        scoreMultiplier = multiplier;
        multiplierTimer = duration;
    }

    // Shared speed multiplier for self-propelled hazards (Boulder,
    // AlienObstacle, Meteorite) so their own speed escalates with
    // difficulty the same way the player's runSpeed does, with an
    // extra kick during storms. Capped well short of unfair, same
    // spirit as maxRunSpeed.
    public static float ObstacleSpeedMultiplier()
    {
        float diff = Instance != null ? Instance.currentDifficulty : 0f;
        float mult = Mathf.Min(2.6f, 1f + diff / 190f);
        return mult + WeatherManager.StormIntensity * 0.35f;
    }

    private Coroutine scorePulseCoroutine;

    public void PulseScore()
    {
        // Orb pickups/meteorite kills can fire this several times a
        // second — without cancelling the previous run, overlapping
        // coroutines each captured whatever scale the last one had
        // reached as "original" and compounded on top of it, so the
        // score text would drift larger and larger over a run.
        if (scorePulseCoroutine != null)
            StopCoroutine(scorePulseCoroutine);
        scorePulseCoroutine = StartCoroutine(ScorePulse());
    }

    IEnumerator ScorePulse()
    {
        float timer = 0f;
        float duration = 0.15f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            float scale = t < 0.5f ?
                Mathf.Lerp(1f, 1.4f, t / 0.5f) :
                Mathf.Lerp(1.4f, 1f, (t - 0.5f) / 0.5f);
            if (scoreText != null)
                scoreText.transform.localScale =
                    Vector3.one * scale;
            yield return null;
        }

        if (scoreText != null)
            scoreText.transform.localScale = Vector3.one;
        scorePulseCoroutine = null;
    }

    public int GetScore()
    {
        return Mathf.FloorToInt(score);
    }
}