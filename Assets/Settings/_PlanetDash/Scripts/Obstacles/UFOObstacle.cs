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

        // Kill if player hits it while not sliding
        float dist = Vector3.Distance(
            transform.position, player.position);
        if (dist < playerKillRadius)
        {
            PlayerController pc =
                FindObjectOfType<PlayerController>();
            if (pc != null && !pc.isSliding)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.TriggerDeath();
            }
        }

        // Destroy when behind player
if (transform.position.z < player.position.z - destroyDistance)
    ObjectPool.Instance.Return(gameObject);
    }
}