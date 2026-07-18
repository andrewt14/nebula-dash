using UnityEngine;
using System.Collections;

public class MagnetEffect : MonoBehaviour
{
    public float magnetRadius = 65f;
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

        // Pull ALL nearby orbs toward player fast
        GameObject[] orbs =
            GameObject.FindGameObjectsWithTag("Orb");
        foreach (GameObject orb in orbs)
        {
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
                float catchUpSpeed = pc != null ? pc.runSpeed : 0f;
                float speed = catchUpSpeed + Mathf.Lerp(
                    10f, 30f, 1f - dist / magnetRadius);
                orb.transform.position = Vector3.MoveTowards(
                    orb.transform.position,
                    transform.position,
                    speed * Time.deltaTime);
            }
        }
    }

    public void Activate(float duration)
    {
        isActive = true;
        timer = duration;

        // Emoji glyph isn't in the TMP font atlas — renders as an empty
        // missing-glyph box right next to the text.
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowPopup(
                "MAGNET!", transform.position);
    }
}