using UnityEngine;

// Proximity guards used by hazard spawners and the one still-mobile
// hazard (Boulder, which rolls toward the player under its own power).
// Every other hazard is fixed-position, so BlockedNear (a spawn-time
// check) is enough for them — only the boulder can still catch up to
// something ahead of it mid-run, hence BlockedAhead.
public static class HazardSpacing
{
    // Reactive check for a mover (currently just Boulder): is something
    // of type T just ahead of it in the same lane, close enough that it
    // should yield (shatter/despawn) instead of rolling through it?
    public static bool BlockedAhead<T>(
        Transform self, float laneTolerance = 1.2f, float zAheadMax = 3.2f)
        where T : Component
    {
        Vector3 myPos = self.position;
        foreach (T t in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            Transform other = t.transform;
            if (other == self) continue;

            float dz = myPos.z - other.position.z; // >0 means other is ahead
            if (Mathf.Abs(other.position.x - myPos.x) < laneTolerance &&
                dz > 0f && dz < zAheadMax)
                return true;
        }
        return false;
    }

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
