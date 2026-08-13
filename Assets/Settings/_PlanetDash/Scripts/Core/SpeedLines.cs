using UnityEngine;

public class SpeedLines : MonoBehaviour
{
    public ParticleSystem speedLineParticles;
    private PlayerController pc;
    public float speedThreshold = 28f;
    // Top of the emission-rate ramp. Was a bare "60f" literal — since
    // runSpeed climbs to DifficultyManager.maxRunSpeed (~240), that meant
    // InverseLerp(40, 60, runSpeed) hit 1.0 (max emission) the moment
    // runSpeed passed 60, which the real difficulty curve reaches within
    // the first ~15-20s of a run. Every "high speed" score threshold
    // measured for the animator-speed bug alongside this one (1000/2000/
    // 3500/5000) showed runSpeed at 83-126+ — all already past this old
    // ceiling, meaning the speed-line effect was flat/maxed for nearly
    // the entire run instead of escalating with it. One of the two real,
    // measured contributors to "looks stationary at high speed" (the
    // other was PlayerAnimator.maxAnimSpeed) — the game's primary
    // go-fast visual cue was silently capped out almost immediately.
    // Rescaled to DifficultyManager.maxRunSpeed's cap (was tuned to the
    // old 240 cap, then 60, 75, 85, now 110) — kept proportional to
    // speedThreshold/minRunSpeed so the ramp still spans nearly the whole
    // run instead of maxing out early or never reaching full rate.
    public float maxEmissionRunSpeed = 110f;
    public float maxEmissionRate = 60f;

    void Start()
    {
        pc = FindObjectOfType<PlayerController>();
        if (speedLineParticles != null)
        {
            var emission = speedLineParticles.emission;
            emission.rateOverTime = 0f;
        }
    }

    void Update()
    {
        if (pc == null || speedLineParticles == null) return;

        // The module must be fetched from the ParticleSystem each
        // time — caching it in a field leaves an unusable default
        // instance if the system wasn't ready when Start ran.
        var emission = speedLineParticles.emission;

        if (pc.runSpeed > speedThreshold)
        {
            float speedPercent = Mathf.InverseLerp(
                speedThreshold, maxEmissionRunSpeed, pc.runSpeed);
            emission.rateOverTime = speedPercent * maxEmissionRate;
        }
        else
        {
            emission.rateOverTime = 0f;
        }
    }
}
