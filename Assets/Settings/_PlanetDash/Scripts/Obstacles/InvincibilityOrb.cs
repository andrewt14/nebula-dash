using UnityEngine;
using System.Collections;

public class InvincibilityOrb : MonoBehaviour
{
    private static readonly Color NavyColor = new Color(0.05f, 0.1f, 0.55f);

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
            r.material.EnableKeyword("_EMISSION");
            r.material.SetColor("_EmissionColor", NavyColor * 3f);
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
