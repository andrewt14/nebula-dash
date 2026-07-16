using UnityEngine;

// Spawn-time proximity guard shared by every hazard spawner. All hazards
// are fixed-position (nothing self-propels toward the player anymore —
// only the player's own forward speed closes distance), so overlap can
// only happen at the moment something spawns, not by one hazard catching
// up to another mid-run. This is the sole guard needed for that.
public static class HazardSpacing
{
    // Spawn-time guard: is anything of type T already sitting near this
    // world position?
    public static bool BlockedNear<T>(
        Vector3 pos, float laneTolerance = 1.2f, float zTolerance = 3f)
        where T : Component
    {
        foreach (T t in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            Vector3 p = t.transform.position;
            if (Mathf.Abs(p.x - pos.x) < laneTolerance &&
                Mathf.Abs(p.z - pos.z) < zTolerance)
                return true;
        }
        return false;
    }
}
