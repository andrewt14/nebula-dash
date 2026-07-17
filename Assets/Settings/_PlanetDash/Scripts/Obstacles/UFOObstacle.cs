using UnityEngine;

public class UFOObstacle : MonoBehaviour
{
    public float hoverHeight = 1.5f;
    public float bobSpeed = 1f;
    public float bobAmount = 0.2f;
    public float playerKillRadius = 1f;
    private Transform player;
    private float destroyDistance = 20f;
    private float startY;

    void Awake()
    {
        player = GameObject.Find("Player").transform;
        startY = hoverHeight;
    }

    void OnEnable()
    {
        // Reset per-spawn state so pooled instances behave like new ones.
        transform.position = new Vector3(
            transform.position.x,
            hoverHeight,
            transform.position.z);
    }

    void Update()
    {
        if (player == null) return;

        // Hover bob
        float newY = startY + Mathf.Sin(
            Time.time * bobSpeed) * bobAmount;
        transform.position = new Vector3(
            transform.position.x,
            newY,
            transform.position.z);

        // Kill if player hits it while not sliding and not airborne —
        // was missing the isGrounded check entirely, so jumping over
        // the UFO (hoverHeight 1.5, well under a jump's ~2.0 apex)
        // never actually worked as a dodge.
        float dist = Vector3.Distance(
            transform.position, player.position);
        if (dist < playerKillRadius)
        {
            PlayerController pc =
                FindObjectOfType<PlayerController>();
            if (pc != null && !pc.isSliding && pc.isGrounded)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.TriggerDeath();
            }
        }

        // Destroy when behind player (local to current heading, so this
        // stays correct after a 90-degree turn).
if (player.InverseTransformPoint(transform.position).z < -destroyDistance)
    ObjectPool.Instance.Return(gameObject);
    }
}