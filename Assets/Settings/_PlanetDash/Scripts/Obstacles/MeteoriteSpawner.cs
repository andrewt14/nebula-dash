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

    // Spawn position is built from the player's CURRENT heading
    // (forward/right) instead of hardcoded world Z/X, so hazards still
    // land ahead of the player, in the correct lane, after a 90-degree
    // turn. LaneSpacingManager's spawnZ param still takes the resulting
    // world Z — its safe-lane rotation goes a bit stale after a turn
    // (world Z stops advancing while moving along X), a minor pacing
    // simplification rather than plumbing a second heading-agnostic
    // distance scalar through it.
    Vector3 fwd = player.forward;
    Vector3 right = player.right;
    Vector3 spawnPosNoLane = player.position + fwd * dynamicDistance;
    if (GroundTileSpawner.IsInsidePit(spawnPosNoLane.z)) return;

    int lane = LaneSpacingManager.Instance.PickLane(spawnPosNoLane.z);

    float xPos = lanePositions[lane];
    Vector3 spawnPos = spawnPosNoLane + right * xPos;
    spawnPos.y = spawnHeight;

    // Same ground-hazard occupancy check ObjectSpawner uses for
    // boulders/walls/aliens — without it, a comet can land on top of
    // another hazard already sitting in that lane once spawn intervals
    // get tight at high difficulty.
    if (HazardSpacing.BlockedNear<Boulder>(spawnPos, fwd)
        || HazardSpacing.BlockedNear<AlienWall>(spawnPos, fwd)
        || HazardSpacing.BlockedNear<AlienObstacle>(spawnPos, fwd)
        || HazardSpacing.BlockedNear<Meteorite>(spawnPos, fwd))
        return;

    ObjectPool.Instance.Get(meteoritePrefab, spawnPos, Random.rotation);
}
}