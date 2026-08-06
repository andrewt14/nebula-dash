using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    private static ObjectPool _instance;
    public static ObjectPool Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ObjectPool");
                _instance = go.AddComponent<ObjectPool>();
            }
            return _instance;
        }
    }

    // Some obstacle prefabs have their gameplay script on a child object
    // rather than the root (e.g. Comet's "Meteorite" script lives on a
    // child named "default"). Tagging the root with this lets Return()
    // resolve back to the correct pooled root no matter which object in
    // the hierarchy calls it.
    private class PooledInstance : MonoBehaviour
    {
        public GameObject Prefab;
    }

    private readonly Dictionary<GameObject, Queue<GameObject>> pools =
        new Dictionary<GameObject, Queue<GameObject>>();

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!pools.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            pools[prefab] = queue;
        }

        GameObject instance = queue.Count > 0 ? queue.Dequeue() : null;

        if (instance == null)
        {
            instance = Instantiate(prefab);
            instance.AddComponent<PooledInstance>().Prefab = prefab;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    // Forces the (expensive, one-time) first Instantiate+Awake for a
    // pooled prefab to happen right now instead of whenever it first
    // spawns for real during a run. Hazard prefabs build a custom-shader
    // glow material and a Light in Awake() — the first time any given
    // shader/keyword combination is used it can also trigger a real
    // shader-variant compile on device, so without this the run's FIRST
    // boulder/wall/UFO/etc. of each type risked a frame hitch exactly
    // when it appeared. Get()+Return() in the same call so nothing is
    // ever actually visible — see AssetPrewarmer, which calls this
    // during the boot splash's still-opaque window.
    public void Prewarm(GameObject prefab)
    {
        if (prefab == null) return;
        GameObject instance = Get(prefab, new Vector3(0f, -500f, 0f), Quaternion.identity);
        Return(instance);
    }

    public void Return(GameObject caller)
    {
        if (caller == null) return;

        PooledInstance tracked = caller.GetComponentInParent<PooledInstance>(true);
        if (tracked == null)
        {
            Destroy(caller);
            return;
        }

        GameObject root = tracked.gameObject;
        root.SetActive(false);

        // Defensive: an instance can outlive its pool entry (e.g. domain
        // reload creating a fresh ObjectPool with an empty dictionary),
        // which previously threw KeyNotFoundException here. Recreate the
        // queue on demand instead of crashing.
        if (!pools.TryGetValue(tracked.Prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            pools[tracked.Prefab] = queue;
        }
        queue.Enqueue(root);
    }
}
