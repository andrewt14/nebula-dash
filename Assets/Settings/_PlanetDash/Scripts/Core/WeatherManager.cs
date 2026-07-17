using UnityEngine;
using System.Collections;

// Episodic weather: the sky is clear most of the time, then a storm rolls
// in, rains and flashes lightning for a while, and clears again — brief,
// visible squalls rather than a rain level that quietly ramps forever and
// never really reads as "weather changed." As difficulty climbs, storms
// arrive more often and hit harder while they're active. The rain emitter
// follows the player. Colors are owned by ZoneManager.
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance;

    [Header("References")]
    public ParticleSystem rainParticles;
    public Light directionalLight;
    public Camera mainCamera;

    [Header("Difficulty scaling")]
    public float difficultyAtPeak = 100f;

    [Header("Storm Cycle")]
    // Weather is held off entirely until the player has been running for
    // a while — the opening stretch of a run stays clear. Was a world-Z
    // distance threshold, but world Z stops being a reliable "how far
    // into the run" measure once 90-degree turns can redirect travel
    // along X — runTime (DifficultyManager's monotonic play-time clock)
    // is the same signal every other unlock timer in the game already
    // uses instead.
    public float weatherStartRunTime = 60f;
    // Clear stretch shrinks and the storm itself lengthens as difficulty
    // climbs, so storms show up more often and last longer late-run.
    public float clearDurationEarly = 8f;
    public float clearDurationLate = 6f;
    public float stormDurationEarly = 6f;
    public float stormDurationLate = 12f;

    // Read by ZoneManager as a multiplier on its own base fog density, so
    // fog visibly thickens during a storm without the two scripts fighting
    // over RenderSettings.fogDensity directly.
    public static float StormFogMultiplier = 1f;
    // Read by the UFO lightning strikes so they only fire during a storm.
    public static bool IsStorming { get; private set; }
    // Smoothed 0-1 storm presence, read by DifficultyManager and the
    // self-propelled hazards so a storm makes the run mechanically
    // harder (faster obstacles, tighter spawns), not just louder/darker.
    public static float StormIntensity { get; private set; }

    private DifficultyManager dm;
    private Transform player;
    private float lightningTimer = 0f;
    private bool flashing = false;
    private bool inStorm = false;
    private float phaseTimer;
    private float fogMultiplierCurrent = 1f;
    private float stormIntensityCurrent = 0f;

    void Awake() { Instance = this; }

    void Start()
    {
        dm = FindObjectOfType<DifficultyManager>();
        RenderSettings.fog = true;

        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;

        if (rainParticles != null)
        {
            // World space so drops fall naturally while the emitter follows.
            var main = rainParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            rainParticles.Stop();
        }

        // Always start clear so the opening stretch of a run is calm.
        inStorm = false;
        phaseTimer = clearDurationEarly;
    }

    // Read by each UFO's own lightning bolt so background strikes get
    // more frequent as the run gets harder, not just the ones this
    // manager directly fires on the directional light.
    public static float DifficultyProgress { get; private set; }

    float Progress()
    {
        float d = dm != null ? dm.currentDifficulty : 0f;
        float p = Mathf.Clamp01(d / Mathf.Max(1f, difficultyAtPeak));
        DifficultyProgress = p;
        return p;
    }

    void Update()
    {
        // Keep the rain over the player, ahead along their CURRENT
        // heading rather than hardcoded world +Z.
        if (rainParticles != null && player != null)
            rainParticles.transform.position =
                player.position + Vector3.up * 18f + player.forward * 10f;

        float prog = Progress();

        bool weatherUnlocked = dm != null &&
            dm.runTime >= weatherStartRunTime;

        if (weatherUnlocked)
        {
            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                inStorm = !inStorm;
                phaseTimer = inStorm
                    ? Mathf.Lerp(stormDurationEarly, stormDurationLate, prog)
                    : Mathf.Lerp(clearDurationEarly, clearDurationLate, prog);
            }
        }
        else
        {
            inStorm = false;
        }
        IsStorming = inStorm;

        // Rain only exists during an active storm phase, and intensity
        // within that storm still scales with difficulty. Floor is raised
        // well above a barely-there drizzle so the very first storm of a
        // run already reads as unmistakably rain.
        if (rainParticles != null)
        {
            var em = rainParticles.emission;
            float rate = inStorm ? Mathf.Lerp(700f, 2000f, prog) : 0f;
            em.rateOverTime = rate;

            if (rate > 0f && !rainParticles.isEmitting)
                rainParticles.Play();
            else if (rate <= 0f && rainParticles.isEmitting)
                rainParticles.Stop(true,
                    ParticleSystemStopBehavior.StopEmitting);
        }

        // Fog thickens during a storm and eases back once it clears.
        // Capped lower than before (was up to 3.8x) — that peak made
        // late-run storms genuinely too foggy to see obstacles through,
        // which a vignette can't fix since it darkens the edges around a
        // readable center rather than clearing the center itself.
        float fogTarget = inStorm ? Mathf.Lerp(1.6f, 2.2f, prog) : 1f;
        fogMultiplierCurrent = Mathf.MoveTowards(
            fogMultiplierCurrent, fogTarget, Time.deltaTime * 0.8f);
        StormFogMultiplier = fogMultiplierCurrent;

        stormIntensityCurrent = Mathf.MoveTowards(
            stormIntensityCurrent, inStorm ? 1f : 0f, Time.deltaTime * 0.5f);
        StormIntensity = stormIntensityCurrent;

        // Lightning only strikes during a storm.
        if (inStorm)
        {
            lightningTimer -= Time.deltaTime;
            if (lightningTimer <= 0f && !flashing)
            {
                StartCoroutine(LightningFlash());
                float li = Mathf.Lerp(3f, 0.5f, prog);
                lightningTimer = Random.Range(li * 0.6f, li * 1.4f);
            }
        }
    }

    IEnumerator LightningFlash()
    {
        if (directionalLight == null) yield break;

        // Guard against a second flash starting mid-flash and stomping
        // these snapshots — that left the light stuck overexposed.
        flashing = true;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayImpact();

        Color oc = directionalLight.color;
        float oi = directionalLight.intensity;

        directionalLight.color = Color.white;
        directionalLight.intensity = 4f;
        yield return new WaitForSeconds(0.05f);
        directionalLight.intensity = 0.1f;
        yield return new WaitForSeconds(0.05f);
        directionalLight.intensity = 3f;
        yield return new WaitForSeconds(0.05f);

        directionalLight.color = oc;
        directionalLight.intensity = oi;
        flashing = false;

        // A delayed "thunderclap" shake, since sound/impact reads as
        // trailing the flash rather than arriving simultaneously — only
        // on some strikes, and lighter than before, so the storm doesn't
        // shake the screen on every single flash.
        if (Random.value < 0.4f)
            StartCoroutine(ThunderClap());
    }

    IEnumerator ThunderClap()
    {
        yield return new WaitForSeconds(Random.Range(0.25f, 0.5f));
        if (ScreenShake.Instance != null)
            ScreenShake.Instance.Shake(0.2f, 0.1f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayImpact();
    }
}
