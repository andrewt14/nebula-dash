using System.Collections.Generic;
using UnityEngine;

// Guarantees fair, readable lane patterns:
//  - one lane is always reserved "safe" (clear) over a rolling Z-segment,
//    so all three lanes are NEVER blocked at the same track position;
//  - the same lane never gets back-to-back obstacles (alternates between
//    the two open lanes);
//  - as difficulty rises, the safe stretch shortens and safe gaps get
//    rarer, so pressure increases without ever removing the escape lane.
public class LaneSpacingManager : MonoBehaviour
{
    private static LaneSpacingManager _instance;
    public static LaneSpacingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("LaneSpacingManager");
                _instance = go.AddComponent<LaneSpacingManager>();
            }
            return _instance;
        }
    }

    public int laneCount = 3;
    [Range(0f, 1f)] public float safeGapChance = 0.15f;

    [Header("Difficulty Tightening")]
    public float easiestSafeGapChance = 0.15f;
    public float hardestSafeGapChance = 0.04f;
    // How long (in world units) a lane stays reserved-clear. Longer =
    // easier (predictable safe lane); shorter = harder.
    public float easiestSafeSegment = 42f;
    public float hardestSafeSegment = 20f;
    public float difficultyAtMaxTightness = 160f;

    private float safeSegment = 42f;
    private int safeLane = 1;
    private float safeLaneUntilZ = -9999f;
    private int lastLane = -1;

    [Header("Formations (multi-lane blocks)")]
    // Single-lane blocks unlock first; harder two-lane blocks (forcing
    // the one exact safe lane) unlock later, so formation complexity is
    // introduced progressively over a run.
    public float formationUnlockDifficulty = 25f;
    public float hardFormationUnlockDifficulty = 70f;
    [Range(0f, 1f)] public float hardFormationChance = 0.5f;

    void Awake()
    {
        if (_instance == null) _instance = this;
    }

    public void SetDifficulty(float difficulty)
    {
        float t = Mathf.Clamp01(difficulty / difficultyAtMaxTightness);
        safeGapChance = Mathf.Lerp(easiestSafeGapChance, hardestSafeGapChance, t);
        safeSegment = Mathf.Lerp(easiestSafeSegment, hardestSafeSegment, t);
    }

    // Rotate the reserved safe lane once the current segment is passed.
    void EnsureSafeLane(float spawnZ)
    {
        if (spawnZ >= safeLaneUntilZ)
        {
            int newSafe = Random.Range(0, laneCount);
            if (newSafe == safeLane)
                newSafe = (safeLane + 1 + Random.Range(0, laneCount - 1)) % laneCount;
            safeLane = newSafe;
            safeLaneUntilZ = spawnZ + safeSegment;
        }
    }

    // Returns a lane for a single obstacle: never the reserved safe lane,
    // and preferably not the last lane used (avoids same-lane repeats).
    public int PickLane(float spawnZ)
    {
        EnsureSafeLane(spawnZ);

        List<int> open = new List<int>();
        for (int i = 0; i < laneCount; i++)
            if (i != safeLane) open.Add(i);

        List<int> pref = new List<int>();
        foreach (int l in open)
            if (l != lastLane) pref.Add(l);

        List<int> pick = pref.Count > 0 ? pref : open;
        int lane = pick[Random.Range(0, pick.Count)];
        lastLane = lane;
        return lane;
    }

    public bool ShouldInsertSafeGap()
    {
        return Random.value < safeGapChance;
    }

    // For coordinated formation events: blocks the non-safe lanes (two
    // for a hard formation, one for an easy one), ALWAYS leaving the
    // reserved safe lane open. Returns null before formations unlock.
    public int[] GetFormationBlockedLanes(float difficulty, float spawnZ)
    {
        if (difficulty < formationUnlockDifficulty) return null;
        EnsureSafeLane(spawnZ);

        bool hard = difficulty >= hardFormationUnlockDifficulty &&
                    Random.value < hardFormationChance;

        List<int> blocked = new List<int>();
        for (int i = 0; i < laneCount; i++)
            if (i != safeLane) blocked.Add(i);

        if (!hard && blocked.Count > 1)
        {
            int keep = blocked[Random.Range(0, blocked.Count)];
            blocked.Clear();
            blocked.Add(keep);
        }

        lastLane = blocked[blocked.Count - 1];
        return blocked.ToArray();
    }
}
