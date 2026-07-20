using UnityEngine;
using System.Collections;

public class ResourceOrb : MonoBehaviour
{
    // Lets ObjectSpawner's cleanup sweep iterate live orbs directly
    // instead of scanning every GameObject in the scene every frame.
    public static readonly System.Collections.Generic.List<ResourceOrb> Active =
        new System.Collections.Generic.List<ResourceOrb>();

    public int resourceValue = 1;
    private float bobSpeed = 4f;
    private float bobHeight = 0.4f;
    private Vector3 startPos;
    private Transform player;
    private float collectRadius = 2.8f;
    private bool collected = false;
    private Light orbLight;
    private Material mat;
    // Sky color only shifts during zone transitions; skip the per-frame
    // SetColor churn (two SetColor calls per orb per frame) while it's steady.
    private Color lastSkyColor = new Color(-1f, -1f, -1f);

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    void Start()
    {
        startPos = transform.position;
        player = GameObject.Find("Player").transform;
        GameObject lightObj = new GameObject("OrbLight");
lightObj.transform.parent = transform;
lightObj.transform.localPosition = Vector3.zero;
orbLight = lightObj.AddComponent<Light>();
orbLight.color = ZoneManager.CurrentSkyColor;
orbLight.intensity = 3f;
orbLight.range = 4f;

        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            mat = r.material;
            mat.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        if (collected) return;

        // Tint with the current zone's sky color so orbs shift alongside
        // the background instead of staying a fixed purple. Only re-apply
        // when the color actually changes (zone transitions), not every frame.
        Color skyColor = ZoneManager.CurrentSkyColor;
        if (skyColor != lastSkyColor)
        {
            lastSkyColor = skyColor;
            if (orbLight != null)
                orbLight.color = skyColor;
            if (mat != null)
            {
                mat.SetColor("_BaseColor", skyColor);
                mat.SetColor("_EmissionColor", skyColor * 3f);
            }
        }

        transform.position = startPos +
            transform.up * Mathf.Sin(
                Time.time * bobSpeed) * bobHeight;

        if (player != null)
        {
            float dist = Vector3.Distance(
                transform.position, player.position);
            if (dist < collectRadius)
                Collect();
        }
    }

    // MagnetEffect used to write straight to transform.position, but
    // Update() above unconditionally re-derives position from startPos +
    // bob every frame — whichever ran second each frame won, and even
    // then the next frame's Update stomped it right back to startPos.
    // Net result: a magnet-pulled orb never accumulated any real progress
    // toward the player, it just jittered in place at spawn. Moving
    // startPos itself is the single source of truth bob reads from, so a
    // pull actually sticks.
    public void PullToward(Vector3 target, float maxDelta)
    {
        startPos = Vector3.MoveTowards(startPos, target, maxDelta);
    }

    // Exposed so MagnetOrb can instantly sweep up every nearby orb on
    // pickup, not just ones the player happens to run within collectRadius
    // of.
    public void Collect()
    {
        if (collected) return;
        collected = true;
        StartCoroutine(CollectAnimation());
    }

    IEnumerator CollectAnimation()
    {
        if (SpeedBoost.Instance != null)
    SpeedBoost.Instance.ActivateBoost(3f);

        if (AudioManager.Instance != null)
    AudioManager.Instance.PlayCollect();

        // Add score and show popup ONCE at start
        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.score += 5f;
            DifficultyManager.Instance.PulseScore();
        }

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowPopup(
                "+5", transform.position);

        PlanetEvolution planet =
            FindObjectOfType<PlanetEvolution>();
        if (planet != null)
            planet.AddResource(resourceValue);

        float timer = 0f;
        float duration = 0.08f;
        Vector3 startScale = transform.localScale;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            float scale = Mathf.Lerp(1f, 2f, t);
            transform.localScale = startScale * scale;

            transform.position += Vector3.up *
                                  3f * Time.deltaTime;

            if (t > 0.7f)
            {
                float fadeT = (t - 0.7f) / 0.3f;
                transform.localScale = startScale *
                    Mathf.Lerp(2f, 0f, fadeT);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}