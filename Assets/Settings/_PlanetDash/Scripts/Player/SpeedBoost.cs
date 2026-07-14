using UnityEngine;
using System.Collections;
using TMPro;

public class SpeedBoost : MonoBehaviour
{
    public static SpeedBoost Instance;
    public TextMeshProUGUI countdownText;
    private PlayerController pc;
    private DifficultyManager dm;
    private float baseSpeed;
    private bool isBoosting = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        pc = GetComponent<PlayerController>();
        dm = FindObjectOfType<DifficultyManager>();
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
    baseSpeed = pc.runSpeed;
    float boostSpeed = baseSpeed * 2.5f;


    // Play collect sound
    if (AudioManager.Instance != null)
        AudioManager.Instance.PlayCollect();

    // Show countdown
    if (countdownText != null)
        countdownText.gameObject.SetActive(true);

    float timer = duration;
    while (timer > 0f)
    {
        timer -= Time.deltaTime;

        // Smoothly speed up
        pc.runSpeed = Mathf.Lerp(
            pc.runSpeed, boostSpeed,
            10f * Time.deltaTime);

        // Update countdown
        if (countdownText != null)
            countdownText.text =
                "⚡ BOOST " + Mathf.CeilToInt(timer) + "s";

        float pulse = Mathf.Sin(Time.time * 8f)
                     * 0.5f + 0.5f;
        if (countdownText != null)
            countdownText.color = new Color(
                0f, 1f, pulse, 1f);

        yield return null;
    }

    // Slow back down smoothly
    float slowTimer = 0f;
    while (slowTimer < 2f)
    {
        slowTimer += Time.deltaTime;
        pc.runSpeed = Mathf.Lerp(
            boostSpeed, baseSpeed,
            slowTimer / 2f);

        if (countdownText != null)
            countdownText.text = "SLOWING...";
        if (countdownText != null)
            countdownText.color = new Color(
                1f, 0.5f, 0f, 1f);

        yield return null;
    }

    pc.runSpeed = baseSpeed;
    isBoosting = false;

    if (countdownText != null)
        countdownText.gameObject.SetActive(false);
}
}