using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MagnetEffect : MonoBehaviour
{
    // Big, generous pull radius — several lanes and well ahead of the
    // player, so orbs anywhere near the corridor get swept in for the full
    // effect duration.
    // Quadrupled (140 -> 560). Orbs are spawned 105+ units ahead, so the
    // old radius only ever reached about one spawn batch's worth of track.
    public float magnetRadius = 560f;
    public bool isActive = false;
    private float timer = 0f;
    private PlayerController pc;

    // No MagnetEffect component actually exists anywhere in the gameplay
    // scene — every caller looked it up with FindObjectOfType, which
    // silently returns null when nothing is placed, so the magnet pull
    // (and the invincibility orb's magnet freebie) never fired at all.
    // Self-attach to the Player on first use instead of requiring scene
    // wiring, same pattern as PlayerRimEffect.
    private static MagnetEffect instance;

    public static MagnetEffect Get()
    {
        if (instance != null) return instance;
        GameObject player = GameObject.Find("Player");
        if (player == null) return null;
        instance = player.GetComponent<MagnetEffect>();
        if (instance == null) instance = player.AddComponent<MagnetEffect>();
        return instance;
    }

    void Start()
    {
        pc = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (!isActive) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            isActive = false;
            return;
        }

        // Pull ALL nearby orbs toward the player fast. Iterates the
        // authoritative live-orb list (ResourceOrb.Active) instead of a
        // per-frame FindGameObjectsWithTag scan — the tag lookup allocated a
        // fresh array every frame for the full 10s window (a real mobile GC
        // cost) and silently missed any orb whose tag wasn't exactly "Orb".
        List<ResourceOrb> orbs = ResourceOrb.Active;
        float catchUpSpeed = pc != null ? pc.runSpeed : 0f;
        for (int i = 0; i < orbs.Count; i++)
        {
            ResourceOrb orb = orbs[i];
            if (orb == null) continue;
            float dist = Vector3.Distance(
                transform.position, orb.transform.position);
            if (dist < magnetRadius)
            {
                // Pull faster as they get closer. Also has to outrun the
                // player's OWN forward speed (up to 240 at max difficulty)
                // or a pulled orb can never actually catch up — it just
                // trails behind forever and gets cleaned up uncollected,
                // regardless of which lane it's in.
                // Bonus range widened alongside the 4x radius: at the old
                // +10..+30 an orb near the edge of the radius closed on the
                // player at 10 units/sec, so it could never arrive inside
                // the 10s window and the effect looked like it had already
                // ended while the timer was still running.
                float speed = catchUpSpeed + Mathf.Lerp(
                    40f, 90f, 1f - dist / magnetRadius);
                orb.PullToward(transform.position, speed * Time.deltaTime);
            }
        }
    }

    public void Activate(float duration)
    {
        isActive = true;
        // Never let a shorter activation truncate a longer one still
        // running — the invincibility orb grants a 5s magnet as a freebie
        // (GameManager.ActivateInvincibility), which used to cut a live
        // 10s magnet orb window down to 5s.
        timer = Mathf.Max(timer, duration);

        // Emoji glyph isn't in the TMP font atlas — renders as an empty
        // missing-glyph box right next to the text.
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowPopup(
                "MAGNET!", transform.position);
    }
}