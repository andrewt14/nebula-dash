using UnityEngine;

// Late-game hazard: hovers ahead and slides side to side across the
// lanes, so the "safe" lane keeps shifting and the player must read its
// motion and commit a lane change late. Pooled like the other
// obstacles; the only dodge is a lane change (no slide/jump escape).
public class StrafingObstacle : MonoBehaviour
{
    public float hoverHeight = 1.4f;
    public float strafeSpeed = 3.5f;
    public float strafeRange = 2.5f;        // matches outer lane x
    public float playerKillRadius = 1.1f;
    private Transform player;
    private PlayerController pc;
    private float destroyDistance = 20f;
    private int dir = 1;
    // Cached at spawn — the corridor's lateral axis at that moment, so a
    // strafe stays a clean side-to-side slide even if the player turns
    // 90 degrees while this instance is still alive.
    private Vector3 strafeAxis = Vector3.right;
    private Vector3 spawnCenter;
    private float xOffset = 0f;

    void Awake()
    {
        player = GameObject.Find("Player").transform;
        pc = FindObjectOfType<PlayerController>();

        GameObject lightObj = new GameObject("StrafeLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        Light l = lightObj.AddComponent<Light>();
        l.color = Color.white;
        l.intensity = 5.5f;
        l.range = 7f;

        CreateGlowPass();
    }

    // Additive fresnel rim glow over the hazard's own material — same
    // silhouette, reads as a bright white hazard from any angle.
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
        // Reset per-spawn state so pooled instances behave like new.
        transform.position = new Vector3(
            transform.position.x, hoverHeight, transform.position.z);
        dir = Random.value < 0.5f ? -1 : 1;
        xOffset = 0f;
        spawnCenter = transform.position;
        strafeAxis = player != null
            ? player.right : Vector3.right;
    }

    void Update()
    {
        if (player == null) return;

        // Slide across the lanes (along the corridor's lateral axis at
        // spawn time), bouncing at the edges.
        xOffset += strafeSpeed * dir * Time.deltaTime;
        if (xOffset > strafeRange) { xOffset = strafeRange; dir = -1; }
        else if (xOffset < -strafeRange) { xOffset = -strafeRange; dir = 1; }
        Vector3 pos = spawnCenter + strafeAxis * xOffset;
        pos.y = hoverHeight;
        transform.position = pos;

        // Contact kill. z window widens with run speed so it can't be
        // tunneled through at high speeds, matching the other obstacles.
        // Local to the player's current heading, so this stays correct
        // after a 90-degree turn.
        Vector3 localPos = player.InverseTransformPoint(transform.position);
        float xDist = Mathf.Abs(localPos.x);
        float zDist = Mathf.Abs(localPos.z);
        float frameStep = pc != null ? pc.runSpeed * Time.deltaTime : 0f;
        float zWindow = Mathf.Max(playerKillRadius, frameStep * 0.6f);

        if (xDist < playerKillRadius && zDist < zWindow)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerDeath();
        }

        if (localPos.z < -destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }
}
