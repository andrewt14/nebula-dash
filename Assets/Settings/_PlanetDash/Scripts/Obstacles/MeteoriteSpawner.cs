using UnityEngine;

public class MeteoriteSpawner : MonoBehaviour
{
    public GameObject meteoritePrefab;
    public Transform player;
    public float spawnInterval = 2f;
    public float spawnHeight = 20f;
    public float spawnDistance = 40f;
    public float[] lanePositions = { -4f, 0f, 4f };
    private float spawnTimer;
    private PlayerController pc;

    void Update()
    {
        if (player == null) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnMeteorite();
            // ±25% jitter so strikes don't land on a perfectly even
            // metronome — anti-overlap guards below are unaffected.
            spawnTimer = spawnInterval * Random.Range(0.75f, 1.25f);
        }
    }

void SpawnMeteorite()
{
    if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

    // A flat spawnDistance gives a different amount of real reaction
    // time depending on player speed — same fix as the alien wall's
    // spawn distance: guarantee a minimum number of seconds of warning
    // instead of a fixed world-space distance.
    if (pc == null) pc = player.GetComponent<PlayerController>();
    float minReactionTime = 2.5f;
    float dynamicDistance = pc != null
        ? Mathf.Max(spawnDistance, pc.runSpeed * minReactionTime)
        : spawnDistance;

    float spawnZ = player.position.z + dynamicDistance;
    if (GroundTileSpawner.IsInsidePit(spawnZ)) return;

    int lane = LaneSpacingManager.Instance.PickLane(spawnZ);

    float xPos = lanePositions[lane];
    Vector3 spawnPos = new Vector3(xPos, spawnHeight, spawnZ);

    // Same ground-hazard occupancy check ObjectSpawner uses for
    // boulders/walls/aliens — without it, a comet can land on top of
    // another hazard already sitting in that lane once spawn intervals
    // get tight at high difficulty.
    if (HazardSpacing.BlockedNear<Boulder>(spawnPos)
        || HazardSpacing.BlockedNear<AlienWall>(spawnPos)
        || HazardSpacing.BlockedNear<AlienObstacle>(spawnPos)
        || HazardSpacing.BlockedNear<Meteorite>(spawnPos))
        return;

    ObjectPool.Instance.Get(meteoritePrefab, spawnPos, Random.rotation);
}
}