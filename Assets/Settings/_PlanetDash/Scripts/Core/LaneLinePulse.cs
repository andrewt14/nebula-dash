using UnityEngine;

public class LaneLinePulse : MonoBehaviour
{
    public Renderer laneRenderer;
    public float minIntensity = 1f;
    public float maxIntensity = 5f;
    // Tinted by ZoneManager so the side lines shift color per zone.
    public Color baseColor = Color.white;
    private Renderer rend;

    // Every ground tile carries 2 of these (plus corner turn miters) — with
    // tilesAhead=150 that's 300+ live instances at once. GetComponent<Renderer>()
    // .material (as opposed to .sharedMaterial) auto-instantiates a unique
    // Material copy per renderer the first time it's touched, which meant
    // every single lane-line segment drew with its own material instance —
    // no two could ever batch, regardless of GPU instancing/SRP batcher
    // support, since batching requires a genuinely shared material. A
    // MaterialPropertyBlock carries the same per-instance emission color
    // without cloning the material at all.
    private static MaterialPropertyBlock sharedBlock;
    private static readonly int EmissionColorID =
        Shader.PropertyToID("_EmissionColor");

    // Resolved once and reused by every instance instead of each of the
    // 300+ lane-line segments running its own FindObjectOfType scan every
    // time a tile spawns.
    private static PlayerController cachedPc;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (sharedBlock == null) sharedBlock = new MaterialPropertyBlock();
        if (cachedPc == null) cachedPc = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        if (cachedPc == null || rend == null) return;

        float speedPercent = Mathf.InverseLerp(
            12f, 60f, cachedPc.runSpeed);
        float intensity = Mathf.Lerp(
            minIntensity, maxIntensity, speedPercent);

        float pulse = Mathf.Sin(
            Time.time * (2f + speedPercent * 4f))
            * 0.3f + 0.7f;

        rend.GetPropertyBlock(sharedBlock);
        sharedBlock.SetColor(EmissionColorID,
            baseColor * intensity * pulse);
        rend.SetPropertyBlock(sharedBlock);
    }
}