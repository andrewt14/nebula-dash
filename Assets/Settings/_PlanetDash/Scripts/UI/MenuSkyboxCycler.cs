using UnityEngine;

// Cycles the menu skybox through the same neon zone tints ZoneManager
// uses in gameplay (Nebula Drift / Station Corridor / Deep Void), so the
// start screen backdrop keeps shifting color instead of sitting static.
public class MenuSkyboxCycler : MonoBehaviour
{
    public float secondsPerZone = 2.5f;
    public float transitionDuration = 0.8f;

    private static readonly Color[] ZoneTints =
    {
        new Color(0.80f, 0.35f, 0.95f), // Nebula Drift
        new Color(0.35f, 0.60f, 1.00f), // Station Corridor
        new Color(0.90f, 0.25f, 0.60f), // Deep Void
    };

    private Material skyboxInstance;
    private int currentIndex = 0;
    private float timer;

    void Start()
    {
        if (RenderSettings.skybox != null)
        {
            // Instance it so this doesn't dirty the shared skybox asset.
            skyboxInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyboxInstance;
            ApplyTint(ZoneTints[0], 1f);
        }
        timer = secondsPerZone;
    }

    void Update()
    {
        if (skyboxInstance == null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            currentIndex = (currentIndex + 1) % ZoneTints.Length;
            timer = secondsPerZone;
        }

        // Continuous ease toward the current zone's tint — reads as a
        // smooth transition right after each switch, then holds steady.
        float lerpSpeed = Time.deltaTime / Mathf.Max(0.01f, transitionDuration);
        ApplyTint(ZoneTints[currentIndex], lerpSpeed);
    }

    void ApplyTint(Color target, float lerpAmount)
    {
        if (skyboxInstance.HasProperty("_Tint"))
            skyboxInstance.SetColor("_Tint",
                Color.Lerp(skyboxInstance.GetColor("_Tint"), target, lerpAmount));
        else if (skyboxInstance.HasProperty("_SkyTint"))
            skyboxInstance.SetColor("_SkyTint",
                Color.Lerp(skyboxInstance.GetColor("_SkyTint"), target, lerpAmount));
    }
}
