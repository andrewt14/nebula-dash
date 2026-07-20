using System.Collections.Generic;
using UnityEngine;

public class GroundTileSpawner : MonoBehaviour
{
    public static GroundTileSpawner Instance;

    public GameObject tilePrefab;
    public Transform player;
    public int tilesAhead = 150;
    public float tileLength = 5f;
    public Material tileMaterial;
    private PlayerController pc;
    // Where/which-direction the NEXT tile will be generated. Advances
    // along its own forward each spawn — this replaces the old flat
    // world-Z "nextSpawnZ" so the track can bend 90 degrees at a turn
    // and keep generating correctly in the new direction.
    private Vector3 cursorPos;
    private Quaternion cursorRot = Quaternion.identity;
    // Tiles spawn strictly in generation order, so the oldest (furthest
    // behind the player along the path) is always at the front —
    // despawning is then a plain dequeue instead of a position scan.
    private readonly Queue<GameObject> spawnedTiles = new Queue<GameObject>();
    // The regular tile immediately before a turn's corner — its own
    // LaneEdge lines get disabled when a turn begins (see BeginPendingTurn)
    // since they'd otherwise double up with the corner's own bent lines
    // over the stretch where the two tiles overlap.
    private GameObject lastSpawnedNormalTile;
    // The exit leg's first tile doesn't exist yet when BeginPendingTurn
    // runs (it's spawned later by the normal generation loop) — this flag
    // catches it in SpawnTile so its LaneEdge can be disabled too, for the
    // same reason as lastSpawnedNormalTile above.
    private bool disableNextTileLaneEdge = false;

    [Header("Ground Breaks")]
    // Locked out until well into the run; moderately frequent once
    // unlocked now that the break-open reveal happens right in front of
    // the player instead of pre-existing far out.
    public float pitUnlockTime = 45f;
    // Raised alongside the wider minSecondsBetweenPits floor below —
    // that widened the minimum gap between pits considerably, which on
    // its own would've made them far rarer than before at a flat 1%
    // per-tile roll once eligible.
    [Range(0f, 1f)] public float pitChance = 0.06f;
    public float minPitSpacing = 300f;
    // Each BreakingGroundTile's own warning zone scales with speed (its
    // trigger+warn window is seconds-based), so at high speed that zone
    // can span nearly 1000 units — a flat minPitSpacing of 300 let two
    // pits both be actively warning at once, reading as "jumping over
    // two breaks" instead of clearing them one at a time. Guarantee a
    // minimum number of seconds between pits at the run's eventual top
    // speed instead of a fixed world distance.
    public float minSecondsBetweenPits = 6f;
    private float lastPitEndDistance = -9999f;
    // Total length of track laid so far, along whatever heading each
    // stretch was generated on — a monotonic replacement for raw world Z,
    // which stops advancing once the track bends onto a new heading.
    private float cursorPathDistance = 0f;

    [Header("Broken Edge Look")]
    public Texture2D rockAlbedo;
    public Texture2D rockNormal;
    public Texture2D rockOcclusion;

    [Header("90-Degree Turns")]
    // Gated on ZoneManager's level (its on-screen "LEVEL N" banner, 1-
    // indexed) rather than raw play time — this is the biggest read/
    // commitment in the game (a wrong or missed swipe runs the player off
    // the track entirely), so it shouldn't show up until the run has
    // properly settled in.
    public int turnUnlockLevel = 4;
    // Tile-count spacing between turns right at the unlock level — wide,
    // so turns start out rare.
    public int tilesPerTurnMin = 400;
    public int tilesPerTurnMax = 550;
    // Spacing floor once turnRampLevels levels have passed since unlock —
    // turns gradually get more frequent, never below this. Still a real
    // gap (220-320 tiles) even at the floor — turns stay an occasional
    // event, not a constant one, just a more frequent one.
    public int tilesPerTurnMinFloor = 220;
    public int tilesPerTurnMaxFloor = 320;
    public int turnRampLevels = 12;
    // Reaction window, expressed as seconds of travel at the player's
    // current speed — how long before reaching the corner the "TURN
    // LEFT/RIGHT" banner appears.
    // Also literally how many seconds the "3, 2, 1" countdown counts down
    // from — the banner/countdown now always starts at a fixed 3 and
    // ticks down in whole seconds, instead of being derived from live
    // distance/speed (which produced inconsistent, sometimes-fractional-
    // feeling starting numbers). This is purely how far ahead the banner
    // first appears; how long the player actually has before a miss is
    // still governed by turnMissDistance/position, unaffected.
    public float turnReactionTime = 3f;
    // speed * turnReactionTime alone grows unbounded with run speed (up
    // to maxRunSpeed=200), so at high difficulty the banner/countdown was
    // popping up 100+ units before the corner — long before the corner
    // telegraph is even visible through the fog. Hard cap keeps the lead
    // distance sane regardless of how fast the run has gotten.
    public float maxTurnLeadDistance = 90f;
    // DEPRECATED — no longer read. The arm window is now derived from the
    // exact same TurnLeadDistance() the banner/countdown uses, so the two
    // physically cannot drift apart. This field was serialized at 1.0 in
    // GamePlay.unity while turnReactionTime was 3.5, so the countdown told
    // the player "3" and "2" during a stretch where a matching swipe was
    // silently discarded as an ordinary lane change — the turn counter
    // desync.
    [HideInInspector] public float turnArmReactionTime = 3f;
    public float turnExecuteDistance = 1.25f;
    public float turnMissDistance = 5f;
    // How far before the pivot the arrow sign sits — right at the corner
    // itself (not out in the approach corridor) so it reads as marking
    // the actual turn rather than a separate advance-warning sign.
    public float turnTelegraphLeadDistance = 0.2f;
    public float turnObstacleBufferDistance = 48f;
    // Separate (larger) buffer for the EXIT leg specifically. Tile
    // generation runs far ahead of the player (up to tilesAhead tiles) —
    // exit-leg tiles past a turn, and whether each one gets a pit, are
    // decided the moment they're generated, which can happen while the
    // player is still hundreds of units before the corner. By the time
    // postTurnObstacleCooldown's time-based window would matter, those
    // tiles already exist with their pit/no-pit choice locked in. Sharing
    // the same (smaller) buffer as the approach leg left a stretch right
    // after the corner where hazards/pits could still land. This needs to
    // be a genuinely large fixed distance, not a time window, since it's
    // checked at generation time rather than at player-arrival time.
    public float turnExitBufferDistance = 110f;
    // Was 1.2s — at ordinary run speed that's well under one tile's worth
    // of clearance past the corner, so hazards (and pits, now that they're
    // gated by this same suppression) could still show up right as the
    // player exits a turn, before they've even gotten their bearings on
    // the new heading. A few full seconds of guaranteed clear track after
    // every turn.
    public float postTurnObstacleCooldown = 3f;
    private int tilesSinceLastTurn = 0;
    private int nextTurnTileCount;
    private bool turnPending = false;
    private bool turnArmed = false;
    private bool turnMissed = false;
    private Vector3 pendingTurnPos;
    private Vector3 pendingApproachForward;
    private Vector3 pendingApproachRight;
    private Vector3 pendingExitForward;
    private Vector3 pendingExitRight;
    private int pendingTurnDirection;
    private GameObject turnTelegraphRoot;
    private Material turnTelegraphMaterial;
    private float suppressObstacleSpawnsUntil = -999f;
    // Generation can freeze well ahead of the player (up to the full
    // tilesAhead lookahead buffer) — the "SWIPE LEFT/RIGHT" banner only
    // fires once the player is actually within range of the reaction
    // window, so there's no dead stretch where the banner is up but a
    // swipe doesn't do anything yet.
    private bool turnBannerShown = false;
    private int lastCountdownValue = -1;
    // Distance to the pivot at the instant the banner appeared. The
    // countdown is a pure ratio of remaining distance to this, so it can't
    // desync from the approach when run speed ramps mid-countdown (the old
    // version mixed a speed captured at banner time with a live speed).
    private float turnCountdownStartDistance = 0f;

    private static readonly Color TurnLeftColor = new Color(0.15f, 0.55f, 1f);
    private static readonly Color TurnRightColor = new Color(1f, 0.55f, 0.1f);

    // Open gaps the player has to jump. Each pit remembers the heading it
    // was laid on (its own tile's forward, fixed at spawn) instead of a
    // raw world-Z range — a raw Z range only means "a specific point on
    // the track" while the track happens to still be running along Z, and
    // silently breaks the moment the track bends 90 degrees onto X.
    // Projecting distance along the pit's own forward keeps it correct
    // regardless of how many turns the track has since taken.
    private struct PitRange { public Vector3 pos; public Vector3 forward; public float span; }
    private static readonly List<PitRange> activePits = new List<PitRange>();

    public static bool IsInsidePit(Vector3 worldPos, float margin = 3f)
    {
        foreach (PitRange pit in activePits)
        {
            float d = Vector3.Dot(worldPos - pit.pos, pit.forward);
            if (d > -margin && d < pit.span + margin)
                return true;
        }
        return false;
    }

void Start()
{
    Instance = this;
    pc = player.GetComponent<PlayerController>();
    cursorPos = new Vector3(0, 0, player.position.z - 10f);
    cursorRot = Quaternion.identity;
    // No pit until the player has had a fair unbroken stretch to start on.
    lastPitEndDistance = 0f;
    RollNextTurnTileCount();
    for (int i = 0; i < 20; i++)
        SpawnTile();

}

void OnDestroy()
{
    if (turnTelegraphRoot != null)
        Destroy(turnTelegraphRoot);
}

    void Update()
    {
        if (player == null) return;

        bool turnsUnlocked = ZoneManager.Instance != null &&
            ZoneManager.Instance.CurrentLevel >= turnUnlockLevel;

        // Distance-based instead of a forward-dot lookahead — once a turn
        // starts bending the cursor ahead of the player (see
        // BeginPendingTurn), the cursor's heading and the player's current
        // heading legitimately differ until the swipe resolves, so a
        // dot-product-against-player-forward check would stop refilling
        // the buffer mid-bend.
        //
        // Capped per-frame: the moment a turn bends the cursor onto a new
        // heading, the ENTIRE lookahead budget becomes available again in
        // that direction at once (Euclidean distance from the player was
        // already sitting near the cap in the old heading, but starts
        // small again along the new one) — without this cap the while loop
        // dumps ~150 tiles in a single frame, which overflows the tile
        // queue's FIFO despawn cap in DespawnOldTiles() and destroys
        // still-needed tiles right under/ahead of the player. Spreading
        // the same refill over several frames keeps generation and
        // despawn paced together instead of one large spike.
        int spawnedThisFrame = 0;
        const int maxTilesPerFrame = 10;
        while (spawnedThisFrame < maxTilesPerFrame &&
            Vector3.Distance(cursorPos, player.position) < tilesAhead * tileLength)
        {
            if (!turnPending && turnsUnlocked && tilesSinceLastTurn >= nextTurnTileCount)
            {
                BeginPendingTurn();
                spawnedThisFrame++;
                continue;
            }
            SpawnTile();
            spawnedThisFrame++;
        }

        // Generation can freeze far ahead of the player (up to the full
        // lookahead buffer) — only show the telegraph once they're
        // actually close enough for a swipe to start counting, matching
        // HandleSwipe's own window so there's no dead stretch where the
        // banner is up but input doesn't do anything yet.
        if (turnPending && !turnBannerShown)
        {
            float aheadDist = Vector3.Dot(
                pendingTurnPos - player.position, pendingApproachForward);
            if (aheadDist < TurnLeadDistance())
            {
                turnBannerShown = true;
                // Capture the distance to the corner at the instant the banner
                // appears. The displayed counter maps the REMAINING distance
                // onto thirds of this, so it always ticks a clean 3 -> 2 -> 1
                // before the pivot no matter the run speed. The actual miss
                // deadline is unchanged (position-based in MissTurn).
                turnCountdownStartDistance = Mathf.Max(0.01f, aheadDist);
            }
        }

        // Single combined banner+countdown text ("< TURN LEFT  3") — shows
        // exactly how long until an unanswered turn kills the player (the
        // same deadline MissTurn uses: reaching turnMissDistance past the
        // pivot while still unarmed), instead of a static banner with no
        // sense of urgency. Hidden once armed (the "TURN READY" flash owns
        // the banner object at that point) or once resolved/missed.
        if (turnPending && turnBannerShown && !turnArmed && !turnMissed)
        {
            // Map the REMAINING distance onto thirds of the window captured
            // when the banner appeared, so the player always sees a clean 3,
            // then 2, then 1 before the pivot regardless of speed (and
            // regardless of speed CHANGING mid-countdown, which the old
            // time-based version desynced on).
            float remaining = Mathf.Max(0f, DistanceToPendingTurn());
            int display = Mathf.Clamp(
                Mathf.CeilToInt(remaining / (turnCountdownStartDistance / 3f)), 1, 3);
            string label = pendingTurnDirection < 0 ? "< TURN LEFT" : "TURN RIGHT >";
            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowTurnBanner(label + "  " + display,
                    pendingTurnDirection < 0 ? TurnLeftColor : TurnRightColor);
            if (display != lastCountdownValue && AudioManager.Instance != null)
                AudioManager.Instance.PlayTurnCountdown();
            lastCountdownValue = display;
        }
        else if (!turnArmed && ScorePopup.Instance != null)
        {
            ScorePopup.Instance.HideTurnBanner();
            lastCountdownValue = -1;
        }

        UpdatePendingTurn();
        DespawnOldTiles();
        // A pit is behind the player once they've travelled well past it
        // along that pit's own forward — using the pit's stored heading
        // (not raw Z) so this stays correct across turns.
        activePits.RemoveAll(p =>
            Vector3.Dot(player.position - p.pos, p.forward) > tileLength * 2);
    }

    // Stops generation dead at the current cursor and marks it as the
    // corner — a matching swipe within the reaction window (HandleSwipe)
    // resolves it and resumes generation along the new heading; missing
    // it means the track simply never extends further in the old
    // direction, and the player runs off the edge into fallDeathY.
    void BeginPendingTurn()
    {
        turnPending = true;
        turnArmed = false;
        turnMissed = false;
        turnBannerShown = false;
        pendingTurnPos = cursorPos;
        pendingTurnDirection = Random.value < 0.5f ? -1 : 1;
        pendingApproachForward = cursorRot * Vector3.forward;
        pendingApproachRight = cursorRot * Vector3.right;

        // Bend generation onto the new heading right away instead of
        // freezing at the pivot — the corridor is visibly turning ahead of
        // the player well before the reaction window opens. The player's
        // own heading (and the camera's) only changes on a resolved swipe,
        // in ExecuteTurn — this only steers where new tiles get laid.
        cursorRot = Quaternion.AngleAxis(90f * pendingTurnDirection, Vector3.up) * cursorRot;
        pendingExitForward = cursorRot * Vector3.forward;
        pendingExitRight = cursorRot * Vector3.right;

        // The regular tile right before the corner overlaps several units
        // into the corner square's own footprint (tiles are laid with a
        // deliberate overlap) — its LaneEdge lines would double up with
        // the corner's own bent lines over that shared stretch. Disable
        // them here; SpawnCornerTile's bend segments reach back far
        // enough to cover for it.
        if (lastSpawnedNormalTile != null)
            foreach (Transform child in lastSpawnedNormalTile.transform)
                if (child.name.StartsWith("LaneEdge"))
                    child.gameObject.SetActive(false);

        // Corner-fill: one full-width pivot tile so the turn square is
        // completely paved (no fall-through gap on the outside of the
        // turn, no overlap/z-fighting on the inside).
        SpawnCornerTile(pendingTurnPos);
        // Reset the spacing counter HERE, in the generation domain, where
        // the corner is actually laid — not in ResolveTurn, which fires in
        // the gameplay domain when the player reaches the pivot. By that
        // point generation has already run ~tilesAhead tiles past the
        // corner, and zeroing there threw those away, so real corner-to-
        // corner spacing came out as nextTurnTileCount + lookahead (and
        // drifted with speed, since the lookahead refills at
        // maxTilesPerFrame). Counting laid tiles from the laid corner makes
        // the spacing exactly nextTurnTileCount.
        // ponytail: assumes nextTurnTileCount > tilesAhead (currently
        // 220-550 vs 150) so the next threshold is never hit while a turn
        // is still pending; drop tilesPerTurnMinFloor below tilesAhead and
        // turns will stall on the !turnPending gate instead.
        tilesSinceLastTurn = 0;
        RollNextTurnTileCount();
        SpawnTurnTelegraph();
        cursorPos = pendingTurnPos + cursorRot * Vector3.forward * (tileLength - 0.1f);
        cursorPathDistance += tileLength - 0.1f;
        // The exit leg's first tile (about to be spawned from this new
        // cursorPos) overlaps the corner square the same way the last
        // approach tile did — flag it for the same LaneEdge disable.
        disableNextTileLaneEdge = true;
    }

    public bool TryHandleTurnSwipe(int dir)
    {
        if (!turnPending || player == null || turnMissed)
            return false;

        float aheadDist = DistanceToPendingTurn();
        // EXACTLY the banner's own lead distance, not a separate tunable.
        // A tighter arm window than the banner window means the countdown
        // is on screen counting "3", "2" while a matching swipe is silently
        // thrown away as an ordinary lane change — the player is told they
        // have three counts to act and only the last one works. Banner
        // visible <=> swipe accepted, by construction.
        if (aheadDist > TurnLeadDistance())
            return false;

        // Missing the turn is decided purely by position, in
        // UpdatePendingTurn, which runs unconditionally every frame
        // regardless of input. Duplicating that check here meant an
        // unrelated keypress (an ordinary lane-change dodge, wrong
        // direction, thrown in right as the player happened to already be
        // past the miss point) could trigger death through THIS input
        // path — coupling a death to a keypress instead of purely to
        // position. Just no-op here and let UpdatePendingTurn catch it on
        // its own the same frame.
        if (aheadDist < -turnMissDistance)
            return false;

        // Once armed, the turn already executes on its own (positionally,
        // in UpdatePendingTurn) — it needs no further input at all, in
        // EITHER direction. Previously a same-direction repeat was still
        // swallowed as a "redundant" no-op, but that meant if the pending
        // turn was e.g. RIGHT and the player wanted to dodge right
        // (matching direction) after arming, that press did nothing —
        // read as "can't switch lanes right before the turn." Let every
        // swipe fall through to a normal lane change once armed.
        if (turnArmed)
            return false;

        // Wrong-direction swipe here is just an ordinary lane-change
        // attempt that happens to overlap the reaction window (obstacle
        // dodging, etc) — it must NOT kill the player. Only actually
        // running past the pivot without having armed the correct turn
        // (handled positionally in UpdatePendingTurn) counts as a miss.
        // Returning false lets HandleHorizontalInput fall through to a
        // normal LaneLeft/LaneRight.
        if (dir != pendingTurnDirection)
            return false;

        turnArmed = true;
        turnBannerShown = true;
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowTurnBannerTemporary(
                pendingTurnDirection < 0 ? "< TURN READY" : "TURN READY >",
                pendingTurnDirection < 0 ? TurnLeftColor : TurnRightColor,
                0.8f);
        UpdateTurnTelegraphColor(Color.white);

        // Arming still lets the swipe fall through to a normal lane
        // change (return false, not true) — previously the swipe that
        // armed the turn was swallowed entirely, so a swipe toward the
        // turn's own direction moved the player 0 lanes until the turn
        // auto-executed moments later, reading as "input stopped
        // working" right when it mattered most.
        return false;
    }

    void UpdatePendingTurn()
    {
        if (!turnPending || player == null)
            return;

        float aheadDist = DistanceToPendingTurn();
        if (turnArmed)
        {
            if (aheadDist <= turnExecuteDistance)
                ResolveTurn(pendingTurnDirection);
        }
        else if (aheadDist < -turnMissDistance)
        {
            MissTurn();
        }
    }

    // Single source of truth for "how far out does the turn become a
    // thing" — used by BOTH the banner/countdown and the swipe arm check,
    // so those two can never disagree about the window.
    float TurnLeadDistance()
    {
        float speed = pc != null ? pc.runSpeed : 15f;
        return Mathf.Min(speed * turnReactionTime, maxTurnLeadDistance);
    }

    float DistanceToPendingTurn()
    {
        return Vector3.Dot(
            pendingTurnPos - player.position, pendingApproachForward);
    }

    void MissTurn()
    {
        if (turnMissed)
            return;

        turnMissed = true;
        turnPending = false;
        if (turnTelegraphRoot != null)
            Destroy(turnTelegraphRoot, 1f);

        if (GameManager.Instance != null)
            GameManager.Instance.TriggerDeath();
    }

    void ResolveTurn(int dir)
    {
        if (pc != null)
            pc.ExecuteTurn(dir, pendingTurnPos);

        // cursorPos/cursorRot already bent onto the new heading back in
        // BeginPendingTurn and generation has kept extending since — do
        // NOT reset them here, that would re-lay tiles that already exist.
        turnPending = false;
        turnArmed = false;
        turnMissed = false;
        // tilesSinceLastTurn/RollNextTurnTileCount deliberately NOT reset
        // here — see BeginPendingTurn. Resetting in this gameplay-domain
        // callback is what desynced the spacing from the tile count.
        suppressObstacleSpawnsUntil = Time.time + postTurnObstacleCooldown;
        if (turnTelegraphRoot != null)
            Destroy(turnTelegraphRoot);
    }

    // Turns start out rare right at turnUnlockLevel and get gradually more
    // frequent (shorter tile-count spacing) over the next turnRampLevels
    // levels, then hold at the floor — same shape as every other
    // difficulty ramp in the game, keyed off ZoneManager's level instead
    // of raw difficulty since that's what the player actually reads as
    // "getting harder" here.
    void RollNextTurnTileCount()
    {
        int level = ZoneManager.Instance != null ? ZoneManager.Instance.CurrentLevel : 1;
        float t = Mathf.Clamp01((level - turnUnlockLevel) / (float)Mathf.Max(1, turnRampLevels));

        int lo = Mathf.RoundToInt(Mathf.Lerp(tilesPerTurnMin, tilesPerTurnMinFloor, t));
        int hi = Mathf.RoundToInt(Mathf.Lerp(tilesPerTurnMax, tilesPerTurnMaxFloor, t));
        nextTurnTileCount = Random.Range(lo, hi);
    }

void SpawnTile()
{
    GameObject tile = SpawnNormalTile(cursorPos, cursorRot);
    lastSpawnedNormalTile = tile;
    if (disableNextTileLaneEdge)
    {
        disableNextTileLaneEdge = false;
        foreach (Transform child in tile.transform)
            if (child.name.StartsWith("LaneEdge"))
                child.gameObject.SetActive(false);
    }
    tilesSinceLastTurn++;

    // Occasionally mark a tile to break open as the player nears it,
    // instead of already being a gap from the moment it's spawned —
    // this tile looks completely normal until the player is right on
    // top of it. Locked out early-run, and kept clear of any nearby
    // pending/recent turn the same way regular obstacles are, so a pit
    // can never land right on top of a turn corner.
    bool pitsUnlocked = DifficultyManager.Instance != null &&
        DifficultyManager.Instance.runTime >= pitUnlockTime &&
        !IsObstacleSpawnSuppressed(cursorPos);
    float requiredSpacing = minPitSpacing;
    if (DifficultyManager.Instance != null)
        requiredSpacing = Mathf.Max(minPitSpacing,
            DifficultyManager.Instance.maxRunSpeed * minSecondsBetweenPits);
    if (pitsUnlocked && cursorPathDistance - lastPitEndDistance > requiredSpacing &&
        Random.value < pitChance)
    {
        float span = tileLength - 0.1f;
        activePits.Add(new PitRange {
            pos = cursorPos, forward = cursorRot * Vector3.forward, span = span });
        tile.AddComponent<BreakingGroundTile>().spawner = this;
        lastPitEndDistance = cursorPathDistance + span;
    }

    cursorPos += cursorRot * Vector3.forward * (tileLength - 0.1f);
    cursorPathDistance += tileLength - 0.1f;
}

GameObject SpawnNormalTile(Vector3 pos, Quaternion rot)
{
    GameObject tile = Instantiate(
        tilePrefab,
        pos,
        rot
    );
    tile.tag = "GroundTile";
    spawnedTiles.Enqueue(tile);

    if (tileMaterial != null)
    {
        Renderer[] renderers =
            tile.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            // The lane edge markers carry their own emissive material
            // (LaneEdgeLeft/Right) — they'd otherwise get silently
            // overwritten with the plain ground material below.
            if (r.gameObject.name.StartsWith("LaneEdge")) continue;

            Material[] mats = new Material[r.materials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = tileMaterial;
            r.materials = mats;
        }
    }

    return tile;
}

// Full-square (10x10) version of the normal tile (10x5), used once per
// turn at the pivot so the corner is fully paved in both directions.
GameObject SpawnCornerTile(Vector3 pos)
{
    GameObject tile = SpawnNormalTile(pos, cursorRot);
    Vector3 scale = tile.transform.localScale;
    scale.z *= 2f;
    tile.transform.localScale = scale;
    tilesSinceLastTurn++;

    // The corner tile is rotated to the EXIT heading only, before the
    // player has actually turned — its LaneEdge children would render
    // straight lines snapped to that new heading while the player is
    // still approaching in the old direction, reading as a kink instead
    // of a bend. A single flat rotated square can't have edges that line
    // up with both the approach and exit corridors at once. Grab its
    // material for reuse, then disable them...
    Material laneEdgeMat = null;
    foreach (Transform child in tile.transform)
    {
        if (!child.name.StartsWith("LaneEdge")) continue;
        if (laneEdgeMat == null)
        {
            Renderer r = child.GetComponent<Renderer>();
            if (r != null) laneEdgeMat = r.sharedMaterial;
        }
        child.gameObject.SetActive(false);
    }

    // ...and replace them with a genuine right-angle bend on each side,
    // tracing the corner square's own perimeter — a single diagonal cut
    // from the approach edge straight to the exit edge is NOT a 90-degree
    // turn and cuts across open platform.
    //
    // The square's own corners sit at pos +- approachRight*5 +- exitRight*5
    // (it's a 10x10 square centered on the pivot in BOTH the approach and
    // exit bases at once). For side = +-1 (the two lateral edges):
    //   near   = pos - approachForward*extend + approachRight*5*side
    //   corner = pos + approachRight*5*side    + exitRight*5*side
    //   far    = pos + exitForward*extend      + exitRight*5*side
    // near-corner-far are two straight segments meeting at the square's
    // own corner vertex — a real 90-degree bend, never inside the
    // platform. The adjacent normal tile on each side (last approach tile,
    // first exit tile) has its own LaneEdge disabled (see
    // lastSpawnedNormalTile / disableNextTileLaneEdge) since it would
    // otherwise double up with this line over their overlap — extend
    // reaches past that whole disabled tile (spacing 4.8 + half-length
    // 2.45) to meet the NEXT (still-enabled) tile's line with no gap.
    float extend = (tileLength - 0.1f) + tileLength * 0.5f;
    foreach (float side in new float[] { 1f, -1f })
    {
        Vector3 near = pos - pendingApproachForward * extend + pendingApproachRight * 5f * side;
        Vector3 corner = pos + pendingApproachRight * 5f * side + pendingExitRight * 5f * side;
        Vector3 far = pos + pendingExitForward * extend + pendingExitRight * 5f * side;
        MakeCornerEdgeMiter(near, corner, laneEdgeMat);
        MakeCornerEdgeMiter(corner, far, laneEdgeMat);
    }

    return tile;
}

void MakeCornerEdgeMiter(Vector3 a, Vector3 b, Material mat)
{
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = "CornerEdgeMiter";
    Destroy(go.GetComponent<Collider>());
    Vector3 delta = b - a;
    go.transform.position = (a + b) * 0.5f;
    go.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
    go.transform.localScale = new Vector3(0.1f, 0.01f, delta.magnitude);
    Renderer r = go.GetComponent<Renderer>();
    if (r != null && mat != null)
        r.sharedMaterial = mat;
    // Tracked in the same despawn queue as regular tiles so it gets
    // cleaned up in sync with the corner tile instead of leaking.
    spawnedTiles.Enqueue(go);
}

void SpawnTurnTelegraph()
{
    if (turnTelegraphRoot != null)
        Destroy(turnTelegraphRoot);

    // The banner TEXT still uses the direction color (TurnLeftColor/
    // TurnRightColor) — just the ground arrow itself is plain white.
    Color color = Color.white;
    turnTelegraphMaterial = CreateTurnTelegraphMaterial(color);
    turnTelegraphRoot = new GameObject(
        pendingTurnDirection < 0 ? "TurnTelegraph_Left" : "TurnTelegraph_Right");

    Vector3 side = pendingApproachRight * pendingTurnDirection;
    Vector3 signCenter = pendingTurnPos
        - pendingApproachForward * turnTelegraphLeadDistance
        + Vector3.up * 0.18f;

    // Track edges sit at +-5 (GroundTile's LaneEdge markers) — keep the
    // arrow inside that with a real margin so it never draws past the
    // actual platform boundary, regardless of viewing angle.
    float halfWidth = 3.5f;

    // Just a single clean chevron floating above the track — no approach
    // bar, no arc trail. Shaft + two head wings, angled back into a crisp
    // arrowhead shape rather than a boxy V.
    Vector3 arrowY = Vector3.up * 1.6f;
    Vector3 tail = signCenter - side * (halfWidth * 0.5f) + arrowY;
    Vector3 tip = signCenter + side * (halfWidth * 0.85f) + arrowY;
    MakeTelegraphSegment("TurnArrowShaft", tail, tip, 0.32f, color);
    Vector3 headBase = tip - side * 1.6f;
    MakeTelegraphSegment("TurnArrowHeadA", tip,
        headBase + pendingApproachForward * 1.1f,
        0.34f, color);
    MakeTelegraphSegment("TurnArrowHeadB", tip,
        headBase - pendingApproachForward * 1.1f,
        0.34f, color);

    GameObject lightObj = new GameObject("TurnTelegraphLight");
    lightObj.transform.SetParent(turnTelegraphRoot.transform, false);
    lightObj.transform.position = signCenter + Vector3.up * 2.2f;
    Light l = lightObj.AddComponent<Light>();
    l.type = LightType.Point;
    l.color = color;
    l.intensity = 6f;
    l.range = 14f;

    // Gentle pulse + bob so the arrow reads as an active signal rather
    // than a static prop, matching the lane lines' own pulse treatment.
    TurnArrowPulse pulse = turnTelegraphRoot.AddComponent<TurnArrowPulse>();
    pulse.baseColor = color;
    pulse.baseY = arrowY.y;
}

Material CreateTurnTelegraphMaterial(Color color)
{
    Shader shader = Shader.Find("NebulaDash/UnlitNoFog");
    if (shader == null)
        shader = Shader.Find("Universal Render Pipeline/Unlit");
    if (shader == null)
        shader = Shader.Find("Standard");

    Material mat = new Material(shader);
    SetTelegraphMaterialColor(mat, color);
    return mat;
}

void SetTelegraphMaterialColor(Material mat, Color color)
{
    if (mat == null) return;
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    if (mat.HasProperty("_EmissionColor"))
    {
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 3f);
    }
}

void UpdateTurnTelegraphColor(Color color)
{
    SetTelegraphMaterialColor(turnTelegraphMaterial, color);
    if (turnTelegraphRoot == null) return;

    foreach (Light l in turnTelegraphRoot.GetComponentsInChildren<Light>())
        l.color = color;
}

void MakeTelegraphSegment(string name, Vector3 a, Vector3 b,
    float thickness, Color color)
{
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(turnTelegraphRoot.transform, false);
    Vector3 delta = b - a;
    go.transform.position = (a + b) * 0.5f;
    go.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
    go.transform.localScale = new Vector3(thickness, thickness, delta.magnitude);
    Destroy(go.GetComponent<Collider>());

    Renderer r = go.GetComponent<Renderer>();
    if (r != null)
        r.sharedMaterial = turnTelegraphMaterial;
}

// Caps a hazard's forward lookahead distance so it can't land past a
// pending turn's pivot. ObjectSpawner/MeteoriteSpawner compute spawn
// position as player.position + player.forward * someDistance with no
// idea that the track stops existing past the pivot until the turn
// resolves — an ordinary (or reaction-time-scaled) lookahead distance
// routinely exceeds that remaining stretch, landing obstacles in open
// air beyond the corner where they're visible well before the player
// could have turned there. Only clamps when the pivot actually falls
// within the requested distance; otherwise returns it unchanged.
public float ClampAheadForTurn(Vector3 origin, Vector3 forward, float desiredDist, float buffer = 4f)
{
    if (!turnPending) return desiredDist;
    float distToPivot = Vector3.Dot(pendingTurnPos - origin, forward);
    if (distToPivot > 0f && distToPivot < desiredDist)
        return Mathf.Max(0f, distToPivot - buffer);
    return desiredDist;
}

public bool IsObstacleSpawnSuppressed(Vector3 worldPos, float extraBuffer = 0f)
{
    if (Time.time < suppressObstacleSpawnsUntil)
        return true;

    if (!turnPending)
        return false;

    float buffer = turnObstacleBufferDistance + extraBuffer;
    Vector3 fromTurn = worldPos - pendingTurnPos;

    float approachDist = Vector3.Dot(fromTurn, pendingApproachForward);
    float approachSide = Mathf.Abs(Vector3.Dot(fromTurn, pendingApproachRight));
    if (approachDist > -buffer && approachDist < tileLength &&
        approachSide < 5.5f + extraBuffer)
        return true;

    float exitDist = Vector3.Dot(fromTurn, pendingExitForward);
    float exitSide = Mathf.Abs(Vector3.Dot(fromTurn, pendingExitRight));
    return exitDist > -tileLength && exitDist < turnExitBufferDistance + extraBuffer &&
           exitSide < 5.5f + extraBuffer;
}
// Shared by both the pit break-open and the lightning crater: a burst of
// jagged rock chunks matching the ground's own material/texture, each
// falling away with real gravity + spin rather than a particle effect.
public void SpawnBreakDebris(Vector3 center, int minCount, int maxCount,
    float spreadX, float spreadZ, float colorBoost)
{
    Color groundColor = tileMaterial != null && tileMaterial.HasProperty("_BaseColor")
        ? tileMaterial.GetColor("_BaseColor")
        : new Color(0.25f, 0.22f, 0.2f);
    Color chunkColor = Color.Lerp(Color.white, groundColor, 0.35f) * colorBoost;

    int chunkCount = Random.Range(minCount, maxCount);
    for (int i = 0; i < chunkCount; i++)
    {
        GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(chunk.GetComponent<Collider>());

        float sx = Random.Range(0.4f, 1.5f);
        float sy = Random.Range(0.2f, 0.7f);
        float sz = Random.Range(0.4f, 1.5f);
        chunk.transform.position = center + new Vector3(
            Random.Range(-spreadX, spreadX), 0f,
            Random.Range(-spreadZ, spreadZ));
        chunk.transform.localScale = new Vector3(sx, sy, sz);
        chunk.transform.rotation = Random.rotation;

        Renderer r = chunk.GetComponent<Renderer>();
        Material m = new Material(tileMaterial != null
            ? tileMaterial : r.sharedMaterial);

        float shade = Random.Range(0.8f, 1.1f);
        m.SetColor("_BaseColor", chunkColor * shade);
        m.SetColor("_Color", chunkColor * shade);
        m.SetFloat("_Smoothness", 0.12f);

        if (rockAlbedo != null)
        {
            m.SetTexture("_BaseMap", rockAlbedo);
            m.SetTextureOffset("_BaseMap",
                new Vector2(Random.value, Random.value));
        }
        if (rockNormal != null)
        {
            m.SetTexture("_BumpMap", rockNormal);
            m.EnableKeyword("_NORMALMAP");
        }
        if (rockOcclusion != null)
            m.SetTexture("_OcclusionMap", rockOcclusion);

        r.material = m;
        chunk.AddComponent<FallingChunk>();
    }
}

void DespawnOldTiles()
{
    // Tried a forward-dot "is this tile actually behind the player" check
    // here — it's WRONG until the player has actually turned: right after
    // BeginPendingTurn bends the cursor, the new leg's tiles sit
    // perpendicular to the player's still-old heading, at whatever Z the
    // pivot happened to be. A pure forward-dot against the player's
    // current (pre-turn) heading reads a lower pivot Z as "behind" even
    // though those tiles are the turn itself, and wipes the entire new
    // leg the moment the player's Z creeps past the pivot's Z (e.g. while
    // falling forward off a missed turn) — worse than the count-cap bug
    // it was meant to fix.
    //
    // Back to a plain count cap, just sized to actually cover the worst
    // case instead of guessing low: a turn can have BOTH the remaining
    // stretch of the old leg (up to a full tilesAhead deep, if the player
    // hasn't reached the pivot yet) AND the new leg's own full lookahead
    // (also up to tilesAhead deep, generated ahead of time so the bend is
    // visible before the player arrives) alive at once. Sizing for only
    // one leg is what let DespawnOldTiles evict still-needed tiles out
    // from under the player. 3x leaves headroom beyond that 2x worst case.
    int maxKeep = tilesAhead * 3;
    while (spawnedTiles.Count > maxKeep)
    {
        GameObject tile = spawnedTiles.Dequeue();
        if (tile != null) Destroy(tile);
    }
}

}

// Sits on a normal-looking ground tile that's been marked to break open.
// The tile stays fully intact (solid mesh + collider, indistinguishable
// from any other tile) until the player is nearly on top of it, trembles
// briefly, then cracks apart into falling debris and removes itself —
// opening the real gap at that exact moment, in front of the player,
// instead of the hole having existed since it spawned hundreds of units
// out.
public class BreakingGroundTile : MonoBehaviour
{
    public GroundTileSpawner spawner;
    // Expressed as seconds of travel at the player's CURRENT speed so
    // the reaction window stays fair as speed climbs — but left
    // uncapped this pushed the tremor hundreds of units out at high
    // speed, well past where fog/render distance make it visible at
    // all. maxWarnDistance below keeps the whole sequence within a
    // range that's actually visible, even if that means less lead time
    // at extreme speed — being able to see it at all matters more than
    // a generous but invisible warning.
    public float reactionTime = 1.6f;   // seconds of warning before it breaks
    public float warnLeadTime = 2.2f;   // additional seconds of tremor before that
    public float minTriggerDistance = 6f;
    public float maxWarnDistance = 90f;
    // Guaranteed gap between "tremor starts" and "it breaks" — at high
    // speed, triggerDistance (speed * reactionTime) hit the maxWarnDistance
    // cap same as warnDistance did, collapsing the two to the same value.
    // The tremor window (warnDistance -> triggerDistance) vanished
    // entirely, so the tile went straight from normal to broken with no
    // visible telegraph at all — "the ground was just broken in front of
    // me." Reserving this window lets warnDistance push slightly past
    // maxWarnDistance rather than ever collapse onto triggerDistance.
    public float minTremorWindow = 20f;

    private Transform player;
    private PlayerController pc;
    private CameraFollow camFollow;
    private Vector3 restPos;
    private bool broken = false;
    private bool warned = false;
    private Renderer[] renderers;
    private Color[] baseColors;

    // Shared across every tile so if two ever tremor at once it still
    // reads as one earthquake, not a stacked burst.
    private static float lastQuakeTime = -99f;

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null) player = p.transform;
        pc = FindObjectOfType<PlayerController>();
        camFollow = FindObjectOfType<CameraFollow>();
        restPos = transform.position;

        renderers = GetComponentsInChildren<Renderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i].material.HasProperty("_BaseColor"))
                baseColors[i] = renderers[i].material.GetColor("_BaseColor");
    }

    void Update()
    {
        if (player == null || broken) return;

        float speed = pc != null ? pc.runSpeed : 15f;
        // Capping the break point at half of maxWarnDistance (rather
        // than the full visible range) meant that once speed pushed
        // speed*reactionTime past that halved ceiling — which happens
        // at fairly moderate speed, well before max — the actual
        // "already broken, still time to jump" window kept shrinking
        // the faster the run got, down to a fraction of a second at
        // high speed. The break point only needs to stay within
        // maxWarnDistance (still visible) — it doesn't need to stay
        // within half of it.
        float triggerDistance = Mathf.Min(maxWarnDistance,
            Mathf.Max(minTriggerDistance, speed * reactionTime));
        float warnDistance = Mathf.Max(
            triggerDistance + minTremorWindow,
            Mathf.Min(maxWarnDistance, triggerDistance + speed * warnLeadTime));

        // Distance along THIS tile's own heading (fixed at spawn), not
        // raw world Z — the track can bend 90 degrees after this tile was
        // laid, but the tile itself never moves, so its own forward is
        // always the correct "ahead" axis for it.
        float zAhead = Vector3.Dot(transform.position - player.position, transform.forward);
        if (zAhead <= 0f || zAhead > warnDistance) return;

        if (!warned)
        {
            // One clear, unmissable cue the instant the warning window
            // opens — the tremor/tint alone were too subtle to notice
            // in front of the player at speed.
            warned = true;
            if (camFollow != null)
                camFollow.DangerPulse();
        }

        if (zAhead < triggerDistance)
        {
            Break();
            return;
        }

        // Tremor grows from nothing at warnDistance to strongest right
        // before the break — a "starting to give way" tell. Scaled up
        // substantially from the original 0.04 max, which was nearly
        // invisible against the track at any real speed.
        float closeness = 1f - Mathf.InverseLerp(
            triggerDistance, warnDistance, zAhead);
        float wobble = closeness * closeness * 0.4f;
        transform.position = restPos + new Vector3(
            Random.Range(-wobble, wobble),
            Random.Range(0f, wobble * 0.5f),
            0f);

        // Tint toward hot orange-red as it gets closer to breaking, on
        // top of the shake, so the tile itself visibly telegraphs danger
        // rather than relying on motion alone.
        Color warnColor = new Color(1f, 0.25f, 0.05f);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].material.HasProperty("_BaseColor")) continue;
            renderers[i].material.SetColor("_BaseColor",
                Color.Lerp(baseColors[i], warnColor, closeness));
        }
    }

    void Break()
    {
        broken = true;

        if (Time.time - lastQuakeTime > 0.3f)
        {
            lastQuakeTime = Time.time;
            if (ScreenShake.Instance != null)
                ScreenShake.Instance.Shake(0.35f, 0.12f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayGroundBreak();
        }

        if (spawner != null)
            spawner.SpawnBreakDebris(restPos, 8, 14, 4.3f, 2f, 1f);

        // Removes the mesh and collider together — the gap opens exactly
        // now, not before.
        Destroy(gameObject);
    }
}

// Reusable fall physics for any broken rock chunk (pit break or lightning
// crater): real accelerating fall + tumble instead of a particle effect,
// self-destroying once it's dropped out of sight.
public class FallingChunk : MonoBehaviour
{
    public float gravity = 14f;
    public float fallOutDepth = 6f;

    private float restY;
    private float fallVelocity;
    private Vector3 drift;
    private Vector3 spinAxis;
    private float spinSpeed;

    void Start()
    {
        restY = transform.position.y;
        fallVelocity = Random.Range(0f, 1.5f);
        drift = new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
        spinAxis = Random.onUnitSphere;
        spinSpeed = Random.Range(150f, 400f);
    }

    void Update()
    {
        fallVelocity += gravity * Time.deltaTime;
        transform.position += new Vector3(
            drift.x * Time.deltaTime,
            -fallVelocity * Time.deltaTime,
            drift.z * Time.deltaTime);
        transform.rotation *= Quaternion.AngleAxis(
            spinSpeed * Time.deltaTime, spinAxis);

        if (restY - transform.position.y > fallOutDepth)
            Destroy(gameObject);
    }
}

// Sits on the turn telegraph's root: a gentle bob + emission pulse so the
// arrow reads as an active signal instead of a static prop, same treatment
// as the lane lines' own pulse (LaneLinePulse).
public class TurnArrowPulse : MonoBehaviour
{
    public Color baseColor = Color.white;
    public float baseY = 1.6f;

    private Renderer[] renderers;
    private Vector3[] localPositions;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        localPositions = new Vector3[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            localPositions[i] = transform.GetChild(i).localPosition;
    }

    void Update()
    {
        float pulse = Mathf.Sin(Time.time * 4f) * 0.5f + 0.5f;
        float bob = Mathf.Sin(Time.time * 2.5f) * 0.15f;

        foreach (Renderer r in renderers)
        {
            if (!r.material.HasProperty("_EmissionColor")) continue;
            r.material.SetColor("_EmissionColor",
                baseColor * Mathf.Lerp(2f, 4f, pulse));
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "TurnTelegraphLight") continue;
            child.localPosition = localPositions[i] + Vector3.up * bob;
        }
    }
}
