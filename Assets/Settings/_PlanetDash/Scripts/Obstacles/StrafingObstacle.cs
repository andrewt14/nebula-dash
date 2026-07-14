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
    }

    void Update()
    {
        if (player == null) return;

        // Slide across the lanes, bouncing at the edges.
        float x = transform.position.x + strafeSpeed * dir * Time.deltaTime;
        if (x > strafeRange) { x = strafeRange; dir = -1; }
        else if (x < -strafeRange) { x = -strafeRange; dir = 1; }
        transform.position = new Vector3(
            x, hoverHeight, transform.position.z);

        // Contact kill. z window widens with run speed so it can't be
        // tunneled through at high speeds, matching the other obstacles.
        float xDist = Mathf.Abs(transform.position.x - player.position.x);
        float zDist = Mathf.Abs(transform.position.z - player.position.z);
        float frameStep = pc != null ? pc.runSpeed * Time.deltaTime : 0f;
        float zWindow = Mathf.Max(playerKillRadius, frameStep * 0.6f);

        if (xDist < playerKillRadius && zDist < zWindow)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerDeath();
        }

        if (transform.position.z < player.position.z - destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }
}
