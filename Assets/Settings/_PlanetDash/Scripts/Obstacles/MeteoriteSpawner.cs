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
        // Same post-death gate as ObjectSpawner — a stationary player
        // means spawned comets never get passed and returned to the pool.
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;
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
    // Same off-screen floor every other spawner uses — at the old 70 units
    // the comet materialized at ~95% fog visibility, i.e. right in front of
    // the player. See ObjectSpawner.MinSpawnDistance.
    float dynamicDistance = pc != null
        ? Mathf.Max(ObjectSpawner.MinSpawnDistance, pc.runSpeed * minReactionTime)
        : ObjectSpawner.MinSpawnDistance;

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
    // player.position already includes the player's own in-lane strafe
    // offset, so it has to be subtracted back out here — otherwise it
    // stacks with the absolute lane offset (xPos) added below and pushes
    // spawns outside the real lane positions.
    float currentOffset = pc != null ? pc.GetCurrentLaneOffset() : 0f;
    // dynamicDistance can run well past a pending turn's pivot at high
    // speed (runSpeed * 2.5s of reaction time) — clamp it the same way
    // ObjectSpawner does, so a comet can't land beyond the corner.
    if (GroundTileSpawner.Instance != null)
        dynamicDistance = GroundTileSpawner.Instance.ClampAheadForTurn(
            player.position, fwd, dynamicDistance);
    Vector3 spawnPosNoLane = player.position - right * currentOffset + fwd * dynamicDistance;
    if (GroundTileSpawner.Instance != null &&
        GroundTileSpawner.Instance.IsObstacleSpawnSuppressed(spawnPosNoLane)) return;
    if (GroundTileSpawner.IsInsidePit(spawnPosNoLane)) return;

    int lane = LaneSpacingManager.Instance.PickLane(spawnPosNoLane.z);

    float xPos = lanePositions[lane];
    Vector3 spawnPos = spawnPosNoLane + right * xPos;
    spawnPos.y = spawnHeight;

    // Same ground-hazard occupancy check ObjectSpawner uses for
    // boulders/walls/aliens — without it, a comet can land on top of
    // another hazard already sitting in that lane once spawn intervals
    // get tight at high difficulty. Geometry-aware overload (real
    // rendered bounds vs. a flat guessed tolerance) — this spawner was
    // still on the old BlockedNear(pos, fwd) overload, missed when
    // ObjectSpawner's call sites were switched over.
    if (HazardSpacing.BlockedNear<Boulder>(spawnPos, fwd, meteoritePrefab)
        || HazardSpacing.BlockedNear<AlienWall>(spawnPos, fwd, meteoritePrefab)
        || HazardSpacing.BlockedNear<AlienObstacle>(spawnPos, fwd, meteoritePrefab)
        || HazardSpacing.BlockedNear<Meteorite>(spawnPos, fwd, meteoritePrefab)
        || HazardSpacing.BlockedNear<UFOObstacle>(spawnPos, fwd, meteoritePrefab)
        // Flat 5-unit tolerance instead of the geometry-aware overload, same
        // reason as ObjectSpawner.IsHazardOccupied: a live strafer sweeps
        // ±strafeRange (reach ~3.2 incl. half-width), so its instantaneous
        // footprint understates where it will be moments later. A comet
        // landing near a swept-to-one-side strafer would otherwise end up in
        // a lane the strafer sweeps into.
        || HazardSpacing.BlockedNear<StrafingObstacle>(spawnPos, fwd, 5f, 3f)
        || HazardSpacing.BlockedNear<LavaCrack>(spawnPos, fwd, meteoritePrefab)
        // Comet vs. orbs: ObjectSpawner's IsHazardOccupied checks orbs, but
        // this spawner never did, so a comet could land directly on a
        // resource/power-up orb sitting in that lane. Orbs are small (0.15
        // half-width) and meteors land flat at the spawn x/z, so the
        // geometry-aware check against them is cheap and exact.
        || HazardSpacing.BlockedNear<ResourceOrb>(spawnPos, fwd, meteoritePrefab)
        || HazardSpacing.BlockedNear<GoldOrb>(spawnPos, fwd, meteoritePrefab)
        || HazardSpacing.BlockedNear<MagnetOrb>(spawnPos, fwd, meteoritePrefab)
        || HazardSpacing.BlockedNear<InvincibilityOrb>(spawnPos, fwd, meteoritePrefab))
        return;

    ObjectPool.Instance.Get(meteoritePrefab, spawnPos, Random.rotation);
}
}