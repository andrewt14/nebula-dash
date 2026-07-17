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
    private float lastPitEndZ = -9999f;

    [Header("Broken Edge Look")]
    public Texture2D rockAlbedo;
    public Texture2D rockNormal;
    public Texture2D rockOcclusion;

    [Header("90-Degree Turns")]
    // Turns only start appearing once the run has settled in, same spirit
    // as the old TurnGate's late unlock — this is the biggest read/
    // commitment in the game (a wrong or missed swipe runs the player off
    // the track entirely), so it shouldn't show up early.
    public float turnUnlockTime = 100f;
    public int tilesPerTurnMin = 110;
    public int tilesPerTurnMax = 170;
    // Reaction window, expressed as seconds of travel at the player's
    // current speed — matches the pattern used for every other
    // late-telegraphed hazard.
    public float turnReactionTime = 3.5f;
    private int tilesSinceLastTurn = 0;
    private int nextTurnTileCount;
    private bool turnPending = false;
    private Vector3 pendingTurnPos;
    private int pendingTurnDirection;
    // Generation can freeze well ahead of the player (up to the full
    // tilesAhead lookahead buffer) — the "SWIPE LEFT/RIGHT" banner only
    // fires once the player is actually within range of the reaction
    // window, so there's no dead stretch where the banner is up but a
    // swipe doesn't do anything yet.
    private bool turnBannerShown = false;
    // Once the track has turned once, world Z/X are no longer a stable
    // "along track"/"lane" pair for pit bookkeeping (a pit's world-Z
    // range only means "a specific point on the track" while the track
    // is still running along Z) — pits are a pre-turn-only hazard rather
    // than teaching the whole pit system to reason about arbitrary
    // headings.
    private bool hasTurnedOnce = false;

    private static readonly Color TurnLeftColor = new Color(0.15f, 0.55f, 1f);
    private static readonly Color TurnRightColor = new Color(1f, 0.55f, 0.1f);

    // World-Z (start, end) ranges of open gaps, so ObjectSpawner can keep
    // obstacles from landing on top of a hole the player has to jump.
    // Only ever populated pre-first-turn — see hasTurnedOnce above.
    private static readonly List<Vector2> activePits = new List<Vector2>();

    public static bool IsInsidePit(float z, float margin = 3f)
    {
        foreach (Vector2 pit in activePits)
            if (z > pit.x - margin && z < pit.y + margin)
                return true;
        return false;
    }

void Start()
{
    Instance = this;
    pc = player.GetComponent<PlayerController>();
    cursorPos = new Vector3(0, 0, player.position.z - 10f);
    cursorRot = Quaternion.identity;
    // No pit until the player has had a fair unbroken stretch to start on.
    lastPitEndZ = cursorPos.z;
    nextTurnTileCount = Random.Range(tilesPerTurnMin, tilesPerTurnMax);
    for (int i = 0; i < 20; i++)
        SpawnTile();

    PlayerController.OnSwipeDirection += HandleSwipe;
}

void OnDestroy()
{
    PlayerController.OnSwipeDirection -= HandleSwipe;
}

    void Update()
    {
        if (player == null) return;

        bool turnsUnlocked = DifficultyManager.Instance != null &&
            DifficultyManager.Instance.runTime >= turnUnlockTime;

        while (!turnPending &&
            Vector3.Dot(cursorPos - player.position, player.transform.forward)
                < tilesAhead * tileLength)
        {
            if (turnsUnlocked && tilesSinceLastTurn >= nextTurnTileCount)
            {
                BeginPendingTurn();
                break;
            }
            SpawnTile();
        }

        // Generation can freeze far ahead of the player (up to the full
        // lookahead buffer) — only show the telegraph once they're
        // actually close enough for a swipe to start counting, matching
        // HandleSwipe's own window so there's no dead stretch where the
        // banner is up but input doesn't do anything yet.
        if (turnPending && !turnBannerShown)
        {
            float aheadDist = Vector3.Dot(
                pendingTurnPos - player.position, player.transform.forward);
            float speed = pc != null ? pc.runSpeed : 15f;
            float leadDistance = Mathf.Max(20f, speed * turnReactionTime) * 1.15f;
            if (aheadDist < leadDistance)
            {
                turnBannerShown = true;
                if (ScorePopup.Instance != null)
                    ScorePopup.Instance.ShowTopBanner(
                        pendingTurnDirection < 0 ? "< TURN LEFT" : "TURN RIGHT >",
                        2.5f,
                        pendingTurnDirection < 0 ? TurnLeftColor : TurnRightColor);
            }
        }

        DespawnOldTiles();
        activePits.RemoveAll(p => p.y < player.position.z - tileLength * 2);
    }

    // Stops generation dead at the current cursor and marks it as the
    // corner — a matching swipe within the reaction window (HandleSwipe)
    // resolves it and resumes generation along the new heading; missing
    // it means the track simply never extends further in the old
    // direction, and the player runs off the edge into fallDeathY.
    void BeginPendingTurn()
    {
        turnPending = true;
        turnBannerShown = false;
        pendingTurnPos = cursorPos;
        pendingTurnDirection = Random.value < 0.5f ? -1 : 1;
    }

    void HandleSwipe(int dir)
    {
        if (!turnPending || dir != pendingTurnDirection || player == null) return;

        float aheadDist = Vector3.Dot(
            pendingTurnPos - player.position, player.transform.forward);
        float speed = pc != null ? pc.runSpeed : 15f;
        float window = Mathf.Max(20f, speed * turnReactionTime);
        // Small negative allowance so a swipe landing right as the player
        // reaches the corner still counts, not just ones well in advance.
        if (aheadDist > -5f && aheadDist < window)
            ResolveTurn(dir);
    }

    void ResolveTurn(int dir)
    {
        if (pc != null)
            pc.ExecuteTurn(dir, pendingTurnPos);

        cursorPos = pendingTurnPos;
        cursorRot = Quaternion.LookRotation(player.transform.forward, Vector3.up);
        turnPending = false;
        tilesSinceLastTurn = 0;
        nextTurnTileCount = Random.Range(tilesPerTurnMin, tilesPerTurnMax);

        if (!hasTurnedOnce)
        {
            hasTurnedOnce = true;
            activePits.Clear();
        }
    }

void SpawnTile()
{
    GameObject tile = SpawnNormalTile(cursorPos, cursorRot);
    tilesSinceLastTurn++;

    // Occasionally mark a tile to break open as the player nears it,
    // instead of already being a gap from the moment it's spawned —
    // this tile looks completely normal until the player is right on
    // top of it. Locked out early-run, and for good post-turn (see
    // hasTurnedOnce).
    bool pitsUnlocked = !hasTurnedOnce &&
        DifficultyManager.Instance != null &&
        DifficultyManager.Instance.runTime >= pitUnlockTime;
    float requiredSpacing = minPitSpacing;
    if (DifficultyManager.Instance != null)
        requiredSpacing = Mathf.Max(minPitSpacing,
            DifficultyManager.Instance.maxRunSpeed * minSecondsBetweenPits);
    if (pitsUnlocked && cursorPos.z - lastPitEndZ > requiredSpacing &&
        Random.value < pitChance)
    {
        float pitEnd = cursorPos.z + tileLength - 0.1f;
        activePits.Add(new Vector2(cursorPos.z, pitEnd));
        tile.AddComponent<BreakingGroundTile>().spawner = this;
        lastPitEndZ = pitEnd;
    }

    cursorPos += cursorRot * Vector3.forward * (tileLength - 0.1f);
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
            Material[] mats = new Material[r.materials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = tileMaterial;
            r.materials = mats;
        }
    }

    return tile;
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
    // Count-based cap instead of a world-Z cutoff — a Z cutoff silently
    // stops despawning anything once the track turns and world Z stops
    // growing along the path. FIFO order is still "oldest/furthest-
    // behind first" regardless of how many times the track has turned,
    // since tiles dequeue in the exact order they were generated.
    int maxKeep = tilesAhead + 20;
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
        float warnDistance = Mathf.Min(maxWarnDistance,
            triggerDistance + speed * warnLeadTime);

        // Pits only ever exist on the pre-first-turn straight stretch
        // (see GroundTileSpawner.hasTurnedOnce), where world Z is still
        // a valid "along track" coordinate.
        float zAhead = transform.position.z - player.position.z;
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
