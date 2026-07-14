using UnityEngine;

// An alien that runs toward the player along the track. Its Mixamo run
// clip animates the legs in place while this script drives the actual
// movement; it faces the player so the run reads correctly. Pooled and
// spawned like the other obstacles; contact kills, lane change dodges.
public class AlienObstacle : MonoBehaviour
{
    public float runSpeed = 20f;
    // The imported Mixamo bind pose already faces world -Z (the travel
    // direction), so no facing correction is needed at rest.
    public float playerKillRadius = 1.1f;
    private Transform player;
    private PlayerController pc;
    private Animation legacyAnim;
    private string clipName;
    private float destroyDistance = 20f;
    private bool isDead = false;

    void Awake()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;
        pc = FindObjectOfType<PlayerController>();
        legacyAnim = GetComponentInChildren<Animation>();
        if (legacyAnim != null && legacyAnim.clip != null)
            clipName = legacyAnim.clip.name;
    }

    void OnEnable()
    {
        // Reset per-spawn state so pooled instances behave like new ones.
        isDead = false;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAlienAppear();
        // Body faces 180 from the imported default so it looks toward
        // the player (its travel direction) — verified via viewpoint
        // capture + Mixamo import convention.
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // With the body facing correctly, the authored gait cycles the
        // legs the wrong way (moonwalk), so play the clip in reverse.
        if (legacyAnim != null && clipName != null)
        {
            legacyAnim.Play(clipName);
            AnimationState st = legacyAnim[clipName];
            if (st != null)
            {
                st.speed = -1f;
                st.time = st.length;
            }
        }
    }

    // True if another hazard (alien wall, boulder, landed comet, or another
    // alien) is just ahead in this alien's lane — it despawns instead of
    // running through it, since it self-propels at its own speed and can
    // otherwise catch up to anything spaced only by spawn-time cooldowns.
    bool BlockedByObstacleAhead()
    {
        return HazardSpacing.BlockedAhead<AlienWall>(transform)
            || HazardSpacing.BlockedAhead<Boulder>(transform)
            || HazardSpacing.BlockedAhead<Meteorite>(transform)
            || HazardSpacing.BlockedAhead<AlienObstacle>(transform);
    }

    void Update()
    {
        if (player == null || isDead) return;

        if (BlockedByObstacleAhead())
        {
            ObjectPool.Instance.Return(gameObject);
            return;
        }

        // Charge straight down the track toward the player, scaling
        // with difficulty/storms the same way the player's own
        // runSpeed does.
        float effectiveRunSpeed = runSpeed * DifficultyManager.ObstacleSpeedMultiplier();
        transform.position += Vector3.back * effectiveRunSpeed * Time.deltaTime;

        // Contact kill. The z window widens with the combined closing
        // speed so it can't be tunneled through at high run speeds.
        float xDist = Mathf.Abs(transform.position.x - player.position.x);
        float zDist = Mathf.Abs(transform.position.z - player.position.z);
        float closing = (effectiveRunSpeed + (pc != null ? pc.runSpeed : 0f))
                        * Time.deltaTime;
        float zWindow = Mathf.Max(playerKillRadius, closing * 0.6f);

        if (xDist < playerKillRadius && zDist < zWindow)
        {
            isDead = true;
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerDeath();
        }

        if (transform.position.z < player.position.z - destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }
}
