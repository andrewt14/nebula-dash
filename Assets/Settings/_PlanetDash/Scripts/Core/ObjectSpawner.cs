using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Orb Spawning")]
    public GameObject orbPrefab;
    public Transform player;
    public float spawnDistance = 30f;
    public float spawnInterval = 1.5f;
    public float destroyDistance = 10f;
    public float[] lanePositions = { -4f, 0f, 4f };

    [Header("Lava Crack Spawning")]
    public GameObject lavaCrackPrefab;
    public float lavaCrackInterval = 5f;
    private float lavaCrackTimer = 3f;

    [Header("Boulder Spawning")]
    public GameObject boulderPrefab;
    public float boulderInterval = 6f;
    public float boulderSpawnDistance = 50f;
    private float boulderTimer = 5f;

    [Header("UFO Spawning")]
    public GameObject ufoPrefab;
    public float ufoInterval = 9f;
    private float ufoTimer = 8f;

[Header("Alien Wall Spawning")]
public GameObject alienWallPrefab;
public float alienWallInterval = 10f;
public float alienWallSpawnDistance = 75f;
// Give the player a few seconds to get their bearings before the first
// wall can appear — previously it could spawn right at run start with
// too little warning to react to.
public float alienWallUnlockTime = 8f;
private float alienWallTimer = 10f;

[Header("Strafing Hazard (late game)")]
public GameObject strafingPrefab;
public float strafingInterval = 6f;
public float strafingUnlockTime = 90f;      // seconds of play before it appears
private float strafingTimer = 6f;

[Header("Turn Gate (forced left/right swipe, late game)")]
public GameObject turnGatePrefab;
public float turnGateInterval = 14f;
public float turnGateSpawnDistance = 60f;
// Its own dedicated late unlock — this is the hardest read in the game
// (a full-width barrier that only a matching swipe clears), so it
// shouldn't show up until the player has clearly settled into the
// core loop.
public float turnGateUnlockTime = 100f;
private float turnGateTimer = 10f;

[Header("Alien Runner")]
public GameObject alienRunnerPrefab;
public float alienInterval = 8f;
public float alienSpawnDistance = 70f;
// Aliens are a mid-run surprise, not a from-the-start obstacle.
public float alienUnlockTime = 30f;         // seconds of play before they appear
private float alienTimer = 8f;
// Keeps comets/boulders/aliens from landing on top of each other. Alien
// bypasses globalObstacleCooldown (boulder resets it too often for alien
// to spawn reliably), so all three big hazards share this dedicated gap
// instead.
public float hazardGap = 6.5f;
private float hazardCooldown = 0f;

[Header("Formations (Subway-Surfers style multi-lane blocks)")]
public float formationIntervalEarly = 22f;
public float formationIntervalLate = 10f;
private float formationTimer = 18f;

    private float spawnTimer;
    private float globalObstacleCooldown = 2f;
    private float wallCooldown = 3f;

    // Every spawn timer reset below is multiplied by this — fixed
    // intervals made spawns land on a perfectly even metronome, which
    // reads as predictable/scripted rather than randomized. The existing
    // anti-overlap guards (IsHazardOccupied, HazardSpacing, hazardCooldown,
    // globalObstacleCooldown, LaneSpacingManager) are untouched, so this
    // only varies timing, never causes hazards to actually collide.
    float Jitter() => Random.Range(0.75f, 1.25f);
[Header("Power Ups")]
public GameObject magnetOrbPrefab;
public GameObject goldOrbPrefab;
public float magnetInterval = 20f;
private float magnetTimer = 20f;
// Real-time gating: score accelerates with difficulty, so anything
// score-based ends up spawning gold orbs every couple of seconds
// late game. One roll per interval keeps the rate steady all run.
public float goldOrbInterval = 30f;
[Range(0f, 1f)] public float goldOrbChance = 0.5f;
private float goldOrbTimer = 30f;

// Navy invincibility orb: locked out until well into the run and
// extremely rare even after that — a genuine rare-drop, not a regular
// power-up.
public GameObject invincibilityOrbPrefab;
public float invincibilityUnlockDistance = 2500f;
public float invincibilityInterval = 45f;
[Range(0f, 1f)] public float invincibilityChance = 0.08f;
private float invincibilityTimer = 45f;

    void Update()
    {

// Magnet orb every 20 seconds
magnetTimer -= Time.deltaTime;
if (magnetTimer <= 0f)
{
    SpawnMagnetOrb();
    magnetTimer = magnetInterval;
}

// Gold orb: one 50% roll every 30 seconds
goldOrbTimer -= Time.deltaTime;
if (goldOrbTimer <= 0f)
{
    goldOrbTimer = goldOrbInterval;
    if (Random.value < goldOrbChance)
        SpawnGoldOrb();
}

// Navy invincibility orb: rare roll, and only once the player has
// covered enough distance.
if (player != null && player.position.z >= invincibilityUnlockDistance)
{
    invincibilityTimer -= Time.deltaTime;
    if (invincibilityTimer <= 0f)
    {
        invincibilityTimer = invincibilityInterval;
        if (Random.value < invincibilityChance)
            SpawnInvincibilityOrb();
    }
}
        if (player == null) return;

        if (globalObstacleCooldown > 0f)
            globalObstacleCooldown -= Time.deltaTime;

        if (wallCooldown > 0f)
            wallCooldown -= Time.deltaTime;

        if (hazardCooldown > 0f)
            hazardCooldown -= Time.deltaTime;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnOrb();
            spawnTimer = spawnInterval * Jitter();
        }

        lavaCrackTimer -= Time.deltaTime;
        if (lavaCrackTimer <= 0f)
        {
            SpawnLavaCrack();
            lavaCrackTimer = lavaCrackInterval;
        }

boulderTimer -= Time.deltaTime;
if (boulderTimer <= 0f && globalObstacleCooldown <= 0f && hazardCooldown <= 0f)
{
    SpawnBoulder();
    boulderTimer = boulderInterval * Jitter();
    globalObstacleCooldown = 5f;
    hazardCooldown = hazardGap;

}

        ufoTimer -= Time.deltaTime;
        if (ufoTimer <= 0f && globalObstacleCooldown <= 0f && hazardCooldown <= 0f)
        {
            SpawnUFO();
            ufoTimer = ufoInterval * Jitter();
            globalObstacleCooldown = 3f;
            hazardCooldown = hazardGap;
        }

        // Wall uses its own cooldown
        bool wallUnlocked = DifficultyManager.Instance == null ||
            DifficultyManager.Instance.runTime >= alienWallUnlockTime;
        alienWallTimer -= Time.deltaTime;
        if (wallUnlocked && alienWallTimer <= 0f && wallCooldown <= 0f)
        {
            SpawnAlienWall();
            alienWallTimer = alienWallInterval * Jitter();
            wallCooldown = 3f;
        }

        // Strafing hazard unlocks only in the late game
        if (strafingPrefab != null &&
            DifficultyManager.Instance != null &&
            DifficultyManager.Instance.runTime >= strafingUnlockTime)
        {
            strafingTimer -= Time.deltaTime;
            if (strafingTimer <= 0f && globalObstacleCooldown <= 0f)
            {
                SpawnStrafing();
                strafingTimer = strafingInterval * Jitter();
                globalObstacleCooldown = 4f;
            }
        }

        // Turn gate: the hardest read in the game, so it gets the latest
        // unlock and its own timer (a full-width barrier already commands
        // the whole track — it doesn't need to fight the shared cooldown
        // against boulders/UFOs to feel fair).
        if (turnGatePrefab != null &&
            DifficultyManager.Instance != null &&
            DifficultyManager.Instance.runTime >= turnGateUnlockTime)
        {
            turnGateTimer -= Time.deltaTime;
            if (turnGateTimer <= 0f)
            {
                SpawnTurnGate();
                turnGateTimer = turnGateInterval * Jitter();
            }
        }

        // Alien runner unlocks after a short warm-up. Uses its own timer
        // (not the shared globalObstacleCooldown, which the boulder keeps
        // resetting) so it spawns reliably.
        if (alienRunnerPrefab != null &&
            DifficultyManager.Instance != null &&
            DifficultyManager.Instance.runTime >= alienUnlockTime)
        {
            alienTimer -= Time.deltaTime;
            if (alienTimer <= 0f && hazardCooldown <= 0f)
            {
                SpawnAlien();
                alienTimer = alienInterval * Jitter();
                hazardCooldown = hazardGap;
            }
        }

        // Formation events: coordinated multi-lane blocks layered on top
        // of the ambient per-type spawners, progressively unlocked and
        // complexified with difficulty (Subway-Surfers style pacing).
        if (DifficultyManager.Instance != null)
        {
            float d = DifficultyManager.Instance.currentDifficulty;
            formationTimer -= Time.deltaTime;
            if (formationTimer <= 0f)
            {
                formationTimer = Mathf.Lerp(
                    formationIntervalEarly, formationIntervalLate,
                    Mathf.Clamp01(d / 160f));
                TryFormation(d);
            }
        }

        CleanupBehindPlayer();
    }

    // Spawns a coordinated set of obstacles that simultaneously block
    // one or two lanes at the same z, leaving the rest open — a
    // deliberate "pick your lane" moment rather than independent random
    // spawns. Reuses the exact same pooled prefabs/spawn calls as the
    // ambient spawners, so pooling stays intact.
    void TryFormation(float difficulty)
    {
        if (LaneSpacingManager.Instance == null) return;

        float spawnZ = player.position.z + boulderSpawnDistance;
        // Wide margin since this can place an alien wall, which rises up
        // from below y=0 and would show through a nearby pit.
        if (GroundTileSpawner.IsInsidePit(spawnZ, 25f)) return;
        int[] blockedLanes = LaneSpacingManager.Instance
            .GetFormationBlockedLanes(difficulty, spawnZ);
        if (blockedLanes == null || blockedLanes.Length == 0) return;

        System.Collections.Generic.List<GameObject> choices =
            new System.Collections.Generic.List<GameObject>();
        if (boulderPrefab != null) choices.Add(boulderPrefab);
        if (alienWallPrefab != null) choices.Add(alienWallPrefab);
        if (alienRunnerPrefab != null && DifficultyManager.Instance != null &&
            DifficultyManager.Instance.runTime >= alienUnlockTime)
            choices.Add(alienRunnerPrefab);
        if (choices.Count == 0) return;

        foreach (int lane in blockedLanes)
        {
            GameObject prefab = choices[Random.Range(0, choices.Count)];
            float yPos = prefab == alienWallPrefab ? -3f :
                         prefab == boulderPrefab ? 0.2f : 0f;
            Vector3 pos = new Vector3(lanePositions[lane], yPos, spawnZ);
            // Same occupancy guard the ambient per-type spawners use —
            // without it a formation could drop a wall or alien directly
            // on top of a boulder (or vice versa) that another spawner
            // already placed nearby in that lane.
            if (IsHazardOccupied(pos)) continue;
            ObjectPool.Instance.Get(prefab, pos, Quaternion.identity);
        }
    }

    void SpawnOrb()
    {
        int orbCount = Random.Range(1, 3);
        for (int i = 0; i < orbCount; i++)
        {
            int lane = Random.Range(0, 3);
            float xPos = lanePositions[lane];
            float yPos = Random.Range(1.4f, 2f);
            float zOffset = Random.Range(8f, 20f);
            Vector3 spawnPos = new Vector3(
                xPos, yPos,
                player.position.z + spawnDistance + zOffset);
            Instantiate(orbPrefab, spawnPos, Quaternion.identity);
        }
    }

    void SpawnLavaCrack()
    {
        if (lavaCrackPrefab == null) return;
        Vector3 spawnPos = new Vector3(
            0f, 0.1f,
            player.position.z + spawnDistance);
        ObjectPool.Instance.Get(lavaCrackPrefab, spawnPos, Quaternion.identity);
    }

    void SpawnBoulder()
    {
        if (boulderPrefab == null) return;
        if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

        // A flat boulderSpawnDistance gives less and less real warning as
        // run speed climbs. The boulder also rolls toward the player
        // under its own power, so the actual closing speed is the
        // player's runSpeed PLUS the boulder's own roll speed — using
        // only runSpeed here (as if the boulder were stationary) would
        // silently shrink the real reaction window as difficulty/storms
        // push the boulder's own speed up.
        PlayerController pcRef = player.GetComponent<PlayerController>();
        Boulder boulderScript = boulderPrefab.GetComponent<Boulder>();
        float boulderTopSpeed = boulderScript != null
            ? boulderScript.rollSpeed * DifficultyManager.ObstacleSpeedMultiplier()
            : 0f;
        float minReactionTime = 2.5f;
        float dynamicDistance = pcRef != null
            ? Mathf.Max(boulderSpawnDistance,
                (pcRef.runSpeed + boulderTopSpeed) * minReactionTime)
            : boulderSpawnDistance;

        float spawnZ = player.position.z + dynamicDistance;
        if (GroundTileSpawner.IsInsidePit(spawnZ)) return;
        int lane = LaneSpacingManager.Instance.PickLane(spawnZ);
        float xPos = lanePositions[lane];
        Vector3 spawnPos = new Vector3(xPos, 0.2f, spawnZ);
        if (IsHazardOccupied(spawnPos)) return;
        ObjectPool.Instance.Get(boulderPrefab, spawnPos, Quaternion.identity);
    }

    void SpawnAlienWall()
    {
        if (alienWallPrefab == null) return;
        if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

        // A fixed spawn distance gives less and less real warning as
        // run speed climbs — at max speed the wall could reach the
        // player before its own 0.8s rise animation even finishes,
        // so a routine jump can register as "clearing" a wall that
        // never properly formed. Guarantee a minimum reaction window
        // in seconds instead of a flat world-space distance.
        PlayerController pc = player.GetComponent<PlayerController>();
        float minReactionTime = 2.2f;
        float dynamicDistance = pc != null
            ? Mathf.Max(alienWallSpawnDistance, pc.runSpeed * minReactionTime)
            : alienWallSpawnDistance;

        float spawnZ = player.position.z + dynamicDistance;
        // Extra-wide margin (the wall rises up from below y=0 on spawn —
        // needs real clearance from any pit or that rise becomes visible
        // through the opened gap).
        if (GroundTileSpawner.IsInsidePit(spawnZ, 25f)) return;
        int lane = LaneSpacingManager.Instance.PickLane(spawnZ);
        float xPos = lanePositions[lane];
        Vector3 spawnPos = new Vector3(xPos, -3f, spawnZ);
        if (IsHazardOccupied(spawnPos)) return;
        ObjectPool.Instance.Get(alienWallPrefab, spawnPos, Quaternion.identity);
    }

    void SpawnUFO()
    {
        if (ufoPrefab == null) return;
        if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

        float spawnZ = player.position.z + spawnDistance;
        int lane = LaneSpacingManager.Instance.PickLane(spawnZ);
        float xPos = lanePositions[lane];
        Vector3 spawnPos = new Vector3(xPos, 1.5f, spawnZ);
        ObjectPool.Instance.Get(ufoPrefab, spawnPos, Quaternion.identity);
    }

    void SpawnStrafing()
    {
        if (strafingPrefab == null) return;
        if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

        // Spawn a little further out so its strafe is readable.
        float spawnZ = player.position.z + spawnDistance + 15f;
        int lane = LaneSpacingManager.Instance.PickLane(spawnZ);
        float xPos = lanePositions[lane];
        Vector3 spawnPos = new Vector3(xPos, 1.4f, spawnZ);
        ObjectPool.Instance.Get(strafingPrefab, spawnPos, Quaternion.identity);
    }

    // Full-width barrier spanning every lane, so it's placed on the
    // track's center line rather than through LaneSpacingManager.
    void SpawnTurnGate()
    {
        if (turnGatePrefab == null) return;

        PlayerController pcRef = player.GetComponent<PlayerController>();
        float minReactionTime = 3.5f;
        float dynamicDistance = pcRef != null
            ? Mathf.Max(turnGateSpawnDistance, pcRef.runSpeed * minReactionTime)
            : turnGateSpawnDistance;

        float spawnZ = player.position.z + dynamicDistance;
        // Wide margin — a mistimed swipe already reads as harsh; landing
        // it right on a pit's edge would make the fair-warning window
        // ambiguous on top of that.
        if (GroundTileSpawner.IsInsidePit(spawnZ, 20f)) return;
        Vector3 spawnPos = new Vector3(0f, 0f, spawnZ);
        ObjectPool.Instance.Get(turnGatePrefab, spawnPos, Quaternion.identity);
    }

    void SpawnAlien()
    {
        if (alienRunnerPrefab == null) return;
        if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

        float spawnZ = player.position.z + alienSpawnDistance;
        if (GroundTileSpawner.IsInsidePit(spawnZ)) return;
        int lane = LaneSpacingManager.Instance.PickLane(spawnZ);
        float xPos = lanePositions[lane];
        Vector3 spawnPos = new Vector3(xPos, 0f, spawnZ);
        if (IsHazardOccupied(spawnPos)) return;
        ObjectPool.Instance.Get(alienRunnerPrefab, spawnPos, Quaternion.identity);
    }

    // Spawn-time guard shared by all ground-level hazards: refuses to
    // place a new one on top of an existing boulder/wall/comet/alien in
    // the same lane. Every hazard is fixed-position, so this is the only
    // guard needed — nothing can drift into an overlap after spawning.
    bool IsHazardOccupied(Vector3 pos)
    {
        return HazardSpacing.BlockedNear<AlienWall>(pos)
            || HazardSpacing.BlockedNear<Boulder>(pos)
            || HazardSpacing.BlockedNear<Meteorite>(pos)
            || HazardSpacing.BlockedNear<AlienObstacle>(pos);
    }

void SpawnGoldOrb()
{
    if (goldOrbPrefab == null) return;
    int lane = Random.Range(0, 3);
    float xPos = lanePositions[lane];
    float yPos = Random.Range(1.8f, 2.5f);
    Vector3 spawnPos = new Vector3(
        xPos, 2.8f,
        player.position.z + spawnDistance);
    Instantiate(goldOrbPrefab, spawnPos,
                Quaternion.identity);
}

void SpawnMagnetOrb()
{
    if (magnetOrbPrefab == null) return;
    int lane = Random.Range(0, 3);
    float xPos = lanePositions[lane];
    float yPos = Random.Range(1.8f, 2.5f);
    Vector3 spawnPos = new Vector3(
        xPos, 2.8f,
        player.position.z + spawnDistance);
    Instantiate(magnetOrbPrefab, spawnPos,
                Quaternion.identity);
}

void SpawnInvincibilityOrb()
{
    if (invincibilityOrbPrefab == null) return;
    int lane = Random.Range(0, 3);
    float xPos = lanePositions[lane];
    Vector3 spawnPos = new Vector3(
        xPos, 2.8f,
        player.position.z + spawnDistance);
    Instantiate(invincibilityOrbPrefab, spawnPos,
                Quaternion.identity);
}

    // Was scanning every single GameObject in the scene every frame via
    // FindObjectsByType<GameObject> just to find ones named "ResourceOrb"
    // — by far the most expensive call in the game loop once the scene
    // filled up with ground tiles and pooled hazards. ResourceOrb now
    // self-registers, so this just walks the live orbs directly.
    void CleanupBehindPlayer()
    {
        float cutoffZ = player.position.z - destroyDistance;
        for (int i = ResourceOrb.Active.Count - 1; i >= 0; i--)
        {
            ResourceOrb orb = ResourceOrb.Active[i];
            if (orb != null && orb.transform.position.z < cutoffZ)
                Destroy(orb.gameObject);
        }
    }
}