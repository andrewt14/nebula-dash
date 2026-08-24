using UnityEngine;

// Full-width gate: two posts at the track edges with a jagged red-orange
// electric barrier strung between them. Every other hazard is dodgeable
// by changing lane; this one spans every lane, so it forces a specific
// action instead — always a HIGH beam that only a slide clears (no
// jump-over mode — see ObjectSpawner.SpawnLaserBeam for the spawn gate).
public class LaserBeam : MonoBehaviour
{
    // Beam center height. Leaves a standing player (height ~2.0)
    // overlapping the beam while a slide clears it — tuned against the
    // baked beam quad scale (9.6 x 0.7); keep beamThickness in sync if
    // that changes.
    public float slideUnderY = 1.9f;
    public float beamThickness = 0.7f;
    public float destroyDistance = 15f;

    private Transform player;
    private PlayerController pc;
    private Transform beamQuad;
    private bool isDead = false;

    void Awake()
    {
        player = GameObject.Find("Player").transform;
        pc = FindObjectOfType<PlayerController>();
        beamQuad = transform.Find("BeamQuad");

        CreateLight();
    }

    // Orange-red point light at the beam so it throws actual light onto
    // the track around it as it approaches through the fog.
    void CreateLight()
    {
        GameObject lightObj = new GameObject("BeamLight");
        lightObj.transform.parent = beamQuad;
        lightObj.transform.localPosition = Vector3.zero;
        Light light = lightObj.AddComponent<Light>();
        light.color = new Color(1f, 0.15f, 0.08f);
        light.intensity = 9f;
        light.range = 11f;
    }

    void OnEnable()
    {
        // Reset per-spawn state so pooled instances behave like new ones.
        isDead = false;

        if (beamQuad != null)
            beamQuad.localPosition = new Vector3(0f, slideUnderY, 0f);
    }

    void Update()
    {
        if (player == null || isDead) return;

        // Beam is full-width so there is no lateral escape; only the
        // forward window matters. Widen it with per-frame player movement
        // so a fast frame can't tunnel clean through the thin beam.
        Vector3 localPos = player.InverseTransformPoint(transform.position);
        float frameStep = pc != null ? pc.runSpeed * Time.deltaTime : 0f;
        float zWindow = Mathf.Max(0.8f, frameStep);
        if (Mathf.Abs(localPos.z) < zWindow)
            TryKill();

        if (localPos.z < -destroyDistance)
            ObjectPool.Instance.Return(gameObject);
    }

    // The trigger BoxCollider on the beam quad fires this too — both the
    // per-frame sweep (tunneling-proof) and the collider gate here, and
    // isDead latches so one pass can only ever trigger death once.
    void OnTriggerStay(Collider other)
    {
        if (isDead) return;
        if (player == null || other.transform != player) return;
        TryKill();
    }

    void TryKill()
    {
        // Only a slide clears this — jumping does not.
        if (pc != null && pc.isSliding) return;

        // Gate on GameManager's own invincible/game-over state before
        // latching isDead — see AlienWall for why this matters.
        bool willKill = GameManager.Instance != null &&
            !GameManager.Instance.isGameOver &&
            !GameManager.Instance.isInvincible;
        if (willKill)
        {
            isDead = true;
            GameManager.Instance.TriggerDeath();
        }
    }
}
