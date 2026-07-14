using UnityEngine;

public class SpeedLines : MonoBehaviour
{
    public ParticleSystem speedLineParticles;
    private PlayerController pc;
    public float speedThreshold = 40f;

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
                speedThreshold, 60f, pc.runSpeed);
            emission.rateOverTime = speedPercent * 60f;
        }
        else
        {
            emission.rateOverTime = 0f;
        }
    }
}
