using UnityEngine;
using System.Collections;
using TMPro;

// Used to write pc.runSpeed directly (both mid-boost and on revert). That
// put two systems writing the same field every frame with no coordination:
// DifficultyManager.ApplyDifficulty() ALSO sets pc.runSpeed unconditionally,
// every frame, from the natural difficulty curve. Whichever ran later in a
// given frame won that frame. Worse, the revert at the end of a boost set
// pc.runSpeed back to `baseSpeed` — a snapshot captured at the MOMENT the
// boost started, 5 seconds earlier (3s boost + 2s decay) — permanently
// erasing whatever natural difficulty-driven growth happened during that
// whole window. ResourceOrb fires a boost on every single pickup (no
// rarity gate, no cooldown beyond "not already boosting"), and orbs spawn
// every ~1.5s — so during ordinary play this fired constantly, each time
// yanking runSpeed back to a value already several seconds stale. That's
// what read as the run intermittently "getting stuck" at higher scores:
// not the animator's curve (already fixed separately), but the actual
// world-traversal speed itself being repeatedly reset backward.
//
// Now exposes a pure multiplier instead of ever touching pc.runSpeed.
// DifficultyManager.ApplyDifficulty is the ONLY writer of pc.runSpeed —
// it multiplies its own freshly-computed natural speed by this every
// frame. When a boost ends the multiplier eases back to 1 and runSpeed
// simply falls back to whatever the CURRENT natural speed is, never a
// stale snapshot.
public class SpeedBoost : MonoBehaviour
{
    public static SpeedBoost Instance;
    public TextMeshProUGUI countdownText;
    public float boostMultiplier = 2.5f;

    public float Multiplier { get; private set; } = 1f;

    private bool isBoosting = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    public void ActivateBoost(float duration)
    {
        if (isBoosting) return;
        StartCoroutine(BoostCoroutine(duration));
    }

    IEnumerator BoostCoroutine(float duration)
    {
        isBoosting = true;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayCollect();

        if (countdownText != null)
            countdownText.gameObject.SetActive(true);

        float timer = duration;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            Multiplier = Mathf.Lerp(Multiplier, boostMultiplier, 10f * Time.deltaTime);

            if (countdownText != null)
                countdownText.text = "⚡ BOOST " + Mathf.CeilToInt(timer) + "s";

            float pulse = Mathf.Sin(Time.time * 8f) * 0.5f + 0.5f;
            if (countdownText != null)
                countdownText.color = new Color(0f, 1f, pulse, 1f);

            yield return null;
        }

        float slowTimer = 0f;
        float startMultiplier = Multiplier;
        while (slowTimer < 2f)
        {
            slowTimer += Time.deltaTime;
            Multiplier = Mathf.Lerp(startMultiplier, 1f, slowTimer / 2f);

            if (countdownText != null)
                countdownText.text = "SLOWING...";
            if (countdownText != null)
                countdownText.color = new Color(1f, 0.5f, 0f, 1f);

            yield return null;
        }

        Multiplier = 1f;
        isBoosting = false;

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }
}
