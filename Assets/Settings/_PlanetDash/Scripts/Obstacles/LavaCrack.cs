using UnityEngine;

public class LavaCrack : MonoBehaviour
{
    public float playerKillRadius = 0.6f;
    private Transform player;
    private float pulseSpeed = 2f;
    private Renderer rend;
    private float destroyDistance = 15f;
    // Cached here rather than re-fetched per frame: Update ran a
    // GetComponentInChildren<Light>() and a FindObjectOfType<PlayerController>()
    // on every crack every frame, both of which walk the hierarchy/scene.
    private Light lavaLight;
    private PlayerController pc;

    void Start()
    {
        player = GameObject.Find("Player").transform;
        pc = player != null ? player.GetComponent<PlayerController>() : null;
        rend = GetComponent<Renderer>();
        GameObject lightObj = new GameObject("LavaLight");
lightObj.transform.parent = transform;
lightObj.transform.localPosition = Vector3.zero;
lavaLight = lightObj.AddComponent<Light>();
lavaLight.color = new Color(1f, 0.3f, 0f);
lavaLight.intensity = 5f;
lavaLight.range = 4f;
    }

void Update()
{
    if (player == null) return;

    float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
    if (lavaLight != null)
        lavaLight.intensity = 3f + pulse * 4f;
    rend.material.SetColor("_EmissionColor",
        new Color(1f, 0.3f, 0f) * (3f + pulse * 2f));

    // Check forward distance only (crack spans full width) — local to
    // the player's current heading so this stays correct after a
    // 90-degree turn.
    float zDist = Mathf.Abs(player.InverseTransformPoint(transform.position).z);
    if (zDist < 1f)
    {
        if (pc != null && pc.isGrounded)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerDeath();
        }
    }

    if (player.InverseTransformPoint(transform.position).z < -destroyDistance)
        ObjectPool.Instance.Return(gameObject);
}
}