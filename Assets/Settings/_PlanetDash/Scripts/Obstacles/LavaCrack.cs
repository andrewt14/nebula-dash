using UnityEngine;

public class LavaCrack : MonoBehaviour
{
    public float playerKillRadius = 0.6f;
    private Transform player;
    private float pulseSpeed = 2f;
    private Renderer rend;
    private float destroyDistance = 15f;

    void Start()
    {
        player = GameObject.Find("Player").transform;
        rend = GetComponent<Renderer>();
        GameObject lightObj = new GameObject("LavaLight");
lightObj.transform.parent = transform;
lightObj.transform.localPosition = Vector3.zero;
Light light = lightObj.AddComponent<Light>();
light.color = new Color(1f, 0.3f, 0f);
light.intensity = 5f;
light.range = 4f;
    }

void Update()
{
    if (player == null) return;

    float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
    // Add in Update after pulse calculation
Light lavaLight = GetComponentInChildren<Light>();
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
        PlayerController pc =
            FindObjectOfType<PlayerController>();
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