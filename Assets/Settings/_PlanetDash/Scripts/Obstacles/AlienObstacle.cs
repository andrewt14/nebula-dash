using UnityEngine;

// An alien fixed at its spawn position along the track. Its Mixamo run
// clip animates the legs in place, so it reads as charging even though
// only the player's own forward speed closes the distance. Pooled and
// spawned like the other obstacles; contact kills, lane change dodges.
public class AlienObstacle : MonoBehaviour
{
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

        // Fixed-position hazard (Subway Surfers / Temple Run style): the
        // alien sits at its spawn Z and runs in place — only the
        // player's forward runSpeed closes the distance. The legacy run
        // clip keeps its legs animating so it still reads as charging.

        // Contact kill. The z window widens with the player's closing
        // speed so it can't be tunneled through at high run speeds.
        // Local to the player's current heading, so this stays correct
        // after a 90-degree turn.
        Vector3 localPos = player.InverseTransformPoint(transform.position);
        float xDist = Mathf.Abs(localPos.x);
        float zDist = Mathf.Abs(localPos.z);
        float closing = (pc != null ? pc.runSpeed : 0f) * Time.deltaTime;
        float zWindow = Mathf.Max(playerKillRadius, closing * 0.6f);

        if (xDist < playerKillRadius && zDist < zWindow)
        {
            isDead = true;
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerDeath();
        }

        if (localPos.z < -destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }
}
