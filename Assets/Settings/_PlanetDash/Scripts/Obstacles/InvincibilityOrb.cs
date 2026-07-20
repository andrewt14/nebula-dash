using UnityEngine;
using System.Collections;

public class InvincibilityOrb : MonoBehaviour
{
    // Was navy (0.05, 0.1, 0.55) — too dark to read as "neon" against a
    // space background, and it never mattered anyway since only emission
    // was being set (see Start below): the prefab is a straight clone of
    // GoldOrb sharing its material, so the orb rendered as plain gold.
    private static readonly Color NavyColor = new Color(0.1f, 0.55f, 1f);

    public float invincibleDuration = 5f;

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

        GameObject lightObj = new GameObject("NavyLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        orbLight = lightObj.AddComponent<Light>();
        orbLight.color = NavyColor;
        orbLight.intensity = 8f;
        orbLight.range = 8f;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material m = r.material;
            // Base color too, not just emission — this prefab shares its
            // material with GoldOrb, so leaving _BaseColor untouched made
            // it render as an ordinary gold orb with a barely-visible tint.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", NavyColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", NavyColor);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", NavyColor * 4f);
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
            orbLight.intensity = 6f + Mathf.Sin(Time.time * 6f) * 2f;
        transform.localScale = baseScale *
            (1f + Mathf.Sin(Time.time * 4f) * 0.08f);

        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist < collectRadius)
            {
                collected = true;
                StartCoroutine(CollectInvincibilityOrb());
            }
        }
    }

    IEnumerator CollectInvincibilityOrb()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (orbLight != null) orbLight.enabled = false;

        // GameManager.ActivateInvincibility already shows its own
        // "INVINCIBLE!" popup — this orb's whole job is that effect.
        if (GameManager.Instance != null)
            GameManager.Instance.ActivateInvincibility(invincibleDuration);
        PlayerRimEffect.Flash(NavyColor, invincibleDuration);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayCollect();

        Destroy(gameObject, 0.5f);
        yield return null;
    }
}
