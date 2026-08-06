using UnityEngine;

// Warms the object pool for every hazard prefab during the boot splash's
// still-opaque window (see SceneTransitionOverlay), so the run's first
// boulder/wall/UFO/etc. of each type doesn't pay its one-time
// Instantiate+Awake cost (building a custom-shader glow material and a
// Light — and on device, possibly a first-use shader-variant compile)
// live, mid-run, as a frame hitch. Runs in Awake() — guaranteed to finish
// before any Start()-driven coroutine, including the overlay's own fade,
// gets its first frame.
[DefaultExecutionOrder(-900)]
public class AssetPrewarmer : MonoBehaviour
{
    void Awake()
    {
        ObjectSpawner spawner = GetComponent<ObjectSpawner>();
        if (spawner != null)
        {
            ObjectPool.Instance.Prewarm(spawner.boulderPrefab);
            ObjectPool.Instance.Prewarm(spawner.alienWallPrefab);
            ObjectPool.Instance.Prewarm(spawner.ufoPrefab);
            ObjectPool.Instance.Prewarm(spawner.strafingPrefab);
            ObjectPool.Instance.Prewarm(spawner.alienRunnerPrefab);
            ObjectPool.Instance.Prewarm(spawner.lavaCrackPrefab);
        }

        MeteoriteSpawner meteoriteSpawner = GetComponent<MeteoriteSpawner>();
        if (meteoriteSpawner != null)
            ObjectPool.Instance.Prewarm(meteoriteSpawner.meteoritePrefab);
    }
}
