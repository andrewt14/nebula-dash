using UnityEngine;

public class AlienWall : MonoBehaviour
{
    public float moveSpeed = 1f;
    // Bob speed climbs with difficulty so the wall keeps reading as more
    // aggressive throughout the run, not just at a fixed rate.
    public float moveSpeedPerDifficulty = 0.025f;
    public float maxMoveSpeed = 4f;
    // Bob amplitude. Was 0.2 — confirmed via direct position logging that
    // the wall genuinely never stops animating (spawning->bob transition
    // and the per-frame Sin offset both checked live, no freeze), but a
    // +/-0.2 sway on a wall this size reads as visually static from normal
    // play distance — "looks stuck" even though it technically isn't.
    // Raised to 0.4. RestHeightFor (below) derives the rest height FROM
    // this value specifically so a bigger bob can't reintroduce the
    // ground-clipping bug — the floor grows with moveRange automatically,
    // so this can't sink the mesh below y=0 on the downswing.
    public float moveRange = 0.4f;
    public float playerKillRadius = 2f;
    public float spawnFromY = -3f;
    public float targetY = 1f;

    [Header("Height mode")]
    // Each wall spawns as either a HIGH barrier with a gap underneath
    // (slide under it) or a LOW barrier (jump over it), so obstacles vary
    // in the action they require instead of always forcing a lane change.
    public float slideUnderY = 2.9f;   // hovers high -> slide under
    // Both of these are REQUESTS, not final rest heights — RestHeightFor
    // raises either one if the wall's own half-height plus its bob would
    // otherwise push the mesh through the floor. Hand-tuning this value
    // is what kept failing: 0.4 buried the short wall at rest, 0.75 fixed
    // rest but still sank it on every bob downswing.
    public float jumpOverY = 0.75f;     // sits low -> jump over
    public float jumpOverScaleY = 0.45f;
    // The short jump-over wall only starts appearing once the run has
    // picked up some difficulty — early game only gets the slide-under
    // variant so the very first walls aren't the harder read.
    public float jumpOverUnlockDifficulty = 20f;

    private enum WallMode { SlideUnder, JumpOver }
    private WallMode mode;
    private Vector3 baseScale;

    private Transform player;
    private PlayerController pc;
    private float destroyDistance = 15f;
    private bool spawning = true;
    private float spawnSpeed = 5f;
    private bool isDead = false;
    private float spawnEndTime = 0f;
    // Mesh height at localScale.y == 1, measured once from the real
    // renderer instead of assuming a unit cube. Everything about where
    // this wall is allowed to rest is derived from this (see RestHeightFor)
    // so changing jumpOverScaleY, moveRange or the mesh itself can't
    // silently put the wall back under the floor.
    private float unitHeight = 1f;

    void Awake()
    {
        player = GameObject.Find("Player").transform;
        pc = FindObjectOfType<PlayerController>();
        baseScale = transform.localScale;
        MeasureUnitHeight();

        GameObject lightObj = new GameObject("WallLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        Light light = lightObj.AddComponent<Light>();
        light.color = Color.white;
        light.intensity = 5.5f;
        light.range = 7f;

        CreateGlowPass();
    }

    void MeasureUnitHeight()
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0 || Mathf.Approximately(baseScale.y, 0f)) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        unitHeight = b.size.y / baseScale.y;
    }

    // Lowest centre height this wall may rest at without any part of the
    // mesh dropping through the floor — including the full downswing of
    // the idle bob, which is what the previous pass missed.
    //
    // The jump-over variant is 0.45x height (half-height 0.675) and used
    // to rest at a hand-tuned 0.75, chosen so its bottom edge sat just
    // above y=0 AT REST. But Update bobs it by +/-moveRange (0.2) forever
    // after, so on every downswing the bottom edge reached -0.125: visibly
    // sunk into the ground while its kill check was fully live, which is
    // exactly the "kills me from under the floor" report. The tall
    // slide-under variant was never affected (half-height 1.5, resting at
    // 2.9), which is why this only ever showed up on the smaller walls.
    //
    // Deriving it instead of hand-tuning means the clearance holds for any
    // scale or bob amplitude.
    float RestHeightFor(float requestedY)
    {
        float halfHeight = unitHeight * transform.localScale.y * 0.5f;
        const float groundClearance = 0.02f;   // avoids z-fighting the floor
        return Mathf.Max(requestedY, halfHeight + moveRange + groundClearance);
    }

    // Additive fresnel edge-glow over the wall's own material — same
    // silhouette, no remodel, just a stronger cyan glow.
    void CreateGlowPass()
    {
        Shader rimShader = Shader.Find("NebulaDash/SuitRimGlow");
        if (rimShader == null) return;

        Material glow = new Material(rimShader);
        glow.SetColor("_RimColor", Color.white);
        glow.SetFloat("_RimStrength", 4f);
        glow.SetFloat("_RimPower", 2.1f);

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
        isDead = false;
        spawning = true;
        spawnEndTime = 0f;

        // Randomly choose slide-under vs jump-over and shape the wall.
        // Jump-over only unlocks once difficulty has picked up.
        float difficulty = DifficultyManager.Instance != null
            ? DifficultyManager.Instance.currentDifficulty : 0f;
        bool jumpOverUnlocked = difficulty >= jumpOverUnlockDifficulty;
        mode = (jumpOverUnlocked && Random.value < 0.5f)
            ? WallMode.JumpOver : WallMode.SlideUnder;

        moveSpeed = Mathf.Min(
            maxMoveSpeed, 1f + difficulty * moveSpeedPerDifficulty);
        // Scale first, then derive the rest height from the scale that was
        // actually applied — the two have to be computed together or they
        // drift apart, which is how the short variant ended up resting
        // lower than its own half-height allowed.
        if (mode == WallMode.SlideUnder)
        {
            transform.localScale = baseScale;
            targetY = RestHeightFor(slideUnderY);
        }
        else
        {
            transform.localScale = new Vector3(
                baseScale.x, baseScale.y * jumpOverScaleY, baseScale.z);
            targetY = RestHeightFor(jumpOverY);
        }
        spawnFromY = targetY - 4f;

        transform.position = new Vector3(
            transform.position.x,
            spawnFromY,
            transform.position.z);
    }

    void Update()
    {
        if (player == null || isDead) return;

        if (spawning)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                new Vector3(transform.position.x,
                           targetY,
                           transform.position.z),
                spawnSpeed * Time.deltaTime);

if (Mathf.Abs(transform.position.y - targetY) < 0.1f)
{
    spawning = false;
    spawnEndTime = Time.time;
    // Lock position exactly at targetY
    transform.position = new Vector3(
        transform.position.x,
        targetY,
        transform.position.z);
}
        }
else
{
    // Bob never dips below targetY - moveRange, and RestHeightFor
    // guarantees targetY leaves at least that much room under the
    // mesh — so a fully-raised wall can visibly bob but can never
    // sink back into the ground.
    float newY = targetY + Mathf.Sin(
        (Time.time - spawnEndTime) * moveSpeed) * moveRange;
    transform.position = new Vector3(
        transform.position.x,
        newY,
        transform.position.z);
}

// Kill box only goes live once the wall has fully risen to its resting
// height — while `spawning` is true the wall is still visually rising
// out of the ground and isn't actually blocking anything yet, so
// checking during that window was killing the player on contact with
// a hitbox that didn't match what was on screen.
if (!spawning)
{
    // Local to the player's current heading, so this stays correct after
    // a 90-degree turn re-orients which world axis is "ahead"/"lane".
    Vector3 localPos = player.InverseTransformPoint(transform.position);
    float xDist = Mathf.Abs(localPos.x);
    float zDist = Mathf.Abs(localPos.z);

    // Widen the z window with per-frame player movement so the check
    // can't be tunneled through at high run speeds. Window must be at
    // least as wide as one frame's travel distance, or a fast frame can
    // step clean over it without ever landing inside the check.
    float frameStep = pc != null ? pc.runSpeed * Time.deltaTime : 0f;
    float zWindow = Mathf.Max(0.8f, frameStep);

    // Passing depends on the wall's mode: slide under a HIGH wall, jump
    // over a LOW one. A lane change always avoids it either way.
    bool cleared = false;
    if (pc != null)
    {
        if (mode == WallMode.SlideUnder && pc.isSliding) cleared = true;
        if (mode == WallMode.JumpOver && !pc.isGrounded) cleared = true;
    }

    if (xDist < 1.5f && zDist < zWindow && !cleared)
    {
        // Gate on GameManager's own invincible/game-over state before
        // latching isDead — setting it unconditionally meant an
        // invincible pass through the wall permanently tripped the
        // isDead early-return above, stranding the pooled instance
        // active forever instead of ever reaching the despawn check
        // below.
        bool willKill = GameManager.Instance != null &&
            !GameManager.Instance.isGameOver &&
            !GameManager.Instance.isInvincible;
        if (willKill)
        {
            isDead = true;
            GameManager.Instance.TriggerDeath();
        }
    }
}

if (player.InverseTransformPoint(transform.position).z < -destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }
}