using UnityEngine;

public class MagnetOrb : MonoBehaviour
{
    private float bobSpeed = 3f;
    private float bobHeight = 0.3f;
    private Vector3 startPos;
    private Transform player;
    private float collectRadius = 2f; // tight radius
    private bool collected = false;
    private Light orbLight;

    void Start()
    {
        startPos = transform.position;
        player = GameObject.Find("Player").transform;

        GameObject lightObj = new GameObject("MagnetLight");
        lightObj.transform.parent = transform;
        lightObj.transform.localPosition = Vector3.zero;
        orbLight = lightObj.AddComponent<Light>();
        orbLight.color = new Color(0f, 1f, 1f);
        orbLight.intensity = 4f;
        orbLight.range = 6f;
    }

    void Update()
    {
        if (collected) return;

        transform.position = startPos +
            transform.up * Mathf.Sin(
                Time.time * bobSpeed) * bobHeight;

        transform.Rotate(Vector3.up * 90f * Time.deltaTime);

        if (orbLight != null)
            orbLight.intensity = 3f +
                Mathf.Sin(Time.time * 4f) * 1f;

        if (player != null)
        {
            float dist = Vector3.Distance(
                transform.position, player.position);
            if (dist < collectRadius)
            {
                collected = true;
                MagnetEffect magnet =
                    FindObjectOfType<MagnetEffect>();
                if (magnet != null)
                    magnet.Activate(10f);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayCollect();
                if (ScorePopup.Instance != null)
                    ScorePopup.Instance.ShowPopup(
                        "MAGNET!", transform.position);
                Destroy(gameObject);
            }
        }
    }
}