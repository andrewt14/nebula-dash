using UnityEngine;

public class AlienWall : MonoBehaviour
{
    public float moveSpeed = 1f;
    // Bob speed climbs with difficulty so the wall keeps reading as more
    // aggressive throughout the run, not just at a fixed rate.
    public float moveSpeedPerDifficulty = 0.025f;
    public float maxMoveSpeed = 4f;
    public float moveRange = 0.2f;
    public float playerKillRadius = 2f;
    public float spawnFromY = -3f;
    public float targetY = 1f;

    [Header("Height mode")]
    // Each wall spawns as either a HIGH barrier with a gap underneath
    // (slide under it) or a LOW barrier (jump over it), so obstacles vary
    // in the action they require instead of always forcing a lane change.
    public float slideUnderY = 2.9f;   // hovers high -> slide under
    public float jumpOverY = 0.4f;     // sits low -> jump over
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

    void Awake()
    {
        player = GameObject.Find("Player").transform;
        pc = FindObjectOfType<PlayerController>();
        baseScale = transform.localScale;

        GameObject lightObj = new GameObject("WallLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        Light light = lightObj.AddComponent<Light>();
        light.color = Color.white;
        light.intensity = 5.5f;
        light.range = 7f;

        CreateGlowPass();
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
        if (mode == WallMode.SlideUnder)
        {
            targetY = slideUnderY;
            transform.localScale = baseScale;
        }
        else
        {
            targetY = jumpOverY;
            transform.localScale = new Vector3(
                baseScale.x, baseScale.y * jumpOverScaleY, baseScale.z);
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
    // Bob never dips below targetY - moveRange, and moveRange itself
    // is always well clear of the ground plane for both wall modes —
    // so a fully-raised wall can visibly bob but can never look like
    // it's sinking back into the ground.
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
    float xDist = Mathf.Abs(
        transform.position.x - player.position.x);
    float zDist = Mathf.Abs(
        transform.position.z - player.position.z);

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
        isDead = true;
        if (GameManager.Instance != null)
            GameManager.Instance.TriggerDeath();
    }
}

if (transform.position.z < player.position.z - destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }
}