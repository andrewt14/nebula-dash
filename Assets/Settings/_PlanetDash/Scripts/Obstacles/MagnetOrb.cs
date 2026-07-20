using UnityEngine;
using System.Collections.Generic;

public class MagnetOrb : MonoBehaviour
{
    private float bobSpeed = 3f;
    private float bobHeight = 0.3f;
    private Vector3 startPos;
    private Transform player;
    // Horizontal-only reach (see the xz-only check below), so this no
    // longer needs to eat into vertical gap the way a 3D radius did. 18
    // meant the orb got sucked up from clear across the corridor without
    // the player ever actually reaching it. Same-lane pass is 0 lateral
    // offset, adjacent lane is one laneWidth (2.5) — 3 comfortably covers
    // both plus a frame's travel margin, without also grabbing it from a
    // lane the player never touched.
    private float collectRadius = 3f;
    private bool collected = false;
    private Light orbLight;

    // How far the instant burst-collect reaches on pickup — separate from
    // (and much bigger than) MagnetEffect's ongoing pull radius, since
    // this is a one-shot "sweep everything nearby" rather than a gradual
    // pull.
    private const float BurstCollectRadius = 60f;

    private static readonly Color MagnetColor = new Color(1f, 0.05f, 0.25f);

    void Start()
    {
        startPos = transform.position;
        player = GameObject.Find("Player").transform;

        // Tint red at runtime rather than editing the shared GoldMaterial
        // asset (also used by other orbs) or making a whole new material.
        // Metallic + high smoothness for a polished-metal look, with a
        // much stronger emission so it visibly glows rather than just
        // being a flat red ball.
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", MagnetColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", MagnetColor);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.95f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", MagnetColor * 30f);
            }
        }

        GameObject lightObj = new GameObject("MagnetLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        orbLight = lightObj.AddComponent<Light>();
        orbLight.color = MagnetColor;
        orbLight.intensity = 16f;
        orbLight.range = 14f;
    }

    void Update()
    {
        if (collected) return;

        transform.position = startPos +
            transform.up * Mathf.Sin(
                Time.time * bobSpeed) * bobHeight;

        transform.Rotate(Vector3.up * 90f * Time.deltaTime);

        if (orbLight != null)
            orbLight.intensity = 14f +
                Mathf.Sin(Time.time * 4f) * 3f;

        if (player != null)
        {
            // Horizontal (xz) distance only — the orb bobs at y~2.8 while the
            // player runs at y~1, so a 3D distance check spent most of the
            // radius on unreachable vertical gap and the player ran under the
            // orb without ever collecting it. This is what read as "the
            // magnet doesn't work."
            Vector3 d = transform.position - player.position;
            float dist = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            if (dist < collectRadius)
            {
                collected = true;

                // Instant sweep: collect every resource orb already within
                // a big radius right now, instead of relying purely on a
                // gradual pull that a player could easily outrun (and
                // that depended on tag lookups working correctly).
                foreach (ResourceOrb orb in new List<ResourceOrb>(ResourceOrb.Active))
                    if (orb != null &&
                        Vector3.Distance(orb.transform.position, player.position) < BurstCollectRadius)
                        orb.Collect();

                // Keep an ongoing pull too, for orbs that spawn just
                // ahead during the ride.
                MagnetEffect magnet = MagnetEffect.Get();
                // MagnetEffect.Activate shows its own "MAGNET!" popup —
                // showing a second one here from the same pickup stacked
                // two overlapping popups on top of each other, and the
                // other one's magnet-emoji glyph isn't in the TMP font
                // atlas, rendering as a missing-glyph box right next to
                // the text.
                if (magnet != null)
                    magnet.Activate(10f);
                PlayerRimEffect.Flash(MagnetColor, 10f);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayCollect();
                Destroy(gameObject);
            }
        }
    }
}