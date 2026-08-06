using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Orb Spawning")]
    public GameObject orbPrefab;
    public Transform player;
    public float spawnDistance = 35f;
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
    public float boulderSpawnDistance = 70f;
    private float boulderTimer = 5f;

    [Header("UFO Spawning")]
    public GameObject ufoPrefab;
    public float ufoInterval = 9f;
    private float ufoTimer = 8f;

[Header("Alien Wall Spawning")]
public GameObject alienWallPrefab;
public float alienWallInterval = 10f;
public float alienWallSpawnDistance = 95f;
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

[Header("Alien Runner")]
public GameObject alienRunnerPrefab;
public float alienInterval = 8f;
public float alienSpawnDistance = 90f;
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
    // Widened from (0.75, 1.25) — the tighter range still read as a
    // near-even metronome once several hazard types were layered
    // together; a wider spread breaks that up without loosening any of
    // the anti-overlap guards, which are untouched.
    float Jitter() => Random.Range(0.6f, 1.45f);
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
// Was a world-Z distance threshold — world Z stops being a reliable
// "how far into the run" measure once 90-degree turns can redirect
// travel along X, so this now gates on play time instead, same as
// every other unlock timer in the game.
// Was 200s/8% — combined with the run needing to survive that long in
// the first place, that made this drop effectively never seen in
// practice rather than just rare. Still a real rare-drop, just one that
// actually shows up within a normal run instead of only a marathon one.
public float invincibilityUnlockRunTime = 60f;
public float invincibilityInterval = 40f;
[Range(0f, 1f)] public float invincibilityChance = 0.35f;
private float invincibilityTimer = 45f;

    private PlayerController playerController;

    // Nothing may be created inside the player's view. The camera's far
    // clip (1000) is NOT what bounds visibility here — the ExponentialSquared
    // fog is, and ZoneManager drives its density at runtime (per-zone base
    // 0.010-0.014, times a difficulty ramp up to 1.7, times
    // WeatherManager.StormFogMultiplier; measured 0.0374 in a storm). The
    // CLEAREST the game ever gets is density 0.010, where transmittance
    // exp(-(density*d)^2) makes the old spawn distances plainly visible:
    //   alien wall  60 -> 96% visible     meteorite 70 -> 95% visible
    //   orbs/UFO   105 -> 33% visible     boulder  150 -> 11% visible
    //   alien      160 ->  8% visible
    // The 60/70/105 spawns were flat-out popping into existence on screen.
    // At 220 units transmittance is under 1% even at the clearest density,
    // so an object materializes fully hidden and fades in through the fog
    // over the following ~70 units — the Subway Surfers/Temple Run
    // behaviour, and identical to how the ground tiles themselves (laid
    // 735 units out) already arrive. Chosen no larger than it needs to be:
    // pushing to 400+ would look exactly the same but quadruple the live
    // object count and stretch the spawn-to-encounter pipeline for nothing.
    // Well inside the far clip (1000) and the track lookahead
    // (tilesAhead 150 * tileLength 4.9 = 735), so nothing spawns past
    // generated ground or gets hard-clipped.
    // Applied as a floor in AheadPos, which every ObjectSpawner spawn
    // routes through, rather than per-call-site.
    public const float MinSpawnDistance = 220f;

    // Player-relative helpers: everything spawns ahead of the player along
    // their CURRENT heading (transform.forward), with lane/lateral offsets
    // along their CURRENT right — instead of hardcoded world Z/X — so
    // hazards keep spawning in the correct place after a 90-degree turn.
    Vector3 AheadPos(float forwardDist, float lateralOffset, float y)
    {
        if (playerController == null)
            playerController = player.GetComponent<PlayerController>();
        // player.position already includes the player's own in-lane strafe
        // offset (from lane changing), so lane target offsets must be
        // measured from the track CENTERLINE, not from wherever the player
        // currently is — otherwise the two offsets add together and push
        // spawns outside the real lane positions.
        float currentOffset = playerController != null
            ? playerController.GetCurrentLaneOffset() : 0f;
        forwardDist = Mathf.Max(forwardDist, MinSpawnDistance);
        // Never place a hazard past a pending turn's pivot — the track
        // doesn't extend past it in the current heading until the turn
        // resolves, so an unclamped lookahead spawns obstacles floating
        // in open air beyond the corner.
        if (GroundTileSpawner.Instance != null)
            forwardDist = GroundTileSpawner.Instance.ClampAheadForTurn(
                player.position, player.forward, forwardDist);
        Vector3 pos = player.position
            - player.right * currentOffset
            + player.forward * forwardDist
            + player.right * lateralOffset;
        pos.y = y;
        return pos;
    }

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
// been running for a while. The timer used to only decrement while
// already unlocked (unlike every other spawn timer, which always
// ticks) — that stacked its own 45s starting value ON TOP of the 60s
// unlock gate, so the first real roll couldn't happen before ~105s in,
// not the ~45s every other "first roll" timer gets. Ticking it
// unconditionally and gating only the actual spawn on unlock is what
// the earlier rarity tuning (60s/35%, see field comments) actually
// intended.
invincibilityTimer -= Time.deltaTime;
if (invincibilityTimer <= 0f)
{
    invincibilityTimer = invincibilityInterval;
    bool unlocked = DifficultyManager.Instance != null &&
        DifficultyManager.Instance.runTime >= invincibilityUnlockRunTime;
    if (unlocked && Random.value < invincibilityChance)
        SpawnInvincibilityOrb();
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

        Vector3 basePos = AheadPos(boulderSpawnDistance, 0f, 0f);
        if (GroundTileSpawner.Instance != null &&
            GroundTileSpawner.Instance.IsObstacleSpawnSuppressed(basePos)) return;
        // Wide margin since this can place an alien wall, which rises up
        // from below y=0 and would show through a nearby pit.
        if (GroundTileSpawner.IsInsidePit(basePos, 25f)) return;
        int[] blockedLanes = LaneSpacingManager.Instance
            .GetFormationBlockedLanes(difficulty, basePos.z);
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
            Vector3 pos = AheadPos(boulderSpawnDistance, lanePositions[lane], yPos);
            // Same occupancy guard the ambient per-type spawners use —
            // without it a formation could drop a wall or alien directly
            // on top of a boulder (or vice versa) that another spawner
            // already placed nearby in that lane.
            if (IsHazardOccupied(pos, prefab)) continue;
            ObjectPool.Instance.Get(prefab, pos, Quaternion.LookRotation(player.forward));
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
            // The floor has to be applied BEFORE the per-orb offset, not
            // after. AheadPos clamps whatever it is handed up to
            // MinSpawnDistance (220), and spawnDistance + zOffset tops out
            // around 125 — so every orb was being clamped to exactly 220
            // and this spread was silently erased. Two orbs from the same
            // batch that also rolled the same lane then spawned inside
            // each other, and every orb shared one forward slot with
            // whatever hazard spawned that frame. Same fix SpawnBoulder
            // already applies to its own distance compensation.
            Vector3 spawnPos = AheadPos(
                Mathf.Max(spawnDistance, MinSpawnDistance) + zOffset, xPos, yPos);
            // Orbs never checked hazard occupancy at all (unlike every
            // hazard type, which checks against every other hazard) —
            // an orb could spawn embedded in a boulder/wall/comet already
            // sitting in that lane. Skip that one orb rather than retry;
            // SpawnOrb already rolls 1-2 of these every ~1.5s, so a
            // rare miss here isn't a meaningful pickup-rate loss.
            if (IsHazardOccupied(spawnPos, orbPrefab)) continue;
            Instantiate(orbPrefab, spawnPos, Quaternion.identity);
        }
    }

    void SpawnLavaCrack()
    {
        if (lavaCrackPrefab == null) return;
        Vector3 spawnPos = AheadPos(spawnDistance, 0f, 0.1f);
        if (GroundTileSpawner.Instance != null &&
            GroundTileSpawner.Instance.IsObstacleSpawnSuppressed(spawnPos)) return;
        if (IsHazardOccupied(spawnPos, lavaCrackPrefab)) return;
        ObjectPool.Instance.Get(lavaCrackPrefab, spawnPos, Quaternion.LookRotation(player.forward));
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

        // Every other hazard is stationary, so how long its fog fade-in
        // takes is purely a function of the PLAYER's own closing speed —
        // AheadPos's shared MinSpawnDistance (220) floor was tuned around
        // that. The boulder ALSO closes distance under its own power,
        // which shortens that same fade-in window in real time: it
        // reaches the "now clearly visible" point sooner than a
        // stationary hazard spawned at the same distance would, reading
        // as popping into view instead of approaching from a distance.
        // Push it out by its own extra closing contribution (roughly the
        // time it'd spend crossing the fade zone) so the fade-in takes
        // about as long, in real time, as it does for everything else.
        // Must apply the MinSpawnDistance floor HERE first, before
        // adding the compensation — at low/mid difficulty dynamicDistance
        // sits well under 220, so adding the extra distance first and
        // then letting AheadPos's own Max(_, 220) run afterward silently
        // erased it back down to exactly 220, same as before this fix.
        dynamicDistance = Mathf.Max(dynamicDistance, MinSpawnDistance)
            + boulderTopSpeed * 1.5f;

        Vector3 basePos = AheadPos(dynamicDistance, 0f, 0f);
        if (GroundTileSpawner.Instance != null &&
            GroundTileSpawner.Instance.IsObstacleSpawnSuppressed(basePos)) return;
        if (GroundTileSpawner.IsInsidePit(basePos)) return;
        int lane = LaneSpacingManager.Instance.PickLane(basePos.z);
        Vector3 spawnPos = AheadPos(dynamicDistance, lanePositions[lane], 0.2f);
        if (IsHazardOccupied(spawnPos, boulderPrefab)) return;
        ObjectPool.Instance.Get(boulderPrefab, spawnPos, Quaternion.LookRotation(player.forward));
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

        Vector3 basePos = AheadPos(dynamicDistance, 0f, 0f);
        if (GroundTileSpawner.Instance != null &&
            GroundTileSpawner.Instance.IsObstacleSpawnSuppressed(basePos)) return;
        // Extra-wide margin (the wall rises up from below y=0 on spawn —
        // needs real clearance from any pit or that rise becomes visible
        // through the opened gap).
        if (GroundTileSpawner.IsInsidePit(basePos, 25f)) return;
        int lane = LaneSpacingManager.Instance.PickLane(basePos.z);
        Vector3 spawnPos = AheadPos(dynamicDistance, lanePositions[lane], -3f);
        if (IsHazardOccupied(spawnPos, alienWallPrefab)) return;
        ObjectPool.Instance.Get(alienWallPrefab, spawnPos, Quaternion.LookRotation(player.forward));
    }

    void SpawnUFO()
    {
        if (ufoPrefab == null) return;
        if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

        Vector3 basePos = AheadPos(spawnDistance, 0f, 0f);
        if (GroundTileSpawner.Instance != null &&
            GroundTileSpawner.Instance.IsObstacleSpawnSuppressed(basePos)) return;
        int lane = LaneSpacingManager.Instance.PickLane(basePos.z);
        Vector3 spawnPos = AheadPos(spawnDistance, lanePositions[lane], 1.5f);
        if (IsHazardOccupied(spawnPos, ufoPrefab)) return;
        ObjectPool.Instance.Get(ufoPrefab, spawnPos, Quaternion.LookRotation(player.forward));
    }

    void SpawnStrafing()
    {
        if (strafingPrefab == null) return;
        if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

        // Spawn a little further out so its strafe is readable.
        Vector3 basePos = AheadPos(spawnDistance + 15f, 0f, 0f);
        if (GroundTileSpawner.Instance != null &&
            GroundTileSpawner.Instance.IsObstacleSpawnSuppressed(basePos)) return;
        int lane = LaneSpacingManager.Instance.PickLane(basePos.z);
        Vector3 spawnPos = AheadPos(spawnDistance + 15f, lanePositions[lane], 1.4f);
        // Wider lateral tolerance than the other occupancy checks — this
        // hazard slides across the full lane width after spawning (see
        // StrafingObstacle.strafeRange), so a spawn-time check using the
        // normal narrow lane tolerance would still let it later drift
        // into a fixed hazard sitting in a neighboring lane.
        Vector3 fwd = player.forward;
        bool sweepBlocked =
            HazardSpacing.BlockedNear<AlienWall>(spawnPos, fwd, 4.5f)
            || HazardSpacing.BlockedNear<Boulder>(spawnPos, fwd, 4.5f)
            || HazardSpacing.BlockedNear<Meteorite>(spawnPos, fwd, 4.5f)
            || HazardSpacing.BlockedNear<AlienObstacle>(spawnPos, fwd, 4.5f)
            || HazardSpacing.BlockedNear<UFOObstacle>(spawnPos, fwd, 4.5f)
            || HazardSpacing.BlockedNear<StrafingObstacle>(spawnPos, fwd, 4.5f)
            || HazardSpacing.BlockedNear<LavaCrack>(spawnPos, fwd, 4.5f);
        if (sweepBlocked) return;
        ObjectPool.Instance.Get(strafingPrefab, spawnPos, Quaternion.LookRotation(player.forward));
    }

    void SpawnAlien()
    {
        if (alienRunnerPrefab == null) return;
        if (LaneSpacingManager.Instance.ShouldInsertSafeGap()) return;

        Vector3 basePos = AheadPos(alienSpawnDistance, 0f, 0f);
        if (GroundTileSpawner.Instance != null &&
            GroundTileSpawner.Instance.IsObstacleSpawnSuppressed(basePos)) return;
        if (GroundTileSpawner.IsInsidePit(basePos)) return;
        int lane = LaneSpacingManager.Instance.PickLane(basePos.z);
        Vector3 spawnPos = AheadPos(alienSpawnDistance, lanePositions[lane], 0f);
        if (IsHazardOccupied(spawnPos, alienRunnerPrefab)) return;
        ObjectPool.Instance.Get(alienRunnerPrefab, spawnPos, Quaternion.LookRotation(player.forward));
    }

    // Spawn-time guard shared by all ground-level hazards: refuses to
    // place a new one where it would actually overlap an existing
    // boulder/wall/comet/alien, using each hazard's REAL rendered size
    // (not a flat guessed tolerance) — see HazardSpacing.BlockedNear's
    // geometry-aware overload. Every hazard is fixed-position, so this is
    // the only guard needed — nothing can drift into an overlap after
    // spawning. candidatePrefab is whatever is about to be spawned at pos.
    // Orbs are plain Instantiates rather than pooled, so they miss the
    // invalidation ObjectPool.Get does. Without it an orb placed at the top
    // of Update is invisible to every hazard occupancy check further down
    // the SAME Update — the exact blind spot that let hazards land on orbs.
    void SpawnPickup(GameObject prefab, Vector3 pos)
    {
        Instantiate(prefab, pos, Quaternion.identity);
        HazardSpacing.Invalidate();
    }

    bool IsHazardOccupied(Vector3 pos, GameObject candidatePrefab)
    {
        Vector3 fwd = player.forward;
        return HazardSpacing.BlockedNear<AlienWall>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<Boulder>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<Meteorite>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<AlienObstacle>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<UFOObstacle>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<StrafingObstacle>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<LavaCrack>(pos, fwd, candidatePrefab)
            // Orbs belong in this check too, and their absence is why orbs
            // kept ending up embedded in obstacles despite SpawnOrb having
            // its own guard. That guard is one-directional: it only sees
            // hazards that ALREADY exist. Orbs spawn at the top of Update
            // and every hazard type spawns below them, so a hazard landing
            // in the same lane/slot on the SAME frame never saw the orb
            // that had just been placed there — and nothing checked
            // afterwards, because every hazard is fixed-position and only
            // ever validated once, at spawn.
            //
            // Ignoring height is deliberate: an alien wall occupies y
            // 1.4-4.4 and a boulder rests at 1.85, which covers the whole
            // band orbs spawn in (1.4-2.0 for resource orbs, 2.8 for
            // power-ups). A spawn-time Y test would also be reading a lie
            // for the wall, which spawns at y=-3 and rises afterwards.
            || HazardSpacing.BlockedNear<ResourceOrb>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<GoldOrb>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<MagnetOrb>(pos, fwd, candidatePrefab)
            || HazardSpacing.BlockedNear<InvincibilityOrb>(pos, fwd, candidatePrefab);
    }

void SpawnGoldOrb()
{
    if (goldOrbPrefab == null) return;
    int lane = Random.Range(0, 3);
    Vector3 spawnPos = AheadPos(spawnDistance, lanePositions[lane], 2.8f);
    if (IsHazardOccupied(spawnPos, goldOrbPrefab)) return;
    Instantiate(goldOrbPrefab, spawnPos,
                Quaternion.identity);
}

void SpawnMagnetOrb()
{
    if (magnetOrbPrefab == null) return;
    int lane = Random.Range(0, 3);
    Vector3 spawnPos = AheadPos(spawnDistance, lanePositions[lane], 2.8f);
    if (IsHazardOccupied(spawnPos, magnetOrbPrefab)) return;
    Instantiate(magnetOrbPrefab, spawnPos,
                Quaternion.identity);
}

void SpawnInvincibilityOrb()
{
    if (invincibilityOrbPrefab == null) return;
    int lane = Random.Range(0, 3);
    Vector3 spawnPos = AheadPos(spawnDistance, lanePositions[lane], 2.8f);
    if (IsHazardOccupied(spawnPos, invincibilityOrbPrefab)) return;
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
        for (int i = ResourceOrb.Active.Count - 1; i >= 0; i--)
        {
            ResourceOrb orb = ResourceOrb.Active[i];
            if (orb != null &&
                player.InverseTransformPoint(orb.transform.position).z < -destroyDistance)
                Destroy(orb.gameObject);
        }
    }
}
