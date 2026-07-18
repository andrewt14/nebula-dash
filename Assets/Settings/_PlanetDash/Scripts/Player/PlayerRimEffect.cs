using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Temporarily tints the player's suit rim glow to match whichever
// power-up orb was just collected (gold 2x-score, red magnet, navy
// invincibility), for exactly as long as that orb's own effect lasts —
// an always-visible "which buff is active" readout without a separate
// HUD icon. Self-attaches to the Player GameObject on first use instead
// of needing scene wiring, same pattern as MagnetEffect/DifficultyManager
// being found via FindObjectOfType elsewhere in this codebase.
public class PlayerRimEffect : MonoBehaviour
{
    private static PlayerRimEffect instance;
    private static readonly Color BaseColor = Color.white;

    public static void Flash(Color color, float duration)
    {
        PlayerRimEffect e = Get();
        if (e != null) e.SetRimColor(color, duration);
    }

    static PlayerRimEffect Get()
    {
        if (instance != null) return instance;
        GameObject player = GameObject.Find("Player");
        if (player == null) return null;
        instance = player.GetComponent<PlayerRimEffect>();
        if (instance == null) instance = player.AddComponent<PlayerRimEffect>();
        return instance;
    }

    private Renderer[] rimRenderers;
    private Coroutine activeCoroutine;

    // Cached lazily (not in Awake) — PlayerCharacterLoader can swap in a
    // different character model after this component first exists, so
    // scanning only once up front could miss the actual active renderers.
    void CacheRenderers()
    {
        List<Renderer> found = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material m in r.materials)
            {
                if (m.HasProperty("_RimColor")) { found.Add(r); break; }
            }
        }
        rimRenderers = found.ToArray();
    }

    void SetRimColor(Color color, float duration)
    {
        CacheRenderers();
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(RimColorCoroutine(color, duration));
    }

    IEnumerator RimColorCoroutine(Color color, float duration)
    {
        ApplyColor(color);
        yield return new WaitForSeconds(duration);
        ApplyColor(BaseColor);
        activeCoroutine = null;
    }

    void ApplyColor(Color color)
    {
        foreach (Renderer r in rimRenderers)
            foreach (Material m in r.materials)
                if (m.HasProperty("_RimColor"))
                    m.SetColor("_RimColor", color);
    }
}
