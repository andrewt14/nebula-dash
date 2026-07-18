using UnityEngine;
using System.Collections;

public class GoldOrb : MonoBehaviour
{
    private static readonly Color GoldColor = new Color(0.85f, 1f, 0.1f);

    private float bobSpeed = 3f;
    private float bobHeight = 0.3f;
    private Vector3 startPos;
    private Vector3 baseScale;
    private Transform player;
    private float collectRadius = 2.5f;
    private bool collected = false;
    private Light orbLight;

    void Start()
    {
        startPos = transform.position;
        baseScale = transform.localScale;
        player = GameObject.Find("Player").transform;

        GameObject lightObj = new GameObject("GoldLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        orbLight = lightObj.AddComponent<Light>();
        orbLight.color = GoldColor;
        orbLight.intensity = 8f;
        orbLight.range = 8f;

        // Hot gold emission so it reads as premium next to the cooler
        // regular orbs.
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.material.EnableKeyword("_EMISSION");
            r.material.SetColor("_EmissionColor", GoldColor * 3f);
        }
    }

    void Update()
    {
        if (collected) return;

        transform.position = startPos +
            transform.up * Mathf.Sin(
                Time.time * bobSpeed) * bobHeight;

        transform.Rotate(Vector3.up * 200f * Time.deltaTime);
        transform.Rotate(Vector3.forward *
                        100f * Time.deltaTime);

        // Breathing pulse in both light and scale
        if (orbLight != null)
            orbLight.intensity = 6f +
                Mathf.Sin(Time.time * 6f) * 2f;
        transform.localScale = baseScale *
            (1f + Mathf.Sin(Time.time * 4f) * 0.08f);

        if (player != null)
        {
            float dist = Vector3.Distance(
                transform.position, player.position);
            if (dist < collectRadius)
            {
                collected = true;
                StartCoroutine(CollectGoldOrb());
            }
        }
    }

    IEnumerator CollectGoldOrb()
    {
        // Hide instantly so collection never interrupts the run — the
        // object only lingers invisibly to finish its effects.
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (orbLight != null) orbLight.enabled = false;

        // 2x score for 10 seconds — no invincibility, that's the navy
        // orb's job now.
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.ActivateMultiplier(2f, 10f);
            DifficultyManager.Instance.score += 50f;
            DifficultyManager.Instance.PulseScore();
        }
        PlayerRimEffect.Flash(GoldColor, 10f);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayCollect();

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowPopup(
                "+500!", transform.position);
        StartCoroutine(DelayedPopup());

        SpawnBurstParticles();

        // Golden flash on the directional light, faded back out
        Light dirLight = null;
        foreach (Light l in FindObjectsByType<Light>(
            FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional)
            {
                dirLight = l;
                break;
            }
        }

        Color originalColor = dirLight != null ?
            dirLight.color : Color.white;
        float originalIntensity = dirLight != null ?
            dirLight.intensity : 1f;

        if (dirLight != null)
        {
            dirLight.color = GoldColor;
            dirLight.intensity = 10f;
        }

        float elapsed = 0f;
        float duration = 1.5f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (dirLight != null)
            {
                dirLight.color = Color.Lerp(
                    GoldColor, originalColor,
                    elapsed / duration);
                dirLight.intensity = Mathf.Lerp(
                    10f, originalIntensity,
                    elapsed / duration);
            }
            yield return null;
        }

        if (dirLight != null)
        {
            dirLight.color = originalColor;
            dirLight.intensity = originalIntensity;
        }

        Destroy(gameObject);
    }

    void SpawnBurstParticles()
    {
        if (player == null) return;

        // One shared material for the whole burst — per-sphere
        // material instances caused a visible hitch on collection.
        Material burstMat = null;

        for (int i = 0; i < 12; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            particle.transform.position =
                player.position + Vector3.up;
            particle.transform.localScale = Vector3.one * 0.4f;
            Destroy(particle.GetComponent<Collider>());

            Renderer r = particle.GetComponent<Renderer>();
            if (burstMat == null)
            {
                burstMat = r.material;
                burstMat.color = GoldColor;
                burstMat.SetColor("_EmissionColor", GoldColor * 5f);
                burstMat.EnableKeyword("_EMISSION");
            }
            else
            {
                r.sharedMaterial = burstMat;
            }

            Vector3 dir = Random.insideUnitSphere.normalized;
            StartCoroutine(FlyParticle(particle, dir, 1.4f));
            // Failsafe: FlyParticle dies with this object, so make
            // sure stray particles can never outlive the burst.
            Destroy(particle, 1.5f);
        }
    }

    IEnumerator FlyParticle(
        GameObject particle, Vector3 direction, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            if (particle == null) yield break;
            particle.transform.position +=
                direction * 10f * Time.deltaTime;
            float scale = Mathf.Lerp(
                0.3f, 0f, timer / duration);
            particle.transform.localScale =
                Vector3.one * scale;
            yield return null;
        }
    }

    IEnumerator DelayedPopup()
    {
        yield return new WaitForSeconds(0.6f);
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowPopup(
                "2X SCORE!",
                transform.position + Vector3.up * 2f);
    }
}
