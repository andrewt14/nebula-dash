using UnityEngine;

// Proximity guards used by hazard spawners and the one still-mobile
// hazard (Boulder, which rolls toward the player under its own power).
// Every other hazard is fixed-position, so BlockedNear (a spawn-time
// check) is enough for them — only the boulder can still catch up to
// something ahead of it mid-run, hence BlockedAhead.
//
// Both take an explicit `pathForward` (the player's current
// transform.forward) instead of assuming world +Z, so they stay correct
// after a 90-degree turn changes which world axis is "ahead"/"lane".
public static class HazardSpacing
{
    // Reactive check for a mover (currently just Boulder): is something
    // of type T just ahead of it in the same lane, close enough that it
    // should yield (shatter/despawn) instead of rolling through it?
    // nearMargin extends the check slightly BEHIND the mover's own center
    // (dz down to -nearMargin) so an obstacle the mover is already level
    // with or just overlapping still counts as blocking — without it the
    // strict dz > 0 test let a hazard sitting exactly at the boulder's own
    // z (or a hair behind it) slip through the window and the boulder rolled
    // visibly through it instead of shattering. Set it to roughly the
    // mover's + hazard's combined radius.
    public static bool BlockedAhead<T>(
        Transform self, Vector3 pathForward,
        float laneTolerance = 1.2f, float zAheadMax = 3.2f,
        float nearMargin = 0f)
        where T : Component
    {
        Vector3 right = Vector3.Cross(Vector3.up, pathForward).normalized;
        Vector3 myPos = self.position;
        float selfFwd = Vector3.Dot(myPos, pathForward);
        float selfRight = Vector3.Dot(myPos, right);

        foreach (T t in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            Transform other = t.transform;
            if (other == self) continue;

            float dz = selfFwd - Vector3.Dot(other.position, pathForward); // >0 means other is ahead
            float dRight = Mathf.Abs(Vector3.Dot(other.position, right) - selfRight);
            if (dRight < laneTolerance && dz > -nearMargin && dz < zAheadMax)
                return true;
        }
        return false;
    }

    // Spawn-time guard: is anything of type T already sitting near this
    // world position?
    public static bool BlockedNear<T>(
        Vector3 pos, Vector3 pathForward,
        float laneTolerance = 1.2f, float zTolerance = 3f)
        where T : Component
    {
        Vector3 right = Vector3.Cross(Vector3.up, pathForward).normalized;
        float posFwd = Vector3.Dot(pos, pathForward);
        float posRight = Vector3.Dot(pos, right);

        foreach (T t in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            Vector3 p = t.transform.position;
            float dRight = Mathf.Abs(Vector3.Dot(p, right) - posRight);
            float dFwd = Mathf.Abs(Vector3.Dot(p, pathForward) - posFwd);
            if (dRight < laneTolerance && dFwd < zTolerance)
                return true;
        }
        return false;
    }
}
