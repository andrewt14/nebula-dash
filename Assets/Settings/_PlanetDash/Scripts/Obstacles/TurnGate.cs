using UnityEngine;

// Late-game "forced turn" hazard (Temple Run style): a full-width arch
// that can only be cleared by swiping in the direction it calls out —
// jumping, sliding, or a lane change does nothing, since it spans every
// lane at head height. Announces its required direction well ahead of
// time (top banner + a matching color tint) so the swipe reads as a
// deliberate call-and-response, not a guess.
public class TurnGate : MonoBehaviour
{
    public float playerKillRadius = 2f;
    // Reaction window (seconds of travel at the player's current speed)
    // during which a matching swipe still counts — matches the pattern
    // used for the other late-telegraphed hazards.
    public float reactionTime = 3.5f;

    private static readonly Color LeftColor = new Color(0.15f, 0.55f, 1f);
    private static readonly Color RightColor = new Color(1f, 0.55f, 0.1f);

    private int requiredDirection; // -1 left, +1 right
    private bool cleared = false;
    private bool isDead = false;
    private Transform player;
    private PlayerController pc;
    private float destroyDistance = 15f;
    private Renderer[] renderers;

    void Awake()
    {
        player = GameObject.Find("Player").transform;
        pc = FindObjectOfType<PlayerController>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable()
    {
        isDead = false;
        cleared = false;
        requiredDirection = Random.value < 0.5f ? -1 : 1;

        Color tint = requiredDirection < 0 ? LeftColor : RightColor;
        foreach (Renderer r in renderers)
        {
            if (r.material.HasProperty("_BaseColor"))
                r.material.SetColor("_BaseColor", tint);
            if (r.material.HasProperty("_EmissionColor"))
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", tint * 2.2f);
            }
        }

        PlayerController.OnSwipeDirection += HandleSwipe;

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowTopBanner(
                requiredDirection < 0 ? "< SWIPE LEFT" : "SWIPE RIGHT >",
                1.6f, tint);
    }

    void OnDisable()
    {
        PlayerController.OnSwipeDirection -= HandleSwipe;
    }

    void HandleSwipe(int dir)
    {
        if (cleared || isDead || player == null || dir != requiredDirection) return;

        float zAhead = transform.position.z - player.position.z;
        float speed = pc != null ? pc.runSpeed : 15f;
        float window = Mathf.Max(20f, speed * reactionTime);
        if (zAhead > 0f && zAhead < window)
            cleared = true;
    }

    void Update()
    {
        if (player == null) return;

        // Only latch isDead on a kill that actually lands — gating on
        // GameManager's own invincible/game-over state first (rather
        // than setting isDead unconditionally) means an invincible pass
        // through the gate doesn't leave Update() permanently bailing
        // out on the isDead check below, which would strand the pooled
        // instance active forever instead of ever reaching its own
        // despawn-when-passed check.
        float zDist = transform.position.z - player.position.z;
        if (!cleared && !isDead && zDist < playerKillRadius && zDist > -playerKillRadius)
        {
            bool willKill = GameManager.Instance != null &&
                !GameManager.Instance.isGameOver &&
                !GameManager.Instance.isInvincible;
            if (willKill)
            {
                isDead = true;
                GameManager.Instance.TriggerDeath();
            }
        }

        if (transform.position.z < player.position.z - destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }
}
