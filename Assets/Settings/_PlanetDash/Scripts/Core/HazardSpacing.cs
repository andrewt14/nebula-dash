using UnityEngine;

// Shared proximity check for self-propelling hazards (Boulder, AlienObstacle)
// that move toward the player at their own speed and can otherwise catch up
// to and clip through a stationary or differently-paced hazard ahead of them
// in the same lane. Spawn-time cooldowns only space out spawn moments, not
// where things end up once they're moving at different speeds — this closes
// that gap by having movers yield (despawn) instead of overlapping.
public static class HazardSpacing
{
    // zAheadMax was 1.8 — at a mover's top speed (~20u/s) that only
    // leaves a fraction of a frame's travel before the check catches up,
    // so on any frame-time hiccup the mover was already visually
    // overlapping the wall/obstacle by the time it stopped. Widened so
    // the stop always lands with a clean visible gap.
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
    // world position? Reactive BlockedAhead only stops a mover from
    // catching up to something — it can't help two hazards that get
    // placed almost on top of each other at the moment they spawn.
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
