using UnityEngine;

public class LaneLinePulse : MonoBehaviour
{
    public Renderer laneRenderer;
    public float minIntensity = 1f;
    public float maxIntensity = 5f;
    // Tinted by ZoneManager so the side lines shift color per zone.
    public Color baseColor = Color.white;
    private Material mat;
    private PlayerController pc;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
        pc = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        if (pc == null || mat == null) return;

        float speedPercent = Mathf.InverseLerp(
            12f, 60f, pc.runSpeed);
        float intensity = Mathf.Lerp(
            minIntensity, maxIntensity, speedPercent);

        float pulse = Mathf.Sin(
            Time.time * (2f + speedPercent * 4f)) 
            * 0.3f + 0.7f;

        mat.SetColor("_EmissionColor",
            baseColor * intensity * pulse);
    }
}