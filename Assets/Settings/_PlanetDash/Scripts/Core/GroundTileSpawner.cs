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
    private float nextSpawnZ = 0f;

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

    // World-Z (start, end) ranges of open gaps, so ObjectSpawner can keep
    // obstacles from landing on top of a hole the player has to jump.
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
    nextSpawnZ = player.position.z - 10f;
    // No pit until the player has had a fair unbroken stretch to start on.
    lastPitEndZ = nextSpawnZ;
    for (int i = 0; i < 20; i++)
        SpawnTile();
}
    void Update()
    {
        if (player == null) return;

        while (nextSpawnZ < player.position.z + (tilesAhead * tileLength))
            SpawnTile();

        DespawnOldTiles();
        activePits.RemoveAll(p => p.y < player.position.z - tileLength * 2);
    }

void SpawnTile()
{
    GameObject tile = SpawnNormalTile(nextSpawnZ);

    // Occasionally mark a tile to break open as the player nears it,
    // instead of already being a gap from the moment it's spawned —
    // this tile looks completely normal until the player is right on
    // top of it. Locked out early-run.
    bool pitsUnlocked = DifficultyManager.Instance != null &&
        DifficultyManager.Instance.runTime >= pitUnlockTime;
    float requiredSpacing = minPitSpacing;
    if (DifficultyManager.Instance != null)
        requiredSpacing = Mathf.Max(minPitSpacing,
            DifficultyManager.Instance.maxRunSpeed * minSecondsBetweenPits);
    if (pitsUnlocked && nextSpawnZ - lastPitEndZ > requiredSpacing &&
        Random.value < pitChance)
    {
        float pitEnd = nextSpawnZ + tileLength - 0.1f;
        activePits.Add(new Vector2(nextSpawnZ, pitEnd));
        tile.AddComponent<BreakingGroundTile>().spawner = this;
        lastPitEndZ = pitEnd;
    }

    nextSpawnZ += tileLength - 0.1f;
}

GameObject SpawnNormalTile(float z)
{
    GameObject tile = Instantiate(
        tilePrefab,
        new Vector3(0, 0, z),
        Quaternion.identity
    );
    tile.tag = "GroundTile";

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
    GameObject[] tiles =
        GameObject.FindGameObjectsWithTag("GroundTile");
    foreach (GameObject tile in tiles)
    {
        if (tile.transform.position.z < player.position.z - tileLength * 2)
            Destroy(tile);
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