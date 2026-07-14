using UnityEngine;
using UnityEngine.Events;


public class LavaRise : MonoBehaviour
{
    [Header("Lava Settings")]
    public float baseRiseSpeed = 0.3f;
    public float accelerationRate = 0.02f;
    public float currentRiseSpeed;
    public float lavaRadius;  // Distance from planet center


    [Header("References")]
    public Transform planetCenter;
    public Transform playerTransform;
    public UnityEvent onPlayerKilled;


    [Header("Visual")]
    public Material lavaMaterial;
    public float glowIntensity = 2.5f;


    private float runTime = 0f;
    private bool playerDead = false;


    void Start()
    {
        currentRiseSpeed = baseRiseSpeed;
        lavaRadius = Vector3.Distance(planetCenter.position,
                     transform.position);
    }


    void Update()
    {
        if (playerDead) return;


        runTime += Time.deltaTime;
        currentRiseSpeed = baseRiseSpeed + (accelerationRate * runTime);


        // Move lava sphere outward from planet center
        lavaRadius += currentRiseSpeed * Time.deltaTime;
        transform.localScale = Vector3.one * (lavaRadius * 2f);


        // Animate lava glow with time
        if (lavaMaterial != null)
            lavaMaterial.SetFloat("_EmissionIntensity",
                glowIntensity + Mathf.Sin(runTime * 3f) * 0.5f);


        CheckPlayerCollision();
    }


    void CheckPlayerCollision()
    {
        float playerDist = Vector3.Distance(planetCenter.position,
                           playerTransform.position);
        if (playerDist <= lavaRadius + 0.5f)
        {
            playerDead = true;
            onPlayerKilled?.Invoke();
        }
    }
}
