using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Owns the background look and progression mood. The run cycles through
// neon zones every zoneLength meters (looping forever, so colors keep
// changing). Each zone sets ambient/fog/light, tints the skybox, recolors
// the flying background UFOs and the side lane lines, and bumps difficulty
// on entry. The player's suit rim stays constant white.
public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance;
    // Read by ResourceOrb so pickups tint with the current zone's sky.
    public static Color CurrentSkyColor = new Color(0.6f, 0f, 1f);

    private class Zone
    {
        public string name;
        public Color ambient;
        public Color fog;
        public float fogDensity;
        public Color lightColor;
        public float lightIntensity;
        public Color skyTint;
        public Color ufoColor;    // HDR so it blooms
        public Color lineColor;   // side lane lines
    }

    public float transitionDuration = 4f;
    public float zoneDuration = 16f;  // seconds of play per zone before advancing
    public GameObject ufoModel;       // ufo.glb; falls back to procedural if unset
    public Vector3 ufoModelEuler = new Vector3(-90f, 0f, 0f); // lay the saucer flat

    private Zone[] zones;
    private int lastLevel = -1;
    // 1-indexed to match the on-screen "LEVEL N" banner text below —
    // read by GroundTileSpawner to gate when 90-degree turns unlock.
    public int CurrentLevel => lastLevel + 1;
    private Transform player;
    private PlayerController playerController;
    private Light dirLight;
    private Material skyboxMat;
    // Zone-driven fog density before WeatherManager's storm multiplier is
    // applied — kept separate so the storm boost can update every frame
    // instead of only during the ~4s zone transition window.
    private float baseFogDensity = 0.01f;

    private readonly List<Transform> ufos = new List<Transform>();
    private readonly List<Material> ufoMats = new List<Material>();
    private readonly List<Vector3> ufoCenters = new List<Vector3>();
    private readonly List<Vector3> ufoRadii = new List<Vector3>();
    private readonly List<float> ufoSpeed = new List<float>();
    private readonly List<float> ufoPhase = new List<float>();
    private readonly List<float> ufoSpin = new List<float>();

    private Coroutine transition;

    void Awake()
    {
        Instance = this;
        BuildZones();
    }

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null)
        {
            player = p.transform;
            playerController = p.GetComponent<PlayerController>();
        }

        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { dirLight = l; break; }

        // Instance the skybox so per-zone tinting doesn't dirty the asset.
        if (RenderSettings.skybox != null)
        {
            skyboxMat = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyboxMat;
        }

        RenderSettings.fog = true;
        CreateUFOs();

        lastLevel = 0;
        ApplyZone(zones[0], 1f);           // snap to opening zone
        ApplyDifficultyBonus(0);

        // Announce the opening zone too, so the run visibly starts at
        // "LEVEL 1" instead of silently applying it and only ever
        // announcing from LEVEL 2 onward.
        // ShowTopBanner (fixed screen position), not ShowPopup — ShowPopup
        // projects a WORLD position through the camera, and Vector3.zero
        // is the track's start line. Once the run has travelled any real
        // distance from there, that world point sits far behind the
        // camera and WorldToScreenPoint stops producing anything on
        // screen at all, so this banner would silently stop appearing.
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowTopBanner(
                "LEVEL 1: " + zones[0].name, 2.0f, zones[0].skyTint);
    }

    void Update()
    {
        float t = DifficultyManager.Instance != null
            ? DifficultyManager.Instance.runTime : 0f;

        // Advance (and loop) zones by play time so colors keep changing
        // on a steady cadence regardless of the score value.
        int level = Mathf.FloorToInt(t / Mathf.Max(1f, zoneDuration));
        if (level > lastLevel)
        {
            lastLevel = level;
            Zone z = ZoneForLevel(level);
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(TransitionTo(z));
            ApplyDifficultyBonus(level);

            // Persist the highest zone level ever reached, so the main
            // menu can show which named zones the player has actually
            // unlocked vs not yet reached.
            int bestLevel = PlayerPrefs.GetInt("BestZoneLevel", 0);
            if (level > bestLevel)
            {
                PlayerPrefs.SetInt("BestZoneLevel", level);
                PlayerPrefs.Save();
            }

            // Cosmetic milestone banner — same zone/difficulty cadence
            // that already exists, just announced on screen. Longer hold
            // and a glowing tint (matching the new zone's own palette)
            // so it reads as a bigger event than a quick pickup blip.
            if (ScorePopup.Instance != null)
                ScorePopup.Instance.ShowTopBanner(
                    "LEVEL " + (level + 1) + ": " + z.name, 2.0f, z.skyTint);
        }

        // Fog keeps thickening with difficulty on top of the per-zone
        // base value, capped well short of unreadable so it reads as
        // "the run is getting harder" instead of just cycling 3 fixed
        // looks forever.
        float difficultyFog = 1f;
        if (DifficultyManager.Instance != null)
            difficultyFog += Mathf.Clamp01(
                DifficultyManager.Instance.currentDifficulty / 300f) * 0.7f;
        RenderSettings.fogDensity =
            baseFogDensity * difficultyFog * WeatherManager.StormFogMultiplier;

        UpdateUFOs();
    }

    void BuildZones()
    {
        zones = new[]
        {
            new Zone {
                name = "Nebula Drift",
                ambient = new Color(0.40f, 0.25f, 0.55f),
                fog = new Color(0.15f, 0.06f, 0.26f), fogDensity = 0.010f,
                lightColor = new Color(0.85f, 0.60f, 1.00f), lightIntensity = 0.9f,
                skyTint = new Color(0.80f, 0.35f, 0.95f),
                ufoColor = new Color(0.60f, 0.30f, 1.00f) * 1.8f,
                lineColor = new Color(1.00f, 0.30f, 1.00f) },
            new Zone {
                name = "Station Corridor",
                ambient = new Color(0.26f, 0.34f, 0.52f),
                fog = new Color(0.06f, 0.12f, 0.22f), fogDensity = 0.014f,
                lightColor = new Color(0.55f, 0.80f, 1.00f), lightIntensity = 0.85f,
                skyTint = new Color(0.35f, 0.60f, 1.00f),
                ufoColor = new Color(0.30f, 0.65f, 1.00f) * 1.8f,
                lineColor = new Color(0.30f, 0.80f, 1.00f) },
            new Zone {
                name = "Deep Void",
                ambient = new Color(0.22f, 0.12f, 0.30f),
                fog = new Color(0.05f, 0.00f, 0.09f), fogDensity = 0.012f,
                lightColor = new Color(0.70f, 0.30f, 0.65f), lightIntensity = 0.8f,
                skyTint = new Color(0.90f, 0.25f, 0.60f),
                ufoColor = new Color(1.00f, 0.15f, 0.60f) * 1.9f,
                lineColor = new Color(1.00f, 0.20f, 0.60f) },
        };
    }

    // A much larger pool of uniquely named zones (same sci-fi/space
    // naming style as the original 3), read independently of the color
    // palette below. Previously names were generated as "<base> +N" —
    // with only 3 base palettes and a 16s zoneDuration, that suffix
    // showed up every 48 seconds. This pool covers ~13 minutes of play
    // before any name repeats, so a run essentially never sees one.
    public static readonly string[] ZoneNamePool = {
        "Nebula Drift", "Station Corridor", "Deep Void", "Ion Storm",
        "Asteroid Belt", "Solar Flare Reach", "Crystal Caverns",
        "Wormhole Passage", "Comet Trail", "Plasma Fields",
        "Quantum Rift", "Stellar Nursery", "Dark Matter Expanse",
        "Photon Stream", "Meteor Shower", "Gravity Well",
        "Cosmic Dust Cloud", "Binary Star System", "Event Horizon",
        "Nova Remnant", "Pulsar Field", "Void Corridor",
        "Starlight Cascade", "Ecliptic Drift", "Solar Wind Channel",
        "Magnetosphere Breach", "Singularity Edge", "Aurora Belt",
        "Deep Space Relay", "Celestial Rift",
    };

    // The 3 base color palettes loop and rotate hue every full pass, so
    // the LOOK keeps visibly evolving on its original fast cadence —
    // only the NAME now comes from the much larger pool above, so it
    // doesn't repeat (or get a "+N" suffix) on the same short cycle.
    Zone ZoneForLevel(int level)
    {
        Zone b = zones[level % zones.Length];
        int cycle = level / zones.Length;
        string name = ZoneNamePool[level % ZoneNamePool.Length];
        if (cycle <= 0) return new Zone {
            name = name, ambient = b.ambient, fog = b.fog,
            fogDensity = b.fogDensity, lightColor = b.lightColor,
            lightIntensity = b.lightIntensity, skyTint = b.skyTint,
            ufoColor = b.ufoColor, lineColor = b.lineColor,
        };

        float shift = cycle * 0.11f;
        return new Zone {
            name = name,
            ambient = ShiftHue(b.ambient, shift),
            fog = ShiftHue(b.fog, shift),
            fogDensity = b.fogDensity,
            lightColor = ShiftHue(b.lightColor, shift),
            lightIntensity = b.lightIntensity,
            skyTint = ShiftHue(b.skyTint, shift),
            ufoColor = ShiftHue(b.ufoColor, shift),
            lineColor = ShiftHue(b.lineColor, shift),
        };
    }

    // Hue-rotate a color while preserving its HDR magnitude (so bloom
    // intensity survives the shift).
    static Color ShiftHue(Color c, float shift)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b), 1f);
        Color.RGBToHSV(new Color(c.r / max, c.g / max, c.b / max),
            out float h, out float s, out float v);
        h = Mathf.Repeat(h + shift, 1f);
        Color outc = Color.HSVToRGB(h, s, v) * max;
        outc.a = c.a;
        return outc;
    }

    // Difficulty/speed is tied directly to level progression now — each
    // level-up gives a clear, escalating jump (bigger at higher levels)
    // instead of relying on the continuous per-second ramp alone, so
    // "getting harder" reads as "leveling up", matching how the rest of
    // the run (zone banners, environment) already progresses in levels.
    void ApplyDifficultyBonus(int level)
    {
        if (DifficultyManager.Instance == null) return;
        float bump = 2.8f + level * 0.35f;
        DifficultyManager.Instance.currentDifficulty += bump;
    }

    // Read by CameraFollow so the "dark moment" mood shows up as a
    // vignette (tunnel-vision) closing in around a readable center
    // instead of dimming the actual scene lighting the player has to
    // spot obstacles against.
    public static float DarknessAmount { get; private set; }

    // t = 1 snaps to the zone; the coroutine feeds partial t while easing.
    void ApplyZone(Zone z, float t)
    {
        // Was floored at 0.68 — even that read as too dark to reliably
        // spot obstacles against, especially stacked with a storm's fog
        // thickening. The moodier "everything's getting dark" feeling
        // now lives in DarknessAmount -> CameraFollow's vignette instead
        // of the actual scene lighting.
        float prog = DifficultyManager.Instance != null
            ? Mathf.Clamp01(DifficultyManager.Instance.currentDifficulty / 100f)
            : 0f;
        float dim = Mathf.Lerp(1f, 0.85f, prog);
        DarknessAmount = Mathf.Lerp(DarknessAmount, prog, t);

        RenderSettings.ambientLight = Color.Lerp(
            RenderSettings.ambientLight, z.ambient * dim, t);
        RenderSettings.fogColor = Color.Lerp(
            RenderSettings.fogColor, z.fog, t);
        baseFogDensity = Mathf.Lerp(baseFogDensity, z.fogDensity, t);

        if (dirLight != null)
        {
            dirLight.color = Color.Lerp(dirLight.color, z.lightColor, t);
            dirLight.intensity = Mathf.Lerp(
                dirLight.intensity, z.lightIntensity * dim, t);
        }

        TintSky(z.skyTint, t);
        CurrentSkyColor = Color.Lerp(CurrentSkyColor, z.skyTint, t);

        foreach (Material m in ufoMats)
        {
            // Model UFOs glow via _RimColor; procedural ones via _BaseColor.
            if (m.HasProperty("_RimColor"))
                m.SetColor("_RimColor", Color.Lerp(
                    m.GetColor("_RimColor"), z.ufoColor, t));
            else if (m.HasProperty("_GlowColor"))
                m.SetColor("_GlowColor", Color.Lerp(
                    m.GetColor("_GlowColor"), z.ufoColor, t));
            else if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", Color.Lerp(
                    m.GetColor("_BaseColor"), z.ufoColor, t));
        }

        // Looked up live instead of cached once in Start() — lane lines
        // now live on individual ground tiles (see GroundTile.prefab's
        // LaneEdgeLeft/Right) and get destroyed/respawned continuously as
        // tiles cycle, so a one-time cache would go stale within seconds.
        // Zone changes are infrequent (every zoneDuration), so this scan
        // is cheap enough to redo each time.
        foreach (LaneLinePulse line in FindObjectsByType<LaneLinePulse>(FindObjectsSortMode.None))
            line.baseColor = Color.Lerp(line.baseColor, z.lineColor, t);
    }

    void TintSky(Color c, float t)
    {
        if (skyboxMat == null) return;
        if (skyboxMat.HasProperty("_Tint"))
            skyboxMat.SetColor("_Tint", Color.Lerp(
                skyboxMat.GetColor("_Tint"), c, t));
        else if (skyboxMat.HasProperty("_SkyTint"))
            skyboxMat.SetColor("_SkyTint", Color.Lerp(
                skyboxMat.GetColor("_SkyTint"), c, t));
    }

    IEnumerator TransitionTo(Zone z)
    {
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float step = Time.deltaTime / Mathf.Max(0.01f, transitionDuration);
            ApplyZone(z, Mathf.Clamp01(step * 2.5f));
            yield return null;
        }
        ApplyZone(z, 1f);
    }

    // Procedural saucers (disc + dome). Swap the visual for a real model
    // later by replacing the child meshes; flight/tint logic is unchanged.
    void CreateUFOs()
    {
        // Back to the original hover-and-bob layout/motion (liked visually)
        // — c.x/c.y/c.z are literal local offsets (right/up/forward) from
        // the player, not an angle/distance pair. The only change from the
        // very original version is WHERE they're anchored from (see
        // UpdateUFOs' basePos, and player.forward/right as the projection
        // basis) — that's what keeps them correctly arranged through a
        // turn instead of freezing/clustering, without changing how they
        // actually look or move.
        Vector3[] centers =
        {
            new Vector3(-65f, 32f, 100f),
            new Vector3(65f, 48f, 155f),
            new Vector3(-80f, 62f, 210f),
            new Vector3(85f, 40f, 260f),
        };
        Vector3[] radii =
        {
            new Vector3(16f, 6f, 10f),
            new Vector3(22f, 8f, 14f),
            new Vector3(26f, 10f, 18f),
            new Vector3(18f, 7f, 12f),
        };
        float[] sizes = { 14f, 18f, 12f, 16f };
        float[] speeds = { 0.6f, 0.42f, 0.5f, 0.45f };

        Shader ufoShader = Shader.Find("NebulaDash/SkyPlanet");
        Color start = new Color(0.60f, 0.30f, 1.00f) * 1.8f;

        for (int i = 0; i < centers.Length; i++)
        {
            GameObject root = new GameObject("BackgroundUFO_" + i);
            float size = sizes[i];

            if (ufoModel != null)
                BuildModelUFO(root.transform, size, start, ufoShader);
            else
                BuildProceduralUFO(root.transform, size, start, ufoShader);

            root.AddComponent<UFOLightningBolt>();

            ufos.Add(root.transform);
            ufoCenters.Add(centers[i]);
            ufoRadii.Add(radii[i]);
            ufoSpeed.Add(speeds[i]);
            ufoPhase.Add(Random.Range(0f, 6.28f));
            ufoSpin.Add(Random.Range(30f, 70f) * (i % 2 == 0 ? 1f : -1f));
        }
    }

    // Real ufo.glb: instantiate, auto-fit to `size` world units, and
    // recolor every part with one fog-immune glow material so the whole
    // saucer tints per zone and stays visible against the sky.
    void BuildModelUFO(Transform parent, float size, Color col, Shader shader)
    {
        GameObject model = Instantiate(ufoModel, parent, false);

        // ufoModel (UFO.prefab) doubles as the actual UFOObstacle hazard
        // spawned by ObjectSpawner — that script's own Update() drives the
        // transform to its hoverHeight (~1.5, ground level) and can
        // TriggerDeath() on the player. Left on a purely decorative
        // background copy, it fights UpdateUFOs' own high-altitude orbit
        // positioning every frame (whichever Update() happens to run last
        // wins) and makes the background prop lethal. This copy is
        // decoration only — same reasoning as MenuBackgroundUFO's separate
        // no-gameplay script for the main menu's saucer.
        UFOObstacle stray = model.GetComponentInChildren<UFOObstacle>(true);
        if (stray != null) Destroy(stray);

        model.transform.localPosition = Vector3.zero;
        // Correct the imported orientation so the saucer lies flat.
        model.transform.localRotation = Quaternion.Euler(ufoModelEuler);
        model.transform.localScale = Vector3.one;

        Renderer[] rends = model.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { BuildProceduralUFO(parent, size, col, shader); return; }

        // Normalize to `size` regardless of the model's native scale.
        Bounds b = rends[0].bounds;
        for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
        float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (maxDim > 0.0001f)
            model.transform.localScale = Vector3.one * (size / maxDim);

        // Keep the model's ORIGINAL material (texture intact) and append
        // an additive fresnel-glow pass tinted per zone — same technique
        // as the player's suit rim, so the texture always shows through.
        Shader rimShader = Shader.Find("NebulaDash/SuitRimGlow");
        Material glowMat = new Material(rimShader != null
            ? rimShader : Shader.Find("Universal Render Pipeline/Unlit"));
        if (glowMat.HasProperty("_RimColor")) glowMat.SetColor("_RimColor", col);
        if (glowMat.HasProperty("_RimStrength")) glowMat.SetFloat("_RimStrength", 2.8f);
        if (glowMat.HasProperty("_RimPower")) glowMat.SetFloat("_RimPower", 2.2f);

        foreach (Renderer r in rends)
        {
            Material[] orig = r.sharedMaterials;
            Material[] mats = new Material[orig.Length + 1];
            for (int k = 0; k < orig.Length; k++) mats[k] = orig[k];
            mats[orig.Length] = glowMat;      // extra additive glow pass
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        ufoMats.Add(glowMat);
    }

    void BuildProceduralUFO(Transform parent, float size, Color col, Shader shader)
    {
        GameObject body = MakePart(parent, shader, col);
        body.transform.localScale = new Vector3(size, size * 0.28f, size);

        GameObject dome = MakePart(parent, shader, col);
        dome.transform.localScale = Vector3.one * (size * 0.5f);
        dome.transform.localPosition = new Vector3(0f, size * 0.16f, 0f);
    }

    GameObject MakePart(Transform parent, Shader shader, Color col)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);

        Material m = new Material(shader != null
            ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", col);
        Renderer r = part.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ufoMats.Add(m);
        return part;
    }

    void UpdateUFOs()
    {
        if (player == null) return;

        // Anchor off the player's lane-centered, ground-level base
        // position, not the raw transform — player.position also carries
        // the in-lane strafe offset and jump/duck bob, which made every
        // background UFO visibly sway/bounce in lockstep with the
        // player's own left/right and up/down movement.
        float laneOffset = playerController != null
            ? playerController.GetCurrentLaneOffset() : 0f;
        float baseY = playerController != null
            ? playerController.GetGroundY() : player.position.y;
        Vector3 basePos = player.position - player.right * laneOffset;
        basePos.y = baseY;

        for (int i = 0; i < ufos.Count; i++)
        {
            float t = Time.time * ufoSpeed[i] + ufoPhase[i];
            Vector3 c = ufoCenters[i];
            Vector3 rad = ufoRadii[i];
            Vector3 local = new Vector3(
                c.x + Mathf.Cos(t) * rad.x,
                c.y + Mathf.Sin(t * 0.8f) * rad.y,
                c.z + Mathf.Sin(t) * rad.z);
            Vector3 pos = basePos
                + player.right * local.x
                + Vector3.up * local.y
                + player.forward * local.z;
            ufos[i].position = pos;
            // Saucer spin + gentle banking toward travel direction.
            ufos[i].rotation = Quaternion.Euler(
                Mathf.Sin(t) * 12f,
                ufos[i].eulerAngles.y + ufoSpin[i] * Time.deltaTime,
                Mathf.Cos(t) * 12f);
        }
    }

    public string CurrentZoneName()
    {
        return (zones != null && lastLevel >= 0)
            ? zones[lastLevel % zones.Length].name : "";
    }
}

// Sits on each background UFO. Only fires during an active storm
// (WeatherManager.IsStorming) — a near-straight, multi-segment bolt that
// strikes down through the air near the track and stops short of the
// ground (purely atmospheric, no gameplay debris impact).
public class UFOLightningBolt : MonoBehaviour
{
    private LineRenderer lr;      // crisp core bolt
    private LineRenderer glowLr;  // wider, softer halo behind it
    private float timer;
    private const int Segments = 8;
    // Pure white "lightning rod" look — the previous blue tint plus
    // branching fork read as scattered background sparks rather than a
    // clean bolt. Strikes now also land near the actual track (see
    // FireBolt) instead of wherever the UFO's wide orbit happens to be.
    // Uses NebulaDash/UnlitNoFog instead of URP's stock Unlit — that
    // shader mixes in scene fog, which during a dense storm blended the
    // "white" bolt toward the dark fog color and read as black even with
    // the color HDR-boosted past 1. The no-fog shader always renders the
    // raw color regardless of storm fog density.
    private static readonly Color BoltColor = new Color(4f, 4f, 4f, 1f);
    private static readonly Color GlowColor = new Color(3f, 3f, 3f, 0.3f);

    void Start()
    {
        lr = MakeLine("BoltCore", 0.18f, 0.05f, BoltColor);
        glowLr = MakeLine("BoltGlow", 0.5f, 0.14f, GlowColor);
        timer = Random.Range(3f, 7f);
    }

    LineRenderer MakeLine(string name, float startWidth, float endWidth, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer l = go.AddComponent<LineRenderer>();
        l.positionCount = Segments;
        l.startWidth = startWidth;
        l.endWidth = endWidth;
        l.numCapVertices = 2;
        l.numCornerVertices = 2;
        l.useWorldSpace = true;
        l.material = new Material(Shader.Find("NebulaDash/UnlitNoFog"));
        l.material.SetColor("_BaseColor", color);
        l.enabled = false;
        return l;
    }

    void Update()
    {
        if (!WeatherManager.IsStorming)
        {
            if (lr.enabled) SetAllEnabled(false);
            timer = Random.Range(3f, 7f);
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            // Background strikes get more frequent as the run gets
            // harder — a full storm at max difficulty roughly triples
            // the strike rate of an early-run storm.
            float freqMult = Mathf.Lerp(1f, 0.35f, WeatherManager.DifficultyProgress);
            timer = Random.Range(3f, 7f) * freqMult;
            StartCoroutine(FireBolt());
        }
    }

    void SetAllEnabled(bool on)
    {
        lr.enabled = on;
        glowLr.enabled = on;
    }

    // Near-straight rod with just a slight organic waver, not a jagged
    // fractal zigzag — reads as a clean bolt rather than scattered
    // sparks.
    static Vector3[] BuildBoltPath(Vector3 top, Vector3 bottom, int segments, float jitter)
    {
        Vector3[] path = new Vector3[segments];
        for (int s = 0; s < segments; s++)
        {
            float f = s / (float)(segments - 1);
            Vector3 p = Vector3.Lerp(top, bottom, f);
            if (s > 0 && s < segments - 1)
                p += new Vector3(
                    Random.Range(-jitter, jitter), 0f,
                    Random.Range(-jitter, jitter));
            path[s] = p;
        }
        return path;
    }

    System.Collections.IEnumerator FireBolt()
    {
        Vector3 top = transform.position;
        // Strike straight down from wherever the UFO actually is, not
        // clamped toward a fixed narrow band near world X=0 — that clamp
        // could be tens of units away from the UFO's own (often wide)
        // horizontal position, producing a bolt that travelled mostly
        // sideways before ever going down instead of reading as vertical
        // lightning. Stops short of the ground instead of reaching it —
        // this is meant purely as an atmospheric sky strike, not
        // something that craters the track.
        Vector3 bottom = new Vector3(top.x, 4f, top.z);

        Vector3[] path = BuildBoltPath(top, bottom, Segments, 0.4f);
        for (int s = 0; s < Segments; s++)
        {
            lr.SetPosition(s, path[s]);
            glowLr.SetPosition(s, path[s]);
        }

        // Randomized flicker instead of a fixed on/off/on pattern, so
        // consecutive strikes don't all read identically.
        int flashes = Random.Range(2, 4);
        for (int i = 0; i < flashes; i++)
        {
            SetAllEnabled(true);
            yield return new WaitForSeconds(Random.Range(0.03f, 0.07f));
            SetAllEnabled(false);
            if (i < flashes - 1)
                yield return new WaitForSeconds(Random.Range(0.03f, 0.06f));
        }
    }
}
