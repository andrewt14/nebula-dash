using UnityEngine;
using System.Collections;

public class Boulder : MonoBehaviour
{
    public float rollSpeed = 15f;
    public float slowMoTriggerZ = 6f;
    // Rest height leaves a visible gap under the boulder so sliding
    // under it reads as an intentional dodge.
    public float restHeight = 1.6f;
    public Material emberMaterial;
    private Transform player;
    private PlayerController pc;
    private CameraFollow camFollow;
    private float destroyDistance = 20f;
    private float gravity = -20f;
    private float verticalVelocity = 0f;
    private bool slowMoTriggered = false;
    private bool dangerWarned = false;
    private bool isDead = false;
    private static float slowMoCooldown = 0f;
    // Tracks zAhead across frames so the trigger check is a swept-interval
    // test instead of a single point sample — see slow-mo trigger comment.
    private float prevZAhead = float.MaxValue;

    void Awake()
    {
        slowMoCooldown = 0f;
        player = GameObject.Find("Player").transform;
        pc = FindObjectOfType<PlayerController>();
        camFollow = FindObjectOfType<CameraFollow>();

        GameObject lightObj = new GameObject("BoulderLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        Light light = lightObj.AddComponent<Light>();
        light.color = new Color(1f, 0.55f, 0f);
        light.intensity = 5.5f;
        light.range = 10f;

        CreateEmberTrail();
        CreateGlowPass();
    }

    // Additive fresnel rim over the rock's own material — same
    // silhouette, no remodel, just a stronger fiery orange glow.
    void CreateGlowPass()
    {
        Shader rimShader = Shader.Find("NebulaDash/SuitRimGlow");
        if (rimShader == null) return;

        Material glow = new Material(rimShader);
        glow.SetColor("_RimColor", new Color(1f, 0.5f, 0f));
        glow.SetFloat("_RimStrength", 3.2f);
        glow.SetFloat("_RimPower", 2.6f);

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material[] orig = r.sharedMaterials;
            Material[] mats = new Material[orig.Length + 1];
            for (int i = 0; i < orig.Length; i++) mats[i] = orig[i];
            mats[orig.Length] = glow;
            r.sharedMaterials = mats;
        }
    }

    // Small glowing ember trail so the boulder reads as a fast,
    // dangerous fireball instead of a plain rolling mesh.
    void CreateEmberTrail()
    {
        GameObject trailObj = new GameObject("EmberTrail");
        trailObj.transform.parent = transform;
        trailObj.transform.localPosition = Vector3.zero;

        ParticleSystem ps = trailObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.6f;
        main.startSpeed = 1.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.1f, 0.9f),
            new Color(1f, 0.25f, 0f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Parent scale is 0.07; without this the embers shrink to
        // invisible specks.
        main.scalingMode = ParticleSystemScalingMode.Shape;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        var psRenderer = trailObj.GetComponent<ParticleSystemRenderer>();
        if (emberMaterial != null)
            psRenderer.material = emberMaterial;
    }

    void OnEnable()
    {
        // Reset per-spawn state so pooled instances behave like new ones.
        isDead = false;
        slowMoTriggered = false;
        dangerWarned = false;
        verticalVelocity = 0f;
        prevZAhead = float.MaxValue;
    }

    // True if another hazard (alien wall, a landed comet, an alien runner)
    // is just ahead in this boulder's lane, so it can despawn instead of
    // rolling through it.
    // sweep widens the check window to cover this frame's actual movement
    // — the default 3.2-unit window was a fixed point-sample test, so at
    // high difficulty (effectiveRollSpeed scales with
    // DifficultyManager.ObstacleSpeedMultiplier, well past 3.2 units/frame
    // at high speed or on a hitched frame) the boulder could step clean
    // over the check between two consecutive frames — dz > window one
    // frame, dz < 0 the next — and roll straight through another hazard
    // without ever registering as blocked. Same tunneling class of bug
    // the slow-mo trigger below already guards against with its own
    // swept-interval test.
    bool BlockedByObstacleAhead(float sweep)
    {
        // 1.2 (the default laneTolerance) is narrower than the hazards it
        // was checking against — AlienWall alone is 3 units wide
        // (localScale.x=3 on a unit cube = 1.5 half-width), plus the
        // boulder's own ~0.5 visual radius, so a boulder up to ~0.8 units
        // past the old tolerance could still visually clip a wall/comet
        // while this check said "not blocked". Widened to cover the
        // widest hazard's half-width plus the boulder's own radius.
        const float laneTolerance = 3f;
        Vector3 fwd = player.forward;
        return HazardSpacing.BlockedAhead<AlienWall>(transform, fwd, laneTolerance, sweep)
            || HazardSpacing.BlockedAhead<Meteorite>(transform, fwd, laneTolerance, sweep)
            || HazardSpacing.BlockedAhead<AlienObstacle>(transform, fwd, laneTolerance, sweep)
            || HazardSpacing.BlockedAhead<UFOObstacle>(transform, fwd, laneTolerance, sweep)
            || HazardSpacing.BlockedAhead<StrafingObstacle>(transform, fwd, laneTolerance, sweep)
            || HazardSpacing.BlockedAhead<LavaCrack>(transform, fwd, laneTolerance, sweep);
    }

    // Reads as the boulder cracking apart on impact instead of silently
    // vanishing when it has to yield to something ahead of it.
    void ShatterEffect()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayImpact();
        if (ScreenShake.Instance != null)
            ScreenShake.Instance.Shake(0.15f, 0.06f);

        for (int i = 0; i < 6; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(piece.GetComponent<Collider>());
            piece.transform.position = transform.position +
                Random.insideUnitSphere * 0.3f;
            piece.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f);

            Renderer r = piece.GetComponent<Renderer>();
            r.material.color = new Color(0.32f, 0.28f, 0.25f);

            Rigidbody rb = piece.AddComponent<Rigidbody>();
            rb.linearVelocity = Random.insideUnitSphere * 4f + Vector3.up * 2f;
            rb.angularVelocity = Random.insideUnitSphere * 10f;

            Destroy(piece, 1.2f);
        }
    }

    void Update()
    {
        if (player == null || isDead) return;

        // Gravity down to rest height
        if (transform.position.y > restHeight)
        {
            verticalVelocity += gravity * Time.deltaTime;
            transform.position += Vector3.up *
                                  verticalVelocity * Time.deltaTime;
        }
        else
        {
            transform.position = new Vector3(
                transform.position.x, restHeight,
                transform.position.z);
            verticalVelocity = 0f;
        }

        // Rolls toward the player (its own forward motion, not just a
        // spin-in-place) — this is the one hazard type meant to visibly
        // close distance under its own power, same as it always has.
        // ObjectSpawner.SpawnBoulder accounts for this speed in its own
        // telegraph-distance math so the combined closing speed still
        // gets a fair reaction window.
        float effectiveRollSpeed = rollSpeed * DifficultyManager.ObstacleSpeedMultiplier();
        // Rolls toward the player along the player's CURRENT heading
        // rather than hardcoded world -Z, so it still rolls the right
        // way down a corridor after a 90-degree turn.
        Vector3 rollDir = -player.forward;
        transform.position += rollDir * effectiveRollSpeed * Time.deltaTime;
        transform.Rotate(player.right * effectiveRollSpeed *
                         7f * Time.deltaTime, Space.World);

        // Don't roll straight through another obstacle in the same lane
        // (e.g. an alien wall) — shatter/despawn on contact instead.
        float sweep = Mathf.Max(3.2f, effectiveRollSpeed * Time.deltaTime * 2.5f);
        if (BlockedByObstacleAhead(sweep))
        {
            ResetTime();
            ShatterEffect();
            ObjectPool.Instance.Return(gameObject);
            return;
        }

        // Slow mo cooldown
        if (slowMoCooldown > 0f)
            slowMoCooldown -= Time.unscaledDeltaTime;

        // Measured in the player's LOCAL space (forward=z, right=x)
        // instead of raw world axes, so this stays correct after a
        // 90-degree turn re-orients which world axis is "ahead"/"lane".
        Vector3 localPos = player.InverseTransformPoint(transform.position);
        float zAhead = localPos.z;
        float xDiff = Mathf.Abs(localPos.x);

        // Early red vignette pulse when this boulder is bearing down
        // on the player's current lane.
        if (!dangerWarned && zAhead > 0f && zAhead < 30f &&
            xDiff < 1.5f)
        {
            dangerWarned = true;
            if (camFollow != null)
                camFollow.DangerPulse();
        }

        // Slow mo trigger — a swept-interval test (did zAhead pass through
        // the window this frame) instead of a point sample, so a fast
        // closing speed or a frame hitch can't skip the whole window
        // between two frames and silently never trigger.
        bool crossedWindow = zAhead <= slowMoTriggerZ && prevZAhead >= 0f;
        if (crossedWindow && xDiff < 2f && !slowMoTriggered &&
            slowMoCooldown <= 0f)
        {
            slowMoTriggered = true;
            slowMoCooldown = 8f;
            StartCoroutine(SlowMotion());

            // The one "close call" reward in the game now — moved here
            // from Meteorite's static landed-comet skim, since a slow-mo
            // near miss with a rolling boulder is the actual dramatic
            // close-call moment.
            if (DifficultyManager.Instance != null)
            {
                DifficultyManager.Instance.score += 100f;
                DifficultyManager.Instance.PulseScore();
            }
            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowPopup(
                    "CLOSE! +100", transform.position);
        }
        prevZAhead = zAhead;

        // Manual kill check
// Use visual size for kill radius
float visualRadius = 0.5f;
float xDist = Mathf.Abs(localPos.x);
float zDist = Mathf.Abs(localPos.z);
float zAheadCheck = localPos.z;

// During slow mo check if player is switching lanes
if (Time.timeScale < 1f)
{
    float distFromTarget = Mathf.Abs(
        pc.GetCurrentLaneOffset() - pc.GetTargetLaneOffset());
    if (distFromTarget > 0.3f)
        return; // Player is moving — safe
}

// Was missing an isGrounded check entirely — willHitStanding fired
// for ANY non-sliding player regardless of jump state, so jumping
// over the boulder never actually worked despite yDist being
// computed (and never used) right above, and the boulder's own
// restHeight comment explicitly framing "visible gap" dodges as
// intentional. Jump apex (~2.0) clears restHeight (1.6), so a timed
// jump is a real dodge now, matching every other jumpable hazard.
bool boulderLow = transform.position.y < restHeight - 0.1f;
bool willHitSliding = pc.isSliding && boulderLow;
bool willHitStanding = !pc.isSliding && pc.isGrounded;

// Boulder rolls toward the player, so the closing speed is both
// speeds combined. Widen the z window with per-frame closure so
// the player can't tunnel through the check at high run speeds.
// Base window is the boulder's visual surface (~1 unit radius plus
// player capsule) so death fires on visible contact, not after the
// player has clipped halfway into the rock.
float contactDistance = 1.2f;
float closingStep = (effectiveRollSpeed + (pc != null ? pc.runSpeed : 0f))
                    * Time.deltaTime;
float zWindow = Mathf.Max(contactDistance, closingStep * 0.6f);

if (xDist < visualRadius &&
    zDist < zWindow &&
    zAheadCheck > -zWindow &&
    (willHitStanding || willHitSliding))
{
    // Only commit to the impact sound/freeze if the hit actually kills —
    // GameManager.TriggerDeath() no-ops while invincible or already
    // game-over, but this block used to play the sound and freeze the
    // boulder regardless, so an invincible player would hear the "you
    // got hit" cue on every pass-through even though nothing happened.
    bool willKill = GameManager.Instance != null &&
        !GameManager.Instance.isGameOver &&
        !GameManager.Instance.isInvincible;
    if (willKill)
    {
        isDead = true;
        ResetTime();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBoulder();
        GameManager.Instance.TriggerDeath();
    }
}
        // Destroy when passed
if (player.InverseTransformPoint(transform.position).z < -destroyDistance)
        {
            if (!isDead) AchievementManager.BouldersDodged++;
            StopAllCoroutines();
            ResetTime();
            ObjectPool.Instance.Return(gameObject);
        }
    }

    IEnumerator SlowMotion()
    {
        // This fired the same "boulder impact" cue on every slow-mo
        // trigger regardless of dodge outcome, reading as a death sound
        // even on a clean dodge — the real impact sound already plays
        // separately, only on an actual kill (see the kill-check below).
        Time.timeScale = 0.4f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicPitch(0.4f);

        yield return new WaitForSecondsRealtime(0.8f);

        float elapsed = 0f;
        float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(0.4f, 1f,
                                        elapsed / duration);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetMusicPitch(Time.timeScale);
            yield return null;
        }

        ResetTime();
    }

    // Timescale and music pitch always move together — this is the one
    // place both get put back to normal, whether slow-mo finished
    // naturally or got cut short by a shatter/kill/despawn.
    void ResetTime()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicPitch(1f);
    }
}
