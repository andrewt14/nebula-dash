using UnityEngine;
using System.Collections;

public class MagnetEffect : MonoBehaviour
{
    public float magnetRadius = 25f;
    public bool isActive = false;
    private float timer = 0f;
    private PlayerController pc;

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
                // Pull faster as they get closer
                float speed = Mathf.Lerp(
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

        if (ScorePopup.Instance != null)
            ScorePopup.Instance.ShowPopup(
                "🧲 MAGNET!", transform.position);
    }
}