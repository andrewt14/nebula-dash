using UnityEngine;
using System.Collections;

public class Meteorite : MonoBehaviour
{
    public float fallSpeed = 35f;
    public float rotationSpeed = 360f;
    public float playerKillRadius = 1.2f;
    public GameObject eruptionPrefab;
    private bool hasLanded = false;
    private bool nearMissRewarded = false;
    private float landedFailsafeTimer = 30f;
    private Transform player;
    private PlayerController pc;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        player = GameObject.Find("Player").transform;
        pc = FindObjectOfType<PlayerController>();

        GameObject lightObj = new GameObject("MeteoriteLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        Light light = lightObj.AddComponent<Light>();
        light.color = new Color(1f, 0.15f, 0f);
        light.intensity = 5f;
        light.range = 6f;

        CreateGlowPass();
    }

    // Additive fresnel rim over the rock's own material/color — same
    // silhouette, no remodel, just a stronger red-hot glow.
    void CreateGlowPass()
    {
        Shader rimShader = Shader.Find("NebulaDash/SuitRimGlow");
        if (rimShader == null) return;

        Material glow = new Material(rimShader);
        glow.SetColor("_RimColor", new Color(1f, 0.05f, 0f));
        glow.SetFloat("_RimStrength", 3.5f);
        glow.SetFloat("_RimPower", 2.4f);

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material[] orig = r.sharedMaterials;
            Material[] mats = new Material[orig.Length + 1];
            for (int i = 0; i < orig.Length; i++) mats[i] = orig[i];
            mats[orig.Length] = glow;
            r.sharedMaterials = mats;
        }
    }

    void OnEnable()
    {
        // Reset per-spawn state so pooled instances behave like new ones.
        // This script lives on a child offset from the pooled root, and
        // directly writes to world position while falling, so its local
        // offset must be re-zeroed here or a reused instance spawns wherever
        // its previous fall happened to leave it instead of the new spawn point.
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        hasLanded = false;
        nearMissRewarded = false;
        landedFailsafeTimer = 30f;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void Update()
    {
        if (!hasLanded)
        {
            // Falls faster with difficulty/storms, same as the other
            // self-propelled hazards, so it stays a real threat instead
            // of becoming easy once the player's own speed outpaces it.
            transform.position += Vector3.down *
                                  fallSpeed * DifficultyManager.ObstacleSpeedMultiplier()
                                  * Time.deltaTime;
            transform.Rotate(
                rotationSpeed * Time.deltaTime,
                rotationSpeed * 0.7f * Time.deltaTime,
                0);

            if (transform.position.y <= 0.5f)
            {
                hasLanded = true;

                if (ScreenShake.Instance != null)
                    ScreenShake.Instance.Shake(0.05f, 0.05f);

                transform.position = new Vector3(
                    transform.position.x,
                    0.5f,
                    transform.position.z);

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
                StartCoroutine(ImpactEffect());
            }

            if (player != null)
            {
                float dist = Vector3.Distance(
                    transform.position, player.position);

                // Near miss shake
                if (dist < 2.5f && dist > playerKillRadius)
                {
                    if (ScreenShake.Instance != null)
                        ScreenShake.Instance.Shake(0.02f, 0.02f);
                }

                if (dist < playerKillRadius)
                {
                    if (GameManager.Instance != null)
                        GameManager.Instance.TriggerDeath();
                }
            }
        }
else
{
    if (player != null)
    {
        // Kill for the whole time it sits on the track. Horizontal
        // distance only — the rock rests at y=0.5 while the player
        // pivot is higher, so a 3D distance check silently shrinks
        // the effective radius to almost nothing.
        float xDist = Mathf.Abs(
            transform.position.x - player.position.x);
        float zDist = Mathf.Abs(
            transform.position.z - player.position.z);

        // Widen the z window with per-frame player movement so the
        // check can't be tunneled through at high run speeds.
        float frameStep = pc != null
            ? pc.runSpeed * Time.deltaTime
            : 0f;
        float zWindow = Mathf.Max(0.8f, frameStep * 0.6f);

        // Feet clearly above the rock means the player jumped over it.
        bool jumpedOver =
            player.position.y - transform.position.y > 0.6f;

        if (xDist < 0.8f && zDist < zWindow && !jumpedOver)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerDeath();
        }
        // Only reward genuinely tight dodges — a hair to the side,
        // or a clean jump directly over the rock.
        else if (!nearMissRewarded && zDist < 0.9f &&
                 (xDist < 1.4f || (jumpedOver && xDist < 0.8f)))
        {
            // Barely dodged it — reward the risk so skimming
            // obstacles feels better than playing it safe.
            nearMissRewarded = true;
            if (GameManager.Instance != null &&
                !GameManager.Instance.isGameOver)
            {
                if (DifficultyManager.Instance != null)
                {
                    DifficultyManager.Instance.score += 15f;
                    DifficultyManager.Instance.PulseScore();
                }
                if (ScorePopup.Instance != null)
                    ScorePopup.Instance.ShowPopup(
                        "CLOSE! +15", transform.position);
            }
        }

        // Return to pool once the player has passed it, like the
        // other obstacles — a fixed lifetime despawns it before a
        // slow (early-game) player ever reaches it.
        if (transform.position.z < player.position.z - 20f)
            ObjectPool.Instance.Return(gameObject);
    }

    // Failsafe so instances can't leak if the player is gone.
    landedFailsafeTimer -= Time.deltaTime;
    if (landedFailsafeTimer <= 0f)
        ObjectPool.Instance.Return(gameObject);
}

    }
    IEnumerator ImpactEffect()
    {
        // TODO(audio-sync): reported as out of sync with the landing visual.
        // Sound, position-snap, and VFX spawn are all synchronous in this
        // same frame — couldn't reproduce a code-level desync by reading.
        // Likely candidate: baked-in leading silence in the shared
        // Impact.ogg clip (would affect every PlayImpact() caller, not
        // just this one). No ffmpeg/audio tooling available in this
        // environment to verify — needs manual clip inspection or trimming.
        if (AudioManager.Instance != null)
    AudioManager.Instance.PlayImpact();
        if (eruptionPrefab != null)
        {
            Vector3 spawnPos = new Vector3(
                transform.position.x,
                0.5f,
                transform.position.z);
            GameObject eruption = Instantiate(
                eruptionPrefab, spawnPos,
                Quaternion.identity);

            ParticleSystem ps = 
                eruption.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Play();

            Destroy(eruption, 2f);
        }
        yield return null;
    }
}