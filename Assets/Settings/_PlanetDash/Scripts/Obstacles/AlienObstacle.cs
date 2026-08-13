using UnityEngine;

// An alien that charges the player under its own power (like Boulder's
// roll), in addition to however fast the player is closing on it.
// Previously fixed at its spawn position — legs animated in place while
// only the player's own forward speed closed the distance — which read as
// "running on the spot" rather than charging, especially once the
// player's own top speed was compressed to a smaller range (see
// DifficultyManager.maxRunSpeed). Pooled and spawned like the other
// obstacles; contact kills, lane change dodges.
public class AlienObstacle : MonoBehaviour
{
    // The imported Mixamo bind pose already faces world -Z (the travel
    // direction), so no facing correction is needed at rest.
    public float playerKillRadius = 1.1f;
    // Modest compared to Boulder's rollSpeed(15) — this should read as a
    // charging humanoid, not a rolling rock. ObjectSpawner.SpawnAlien
    // accounts for this in its own spawn-distance math the same way
    // SpawnBoulder does for the boulder's roll speed.
    public float chargeSpeed = 7f;
    // How high above its own spawn ground the alien reaches at the peak
    // of a comet-hop — enough to clear a landed comet's top (~2.71, see
    // Boulder.cs) with a visible margin. A snappier gravity than the
    // boulder's (-20) so the jump reads as an agile humanoid leap rather
    // than a big rock's lazier arc.
    public float jumpApexHeight = 3.2f;
    private const float hopGravity = -35f;
    private const float hopTriggerMin = 6f;
    private const float hopTriggerPerSpeed = 0.5f;
    private float groundY;
    private float verticalVelocity = 0f;
    private bool hopping = false;
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
        groundY = transform.position.y;
        verticalVelocity = 0f;
        hopping = false;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAlienAppear();
        // Body faces 180 from the imported default so it looks toward
        // the player (its travel direction) — verified via viewpoint
        // capture + Mixamo import convention. Built from the player's
        // CURRENT heading rather than a hardcoded world angle, so it
        // still faces the right way when spawned after a 90-degree turn.
        transform.rotation = player != null
            ? Quaternion.LookRotation(-player.forward, Vector3.up)
            : Quaternion.Euler(0f, 180f, 0f);

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

    void Update()
    {
        if (player == null || isDead) return;

        // Charges toward the player under its own power, same pattern as
        // Boulder's roll — along the player's CURRENT heading rather than
        // hardcoded world -Z, so it still closes the right way down a
        // corridor after a 90-degree turn.
        float effectiveChargeSpeed = chargeSpeed * DifficultyManager.ObstacleSpeedMultiplier();

        // Jumps over a landed comet in its own path, same ballistic-hop
        // pattern as Boulder's bounce (launch velocity + gravity, not a
        // held target height) — a real leap rather than clipping through.
        if (!hopping)
        {
            float hopTrigger = Mathf.Max(
                hopTriggerMin, effectiveChargeSpeed * hopTriggerPerSpeed);
            bool cometAhead = HazardSpacing.BlockedAhead<Meteorite>(
                transform, player.forward, hopTrigger);
            if (cometAhead)
            {
                hopping = true;
                verticalVelocity = Mathf.Sqrt(2f * -hopGravity * jumpApexHeight);
            }
        }

        float newY = transform.position.y;
        if (hopping)
        {
            verticalVelocity += hopGravity * Time.deltaTime;
            newY += verticalVelocity * Time.deltaTime;
            if (newY <= groundY)
            {
                newY = groundY;
                verticalVelocity = 0f;
                hopping = false;
            }
        }

        Vector3 horizontal = -player.forward * effectiveChargeSpeed * Time.deltaTime;
        transform.position = new Vector3(
            transform.position.x + horizontal.x, newY, transform.position.z + horizontal.z);

        // Contact kill. The z window widens with the COMBINED closing
        // speed (player's own forward speed plus this alien's own charge)
        // so it can't be tunneled through at high run speeds. Local to the
        // player's current heading, so this stays correct after a
        // 90-degree turn. Skipped while mid-jump over a comet — the alien
        // passes visibly overhead, same reasoning as Boulder's own
        // overhead-kill exemption.
        Vector3 localPos = player.InverseTransformPoint(transform.position);
        float xDist = Mathf.Abs(localPos.x);
        float zDist = Mathf.Abs(localPos.z);
        float closing = ((pc != null ? pc.runSpeed : 0f) + effectiveChargeSpeed) * Time.deltaTime;
        float zWindow = Mathf.Max(playerKillRadius, closing * 0.6f);
        bool overhead = transform.position.y > groundY + 0.5f;

        if (!overhead && xDist < playerKillRadius && zDist < zWindow)
        {
            isDead = true;
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerDeath();
        }

        if (localPos.z < -destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }
}
