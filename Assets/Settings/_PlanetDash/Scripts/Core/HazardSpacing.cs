using System.Collections.Generic;
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
//
// Both are also geometry-aware: "blocked" means two hazards' REAL
// rendered footprints actually overlap, never a flat guessed tolerance.
// A flat tolerance has to be re-derived by hand every time a lane width
// or a prefab scale changes, and silently goes wrong when it isn't —
// which is exactly what left the boulder rolling through the edge of an
// adjacent-lane alien wall (boulder half-width 0.88 + wall half-width
// 1.50 = 2.38 against a 2.5-unit lane gap, versus the flat 2.0 tolerance
// that was written for a 4-unit lane spacing the scene no longer uses).
public static class HazardSpacing
{
    // FindObjectsByType allocates a fresh array on every call. Boulder
    // polls six hazard types EVERY FRAME, so on a screen with a few
    // boulders that was dozens of full-scene type scans per frame — the
    // single biggest source of per-frame garbage in the run loop. Every
    // caller within one frame sees the same set of hazards anyway, so one
    // scan per type per frame is all that can ever be needed.
    // Bumped whenever anything spawns or despawns. A frame counter ALONE is
    // not a safe cache key here: several hazards are created part-way
    // through a frame (ObjectSpawner places orbs at the top of Update and
    // every hazard type below them), so a scan taken earlier in the same
    // frame would not contain them — and a spawner that cannot see what was
    // just placed is precisely the one-directional blind spot that let orbs
    // end up inside obstacles. Caching within a frame is only valid while
    // the set of live objects has not changed.
    static int version;

    public static void Invalidate()
    {
        version++;
    }

    static class Scan<T> where T : Component
    {
        static T[] cached = new T[0];
        static int frame = -1;
        static int ver = -1;

        public static T[] Get()
        {
            if (frame != Time.frameCount || ver != version)
            {
                cached = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
                frame = Time.frameCount;
                ver = version;
            }
            return cached;
        }
    }

    // A cached entry can still go stale within a frame: Destroy is deferred
    // to end of frame, and pooled hazards are returned by deactivating them.
    // Both leave an entry that must not count as blocking.
    static bool IsLive(Component c)
    {
        return c != null && c.gameObject.activeInHierarchy;
    }

    // GetComponentsInChildren also allocates per call, and a hazard's size
    // never changes after import, so measure each object once and keep it.
    // ponytail: keyed on instance ID alone, which assumes a hazard's
    // lateral/forward footprint is fixed. True for every current hazard —
    // AlienWall is the only one that rescales at runtime and it only
    // touches Y, which neither axis below reads. Rescale a hazard in X/Z
    // at runtime and this needs a lossyScale check to invalidate.
    static readonly Dictionary<int, Vector3> extentsCache =
        new Dictionary<int, Vector3>();

    // Half-extent of an AABB projected onto an arbitrary direction (the
    // standard box/support-function projection) — not just a raw world
    // axis, since a hazard's true lateral "width" relative to the player's
    // CURRENT heading depends on which world axis is forward/right after a
    // 90-degree turn.
    static float ExtentAlong(Vector3 extents, Vector3 dir)
    {
        return extents.x * Mathf.Abs(dir.x)
             + extents.y * Mathf.Abs(dir.y)
             + extents.z * Mathf.Abs(dir.z);
    }

    static Vector3 CachedExtents(GameObject obj)
    {
        int id = obj.GetInstanceID();
        if (extentsCache.TryGetValue(id, out Vector3 cached))
            return cached;

        // Solid geometry only. Boulder and AlienWall both build a world-space
        // ember/glow ParticleSystem in Awake, and a world-simulated particle
        // renderer's bounds cover wherever its live particles have drifted —
        // nothing to do with the hazard's own silhouette. Including those
        // inflated a rolling boulder's measured footprint by several units,
        // which made spawners refuse perfectly clear nearby lanes.
        Bounds b = new Bounds();
        bool any = false;
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer ||
                r is LineRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }

        Vector3 extents = any ? b.extents : new Vector3(0.6f, 0.6f, 0.6f);
        extentsCache[id] = extents;
        return extents;
    }

    static void HalfExtents(
        GameObject obj, Vector3 right, Vector3 forward,
        out float half, out float depth)
    {
        Vector3 e = CachedExtents(obj);
        half = ExtentAlong(e, right);
        depth = ExtentAlong(e, forward);
    }

    // Reactive check for a mover (currently just Boulder): would this
    // hazard's real footprint overlap a T's real footprint, either right
    // now or after this frame's remaining travel (`sweep`)? Used to make
    // the mover yield (shatter/despawn) instead of rolling through it.
    //
    // margin defaults to 0 — unlike the spawn-time guard, which pads to
    // keep hazards visually separated, this one should fire only on a
    // genuine overlap. Padding it would shatter boulders that are merely
    // passing a neighbouring lane cleanly.
    public static bool BlockedAhead<T>(
        Transform self, Vector3 pathForward, float sweep, float margin = 0f)
        where T : Component
    {
        Vector3 right = Vector3.Cross(Vector3.up, pathForward).normalized;
        Vector3 myPos = self.position;
        float selfFwd = Vector3.Dot(myPos, pathForward);
        float selfRight = Vector3.Dot(myPos, right);
        HalfExtents(self.gameObject, right, pathForward,
            out float selfHalf, out float selfDepth);

        foreach (T t in Scan<T>.Get())
        {
            if (!IsLive(t)) continue;
            Transform other = t.transform;
            // A hazard whose script sits on a child (Comet's Meteorite
            // lives on a child named "default") would otherwise match
            // itself through its own root.
            if (other == self || other.IsChildOf(self) ||
                self.IsChildOf(other)) continue;

            float dRight = Mathf.Abs(Vector3.Dot(other.position, right) - selfRight);
            HalfExtents(t.gameObject, right, pathForward,
                out float otherHalf, out float otherDepth);

            // Lanes must genuinely overlap laterally.
            if (dRight >= selfHalf + otherHalf + margin) continue;

            // Positive dz means the other hazard lies in this mover's
            // direction of travel (the boulder rolls along -pathForward).
            // Window runs from "already overlapping" out to one frame of
            // travel, so a fast frame can't step clean over the check.
            float dz = selfFwd - Vector3.Dot(other.position, pathForward);
            float touch = selfDepth + otherDepth + margin;
            if (dz > -touch && dz < touch + sweep)
                return true;
        }
        return false;
    }

    // Spawn-time guard: is anything of type T already sitting near this
    // world position? Flat-tolerance overload, kept for the strafing
    // hazard, which needs a deliberately wider sweep than its own
    // footprint (it slides laterally after spawning).
    public static bool BlockedNear<T>(
        Vector3 pos, Vector3 pathForward,
        float laneTolerance = 1.2f, float zTolerance = 3f)
        where T : Component
    {
        Vector3 right = Vector3.Cross(Vector3.up, pathForward).normalized;
        float posFwd = Vector3.Dot(pos, pathForward);
        float posRight = Vector3.Dot(pos, right);

        foreach (T t in Scan<T>.Get())
        {
            if (!IsLive(t)) continue;
            Vector3 p = t.transform.position;
            float dRight = Mathf.Abs(Vector3.Dot(p, right) - posRight);
            float dFwd = Mathf.Abs(Vector3.Dot(p, pathForward) - posFwd);
            if (dRight < laneTolerance && dFwd < zTolerance)
                return true;
        }
        return false;
    }

    // Geometry-aware spawn-time guard: blocked when the CANDIDATE's own
    // real footprint (from candidatePrefab) would actually overlap an
    // existing T's real footprint. Works on either a live scene instance
    // or an un-instantiated prefab asset — Renderer.bounds is valid on
    // both.
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

        foreach (T t in Scan<T>.Get())
        {
            if (!IsLive(t)) continue;
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
