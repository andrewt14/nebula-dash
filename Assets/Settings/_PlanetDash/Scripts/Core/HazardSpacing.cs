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

    // Half-extent of an AABB projected onto an arbitrary direction (the
    // standard box/support-function projection) — not just a raw world
    // axis, since a hazard's true lateral "width" relative to the player's
    // CURRENT heading depends on which world axis is forward/right after a
    // 90-degree turn.
    static float ExtentAlong(Bounds b, Vector3 dir)
    {
        return b.extents.x * Mathf.Abs(dir.x)
             + b.extents.y * Mathf.Abs(dir.y)
             + b.extents.z * Mathf.Abs(dir.z);
    }

    // Real rendered half-extents of a hazard along the path's right/forward
    // axes. Works on either a live scene instance or an un-instantiated
    // prefab asset — Renderer.bounds is valid on both.
    static void HalfExtents(
        GameObject obj, Vector3 right, Vector3 forward,
        out float half, out float depth)
    {
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) { half = 0.6f; depth = 0.6f; return; }
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        half = ExtentAlong(b, right);
        depth = ExtentAlong(b, forward);
    }

    // Geometry-aware spawn-time guard: blocked when the CANDIDATE's own
    // real footprint (from candidatePrefab) would actually overlap an
    // existing T's real footprint, instead of a single flat tolerance
    // shared by every obstacle type regardless of size. A flat ~1.2 lane
    // tolerance assumes narrow objects; AlienWall alone is 3 units wide —
    // wider than the 2.5-unit lane gap — so two of them picked for
    // adjacent lanes by a formation could pass the old same-lane-only
    // check and still visibly overlap. Real bounds vs. real bounds can't
    // make that mistake for any current or future hazard size.
    public static bool BlockedNear<T>(
        Vector3 pos, Vector3 pathForward, GameObject candidatePrefab,
        float margin = 0.3f)
        where T : Component
    {
        Vector3 right = Vector3.Cross(Vector3.up, pathForward).normalized;
        float posFwd = Vector3.Dot(pos, pathForward);
        float posRight = Vector3.Dot(pos, right);

        HalfExtents(candidatePrefab, right, pathForward,
            out float candHalf, out float candDepth);

        foreach (T t in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            Vector3 p = t.transform.position;
            float dRight = Mathf.Abs(Vector3.Dot(p, right) - posRight);
            float dFwd = Mathf.Abs(Vector3.Dot(p, pathForward) - posFwd);

            HalfExtents(t.gameObject, right, pathForward,
                out float otherHalf, out float otherDepth);

            if (dRight < candHalf + otherHalf + margin &&
                dFwd < candDepth + otherDepth + margin)
                return true;
        }
        return false;
    }
}
