using UnityEngine;

// Occasional cosmetic flourishes with zero gameplay impact — in the
// spirit of Subway Surfers' background spectacle: rare enough to stay a
// treat instead of visual noise, cheap enough to run for a whole run
// without a profiler regression. Nothing here can kill the player, block
// a lane, or touch difficulty/spawn timing.
public class AmbientEffects : MonoBehaviour
{
    [Header("Shooting Stars")]
    public Transform player;
    public float shootingStarMinInterval = 18f;
    public float shootingStarMaxInterval = 35f;
    private float shootingStarTimer;

    [Header("Score Milestones")]
    public float milestoneStep = 2500f;
    private static readonly Color MilestoneColor = new Color(1f, 0.85f, 0.3f);
    private float nextMilestone;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        RollShootingStarTimer();
        nextMilestone = milestoneStep;
    }

    void RollShootingStarTimer()
    {
        shootingStarTimer = Random.Range(shootingStarMinInterval, shootingStarMaxInterval);
    }

    void Update()
    {
        if (player == null) return;

        shootingStarTimer -= Time.deltaTime;
        if (shootingStarTimer <= 0f)
        {
            RollShootingStarTimer();
            SpawnShootingStar();
        }

        // Score only advances while the player is alive (DifficultyManager
        // gates its own Update the same way), so this naturally stops
        // firing on its own once a run ends — no extra isAlive check needed.
        if (DifficultyManager.Instance != null &&
            DifficultyManager.Instance.score >= nextMilestone)
        {
            CelebrateMilestone();
            nextMilestone += milestoneStep;
        }
    }

    // A bright streak crossing high overhead, always spawned relative to
    // the player so it's roughly in view regardless of how far the run
    // has travelled or which way the corridor has turned. No collider —
    // this can't be dodged or hit, purely a background beat.
    void SpawnShootingStar()
    {
        // Subtle static crackle synced to the visual — see
        // AudioManager.PlayStaticPop for why this replaced the earlier
        // sustained ambient drone.
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayStaticPop();

        GameObject star = new GameObject("ShootingStar");
        Vector3 lateral = player.right * Random.Range(-1f, 1f) * 45f;
        Vector3 start = player.position
            + player.forward * Random.Range(70f, 110f)
            + Vector3.up * Random.Range(45f, 70f)
            + lateral;
        star.transform.position = start;

        TrailRenderer trail = star.AddComponent<TrailRenderer>();
        trail.time = 0.4f;
        trail.startWidth = 0.5f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;

        star.AddComponent<ShootingStarMotion>().Init(player.forward, player.right);
    }

    // A quick, distinctly-colored celebration beat every milestoneStep
    // points — reuses the existing popup/shake/score-pulse primitives
    // (same ones the near-miss "CLOSE! +100" reward already uses) instead
    // of adding a second parallel juice system.
    void CelebrateMilestone()
    {
        int shown = Mathf.RoundToInt(nextMilestone);
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowPopup(
                shown + "!", player.position + Vector3.up * 2.2f,
                1.2f, MilestoneColor);
        if (ScreenShake.Instance != null)
            ScreenShake.Instance.Shake(0.12f, 0.05f);
        if (DifficultyManager.Instance != null)
            DifficultyManager.Instance.PulseScore();

        CameraFollow cam = Camera.main != null
            ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cam != null)
            cam.CelebrationPulse();
    }
}

// Straight-line ballistic-looking streak (no gravity, just a fast
// constant velocity) for the shooting star's TrailRenderer to draw
// behind. Self-destroys once its short life is up.
public class ShootingStarMotion : MonoBehaviour
{
    private Vector3 velocity;
    private float life = 1f;

    public void Init(Vector3 forward, Vector3 right)
    {
        Vector3 dir = (-forward * 1.5f
            + right * Random.Range(-1f, 1f)
            - Vector3.up * 0.6f).normalized;
        velocity = dir * Random.Range(55f, 75f);
    }

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }
}
