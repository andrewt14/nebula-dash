using UnityEngine;
using System.Collections;

// Rare pickup for the jetpack/hoverboard flight (see JetpackEffect) — same
// bob/spin/glow treatment as the other rare orbs (InvincibilityOrb), just
// its own color so it reads as a distinct power-up at a glance.
public class JetpackOrb : MonoBehaviour
{
    // Punchier electric cyan — the old tone read pale/washed-out next to
    // the other pickups' stronger glows.
    private static readonly Color JetColor = new Color(0.1f, 0.85f, 1f);

    public float flightDuration = 7f;

    private float bobSpeed = 3f;
    private float bobHeight = 0.3f;
    private Vector3 startPos;
    private Vector3 baseScale;
    private Transform player;
    private float collectRadius = 2.5f;
    private bool collected = false;
    private Light orbLight;

    void Start()
    {
        startPos = transform.position;
        baseScale = transform.localScale;
        player = GameObject.Find("Player").transform;

        GameObject lightObj = new GameObject("JetLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        orbLight = lightObj.AddComponent<Light>();
        orbLight.color = JetColor;
        orbLight.intensity = 10f;
        orbLight.range = 10f;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material m = r.material;
            // Base color too, not just emission — this prefab is cloned
            // from an existing orb sharing its material, so leaving
            // _BaseColor untouched would leave it rendering as that
            // orb's own color with a barely-visible tint.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", JetColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", JetColor);
            m.EnableKeyword("_EMISSION");
            // Bloom threshold is 1.0 (SampleSceneProfile.asset) — bumped
            // from x4 (pale) but capped around x2.5 so it stays a bright
            // cyan glow instead of clipping to solid white.
            m.SetColor("_EmissionColor", JetColor * 2.5f);
        }
    }

    void Update()
    {
        if (collected) return;

        transform.position = startPos +
            transform.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;

        transform.Rotate(Vector3.up * 200f * Time.deltaTime);
        transform.Rotate(Vector3.forward * 100f * Time.deltaTime);

        if (orbLight != null)
            orbLight.intensity = 9f + Mathf.Sin(Time.time * 6f) * 2f;
        transform.localScale = baseScale *
            (1f + Mathf.Sin(Time.time * 4f) * 0.08f);

        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist < collectRadius)
            {
                collected = true;
                StartCoroutine(CollectJetpackOrb());
            }
        }
    }

    IEnumerator CollectJetpackOrb()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (orbLight != null) orbLight.enabled = false;

        // GameManager's own invincibility coroutine (reused by
        // JetpackEffect for the real safety guarantee + its existing
        // countdown text) already shows a generic "INVINCIBLE!" banner —
        // this popup is the one cue that specifically says "jetpack" at
        // the moment of pickup.
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowPopup(
                "JETPACK!", transform.position, 1f, JetColor);
        if (JetpackEffect.Instance != null)
            JetpackEffect.Instance.Activate(flightDuration);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayCollect();

        Destroy(gameObject, 0.5f);
        yield return null;
    }
}
