using UnityEngine;
using System.Collections;

// Subway Surfers/Temple Run-style jetpack: the one power-up that changes
// how the player LOOKS and what they're immune to for its whole duration,
// instead of another stat multiplier stacked on top of the existing
// SpeedBoost/GoldOrb/InvincibilityOrb set.
//
// Deliberately doesn't touch PlayerController's CharacterController
// movement at all — that logic is precisely tuned and load-bearing, and
// re-routing real gameplay position through a flight state would risk
// breaking turns, lane changes, and every existing hazard's kill-check
// math. Instead: the player's animated visual child rises (with a gentle
// bob) on a purely cosmetic local offset, while GameManager's EXISTING
// invincibility system (same one InvincibilityOrb already uses) makes
// ground hazards genuinely harmless for the same window — so it reads as
// flying and really is safe, without a second parallel safety system to
// get wrong. Reusing ActivateInvincibility also gets the countdown UI and
// the magnet-pull freebie for free.
//
// Went through four poses. Straight-up rise with the run cycle still
// playing underneath read as "running in the air" — tried CrossFading
// into a dedicated Flying.fbx clip next, which turned out to be a dead
// end: that FBX is a raw Mixamo download whose curves target
// "mixamorig:Hips/..." paths, but this character's actual skeleton
// (MixamoCharacter/Ch06/mixamorig9:Hips/...) uses a "9"-suffixed prefix
// Unity assigned on import to avoid a naming collision — confirmed
// directly (queried a limb bone's rotation before and after activating
// flight: byte-identical, proving the clip drives zero bones on this
// rig). A forward-leaning tilt was tried and rejected as "should be
// upright" — then explicitly reversed to a full horizontal Superman/
// Iron-Man dive ("horizontal/diving... head leading"), pitched 90
// degrees forward. Since the imported clip can't pose the limbs, this
// now builds the streamlined dive pose procedurally instead (see
// PoseLimbs) — redirecting the real arm/leg bones to trail straight back
// every frame, driven by the same pitch/bob/roll already applied to the
// root.
public class JetpackEffect : MonoBehaviour
{
    public static JetpackEffect Instance;
    public float flightHeight = 3.2f;
    public float transitionSpeed = 3.5f;
    // Full horizontal dive — head leading the direction of travel.
    public float flightPitchDegrees = 90f;
    // Gentle floating bob layered on top of flightHeight once fully
    // risen — small and slow enough to read as buoyant, not jittery.
    public float bobAmplitude = 0.35f;
    public float bobSpeed = 2.2f;
    // Continuous side-to-side sway, always running while airborne (not
    // gated on input) — a different speed than the bob so the two don't
    // fall into lockstep and read as one rigid combined wobble. Bumped
    // from 20/1.5 — that read as too subtle to notice during flight;
    // exposed here (not hardcoded) so it can be dialed in by feel in the
    // Inspector without a code change.
    public float rollAmplitude = 42f;
    public float rollSpeed = 2.4f;
    // Responsive bank into lane-switches (plane-style), self-righting once
    // the lateral motion settles — chosen over a continuous barrel-roll
    // because it reads as skill/control, matching a runner where lane
    // input is the actual mechanic. Driven by PlayerController's lane-
    // change EVENT (direction + timestamp) on its own fixed envelope,
    // NOT by differentiating the lane-offset lerp itself — that was tried
    // first and measured (live, via Unity MCP) to peak at only ~15% of
    // bankAmplitude, because the lerp resolves (~50ms) faster than any
    // smoothing on the bank side can track before the signal collapses.
    public float bankAmplitude = 38f;
    public float bankRiseTime = 0.12f;
    public float bankFallTime = 0.35f;
    // Fifth pivot: the hand-rolled shader shell is replaced with the
    // imported Wallcoeur fire VFX package (real particle art — flame +
    // smoke layers on a clean flame-silhouette texture) positioned at
    // multiple body points (torso/hands/feet) instead of one shell mesh,
    // per the confirmed "wraps the whole character, not just feet"
    // direction. See CreateAura.
    public float auraPulseSpeed = 2.4f;
    public bool IsActive { get; private set; } = false;
    // Current roll+bank angle (degrees), read by CameraFollow so the
    // camera's tilt tracks the body's ACTUAL rotation instead of a fixed
    // constant — eased to 0 during the rise/descend transitions (see
    // FlightCoroutine) rather than snapping, matching how every other
    // per-frame visual here transitions.
    public float CurrentBankDegrees { get; private set; } = 0f;

    private Transform visualRoot;
    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;

    // Path under a Resources folder (see class comment above) — Resources.Load
    // works in real builds, unlike AssetDatabase, which is editor-only and
    // would silently break the moment this ships.
    const string AuraPrefabResourcePath = "VFX/VFX_Fire";

    // One VFX_Fire instance per anchor point (torso + both hands + both
    // feet), each parented directly to its bone so it follows the
    // animated pose for free with zero extra per-frame positioning code.
    // See CreateAura.
    private System.Collections.Generic.List<GameObject> auraInstances = new System.Collections.Generic.List<GameObject>();
    private System.Collections.Generic.List<ParticleSystem> auraParticles = new System.Collections.Generic.List<ParticleSystem>();
    // Cached 0..1 pulse value, written by UpdateAuraVisuals and read by
    // FlightCoroutine to drive the light in sync with the aura.
    private float auraPulse;
    private Light thrusterLight;
    private Transform playerTransform;
    private PlayerController playerController;

    // Real bone names on this character's actual rig (see class comment)
    // — NOT the Mixamo-standard "mixamorig:" prefix the Flying.fbx clip
    // uses. Root-of-chain bones only; each child (forearm/shin) is
    // already close to a straight bind pose (verified directly: forearm
    // rotations were within a few degrees of identity), so redirecting
    // just the shoulder/hip carries the rest of the limb rigidly along
    // with it — no separate elbow/knee correction needed.
    private Transform leftArm, leftForeArm, rightArm, rightForeArm;
    private Transform leftUpLeg, leftLeg, rightUpLeg, rightLeg;
    private Transform leftHand, rightHand, leftFoot, rightFoot;
    private Transform spine2;
    // Rest-pose LOCAL rotation of each PointLimb'd bone, captured once at
    // Start() — see PointLimb for why this is needed (the old version had
    // no fixed "return to" reference and would freeze the limb wherever
    // flight left it instead of actually landing back in a normal pose).
    private Quaternion leftArmBindRot, rightArmBindRot, leftUpLegBindRot, rightUpLegBindRot;

    // Updated every frame by UpdateFlameIntensity — read by the thruster
    // light so it pulses in sync with the SAME speed/boost driver the
    // particle bursts scale off, instead of an independent fixed flicker.
    private float flameDriveT = 0f;

    // 0 = no limb override (Run pose shows through), 1 = full dive pose.
    // Driven by the same eased progress as the body's rise/descend so the
    // limbs blend in/out over the transition instead of snapping the
    // instant IsActive flips.
    private float poseBlend = 0f;

    void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        // JetpackEffect lives on the persistent --MANAGERS-- object, whose
        // Start() can run before the Player has spawned (e.g. after a
        // respawn/scene reload) — a one-shot FindObjectOfType lookup here
        // would silently no-op forever the moment it fires too early,
        // since Start() only ever runs once. Poll instead of assuming the
        // player already exists by the time this runs.
        PlayerAnimator pa = null;
        PlayerController pc = null;
        while (pa == null || pa.animator == null || pc == null)
        {
            pa = FindObjectOfType<PlayerAnimator>();
            pc = FindObjectOfType<PlayerController>();
            if (pa == null || pa.animator == null || pc == null)
                yield return null;
        }
        playerTransform = pc.transform;
        playerController = pc;

        visualRoot = pa.animator.transform;
        baseLocalPos = visualRoot.localPosition;
        baseLocalRot = visualRoot.localRotation;
        CacheLimbBones(pa.animator);
        if (leftArm != null) leftArmBindRot = leftArm.localRotation;
        if (rightArm != null) rightArmBindRot = rightArm.localRotation;
        if (leftUpLeg != null) leftUpLegBindRot = leftUpLeg.localRotation;
        if (rightUpLeg != null) rightUpLegBindRot = rightUpLeg.localRotation;
        CreateLight();
        CreateAura();

        // Hidden until the first jetpack Activate() call.
        SetAuraVisible(false);
    }

    // Fixed purple-fire palette — NOT zone-tinted. The old version derived
    // hot/primary/secondary from ZoneManager.CurrentSkyColor, so the flame
    // only ever looked "purple" when the current zone happened to be
    // purple; that's the actual reason it never read as a consistent
    // purple flame despite tuning. Matches the requested 4-stop gradient:
    // white-hot -> violet/magenta core -> deep indigo-purple -> fade.
    //
    // Pushed past 1.0 (HDR) on purpose: the active GameplayVolume's Bloom
    // sits at threshold 1.2, so plain 0-1 colors never cross it and never
    // actually bloom.
    static void FlameColors(out Color hot, out Color core, out Color deep)
    {
        hot = Brighten(new Color(1f, 0.96f, 1f), 3.5f);
        core = Brighten(new Color(0.72f, 0.12f, 1f), 2.6f);
        deep = Brighten(new Color(0.30f, 0.05f, 0.55f), 1.5f);
    }

    // Scales RGB only (not alpha) so brightened colors stay HDR for bloom
    // without also inflating blend alpha.
    static Color Brighten(Color c, float mult) => new Color(c.r * mult, c.g * mult, c.b * mult, c.a);

    // The player's model isn't a fixed prefab, and the roster turned out to
    // have MORE naming schemes than the two originally found (confirmed
    // live, via Unity MCP, across three separate skins in this same
    // session): "mixamorig9:" (a Unity-assigned suffix avoiding a naming
    // collision), plain lowercase ("l arm"), AND an unsuffixed
    // "mixamorig:" — that third one isn't matched by either of the first
    // two candidate lists, so on that character every lookup below used to
    // return null, which silently no-op'd BOTH the limb-straightening in
    // LateUpdate and the flame-socket creation in Start() — the root cause
    // behind "bent limbs" and "missing particles" being the same character
    // on inspection. Fixed at the source instead of extending the name
    // list a third time (a fourth skin would just break it again): every
    // rig here is a properly configured Humanoid avatar, so
    // Animator.GetBoneTransform(HumanBodyBones.X) resolves the correct
    // bone regardless of its underlying name — confirmed live, this
    // returns "mixamorig:LeftFoot" etc. correctly on the character that
    // broke the old name-list approach. The string-based lookup stays only
    // as a fallback for a hypothetical non-Humanoid rig.
    void CacheLimbBones(Animator anim)
    {
        var all = visualRoot.GetComponentsInChildren<Transform>(true);
        var byName = new System.Collections.Generic.Dictionary<string, Transform>();
        foreach (var t in all)
            if (!byName.ContainsKey(t.name)) byName[t.name] = t;

        bool humanoid = anim != null && anim.isHuman;

        leftArm = HumanBone(anim, humanoid, HumanBodyBones.LeftUpperArm, byName, "mixamorig9:LeftArm", "l arm");
        leftForeArm = HumanBone(anim, humanoid, HumanBodyBones.LeftLowerArm, byName, "mixamorig9:LeftForeArm", "l forearm");
        rightArm = HumanBone(anim, humanoid, HumanBodyBones.RightUpperArm, byName, "mixamorig9:RightArm", "r arm");
        rightForeArm = HumanBone(anim, humanoid, HumanBodyBones.RightLowerArm, byName, "mixamorig9:RightForeArm", "r forearm");
        leftUpLeg = HumanBone(anim, humanoid, HumanBodyBones.LeftUpperLeg, byName, "mixamorig9:LeftUpLeg", "l leg");
        leftLeg = HumanBone(anim, humanoid, HumanBodyBones.LeftLowerLeg, byName, "mixamorig9:LeftLeg", "l knee");
        rightUpLeg = HumanBone(anim, humanoid, HumanBodyBones.RightUpperLeg, byName, "mixamorig9:RightUpLeg", "r leg");
        rightLeg = HumanBone(anim, humanoid, HumanBodyBones.RightLowerLeg, byName, "mixamorig9:RightLeg", "r knee");
        leftHand = HumanBone(anim, humanoid, HumanBodyBones.LeftHand, byName, "mixamorig9:LeftHand", "l hand");
        rightHand = HumanBone(anim, humanoid, HumanBodyBones.RightHand, byName, "mixamorig9:RightHand", "r hand");
        leftFoot = HumanBone(anim, humanoid, HumanBodyBones.LeftFoot, byName, "mixamorig9:LeftFoot", "l foot");
        rightFoot = HumanBone(anim, humanoid, HumanBodyBones.RightFoot, byName, "mixamorig9:RightFoot", "r foot");
        spine2 = HumanBone(anim, humanoid, HumanBodyBones.Chest, byName, "mixamorig9:Spine2", "spine3");
    }

    Transform HumanBone(Animator anim, bool humanoid, HumanBodyBones bone,
        System.Collections.Generic.Dictionary<string, Transform> byName,
        params string[] candidates)
    {
        if (humanoid)
        {
            Transform t = anim.GetBoneTransform(bone);
            if (t != null) return t;
        }
        return FindBone(byName, candidates);
    }

    Transform FindBone(System.Collections.Generic.Dictionary<string, Transform> byName,
        params string[] candidates)
    {
        foreach (var name in candidates)
            if (byName.TryGetValue(name, out Transform t))
                return t;
        return null;
    }

    // Redirects a limb's root bone so the vector to its own child (the
    // limb's current pointing direction) faces `desiredWorldDir` instead
    // — a FromTo correction applied in world space, so it works
    // regardless of this rig's own local bind-pose axis conventions
    // (which differ wildly per bone, e.g. LeftShoulder's bind rotation is
    // nowhere near identity). Rotating the root carries the child
    // (forearm/shin) rigidly along with it.
    //
    // Reset-then-Slerp from the cached BIND rotation every frame, not an
    // incremental Slerp from the bone's own previous-frame value — the
    // old version (`Slerp(bone.rotation, delta * bone.rotation,
    // poseBlend)`) had no fixed "return to" reference: at poseBlend 0,
    // Slerp(x, y, 0) == x is a no-op, so as landing eased poseBlend back
    // toward 0 the leg/arm just froze wherever the previous frame left it
    // instead of actually returning to a normal standing pose — confirmed
    // live (leg bones still showing ~166°/233° euler angles, character
    // stuck lying prone) well after IsActive went false and poseBlend hit
    // 0. Recomputing from the fixed bind pose each frame makes poseBlend
    // a real blend between two fixed endpoints (bind at 0, fully-pointed
    // at 1) with no path dependency, so it always lands cleanly.
    void PointLimb(Transform bone, Transform child, Vector3 desiredWorldDir, Quaternion bindLocalRot)
    {
        if (bone == null || child == null) return;
        bone.localRotation = bindLocalRot;
        Vector3 currentDir = (child.position - bone.position).normalized;
        if (currentDir.sqrMagnitude < 0.0001f) return;
        Quaternion delta = Quaternion.FromToRotation(currentDir, desiredWorldDir);
        Quaternion fullyPointedWorldRot = delta * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, fullyPointedWorldRot, poseBlend);
    }

    // Straightens the elbow/knee itself — PointLimb only aims the UPPER
    // bone (shoulder/hip) and carries the child rigidly along, it never
    // touches the child's own local rotation. That was fine when the
    // child was near its bind pose, but the Flying state can't drive
    // bones on this rig (see class comment) — CrossFading into it leaves
    // whatever the Run cycle's last-evaluated stride pose was frozen on
    // the forearm/shin, which is bent mid-stride, not straight. Blending
    // the child's LOCAL rotation toward identity (this rig's bind pose
    // for these bones, per the class comment) straightens the joint
    // regardless of which stride frame flight happened to start on.
    void StraightenChild(Transform child)
    {
        if (child == null) return;
        child.localRotation = Quaternion.Slerp(child.localRotation, Quaternion.identity, poseBlend);
    }

    // Streamlined dive pose: arms and legs trailing straight back along
    // the direction of travel. Runs in LateUpdate so it overrides
    // whatever the Animator just evaluated that frame (Run's last pose,
    // since the Flying clip itself can't drive these bones — see class
    // comment) rather than being immediately overwritten by it.
    void LateUpdate()
    {
        if (!IsActive || playerTransform == null) return;

        UpdateAuraVisuals();

        // Uses visualRoot's OWN current world axes, not playerTransform's
        // — using the unrotated parent here was the actual reason banking
        // read as invisible: the torso really was rotating (verified via
        // Quaternion.Angle live), but the arms/legs dominate the
        // silhouette and were being locked to a fixed world orientation
        // every frame regardless of visualRoot's roll/bank, so the part
        // of the body that would sell a bank never moved. visualRoot's
        // axes already carry the full pitch+bob+roll+bank each frame, so
        // sourcing directions from them makes the limbs sweep WITH the
        // torso instead of staying world-locked underneath it.
        //
        // Axis mapping after the 90-degree dive pitch (measured live, not
        // assumed): visualRoot.up now points along the direction of
        // travel (the pre-pitch forward), visualRoot.forward now points
        // straight down (the pre-pitch up), visualRoot.right is largely
        // unchanged. So "back" (where limbs trail) is -up, and the old
        // downward droop offset (was playerTransform.up * -0.15) is now
        // +forward.
        Vector3 back = -visualRoot.up;
        // Arms trail back and slightly outward/down from the shoulders —
        // dead straight back with zero spread looked like the arms were
        // clipping through the torso.
        Vector3 leftArmDir = (back + visualRoot.right * -0.35f
            + visualRoot.forward * 0.15f).normalized;
        Vector3 rightArmDir = (back + visualRoot.right * 0.35f
            + visualRoot.forward * 0.15f).normalized;
        PointLimb(leftArm, leftForeArm, leftArmDir, leftArmBindRot);
        StraightenChild(leftForeArm);
        PointLimb(rightArm, rightForeArm, rightArmDir, rightArmBindRot);
        StraightenChild(rightForeArm);

        // Legs straight back, together.
        PointLimb(leftUpLeg, leftLeg, back, leftUpLegBindRot);
        StraightenChild(leftLeg);
        PointLimb(rightUpLeg, rightLeg, back, rightUpLegBindRot);
        StraightenChild(rightLeg);
    }

    // Drives every per-frame aura visual. Instances need no per-frame
    // positioning (they're parented directly to bones — see CreateAura);
    // this only scales playback speed with the same speed/boost driver
    // used elsewhere in the flight system, and drives the light pulse.
    void UpdateAuraVisuals()
    {
        float speedT = playerController != null
            ? Mathf.InverseLerp(16f, 110f, playerController.runSpeed) : 0f;
        float boostT = SpeedBoost.Instance != null
            ? Mathf.InverseLerp(1f, SpeedBoost.Instance.boostMultiplier, SpeedBoost.Instance.Multiplier)
            : 0f;
        float driveT = Mathf.Clamp01(Mathf.Max(speedT, boostT));
        flameDriveT = driveT;

        auraPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * auraPulseSpeed);

        float playbackSpeed = Mathf.Lerp(0.85f, 1.6f, driveT);
        for (int i = 0; i < auraParticles.Count; i++)
            if (auraParticles[i] != null) auraParticles[i].playbackSpeed = playbackSpeed;
    }

    // Small nozzle-style light kept at the spine (general character glow)
    // rather than duplicated per point — cheap, and its intensity is tied
    // to the same pulse the aura uses (see FlightCoroutine).
    void CreateLight()
    {
        Transform parent = spine2 != null ? spine2 : visualRoot;
        GameObject lightObj = new GameObject("JetpackFlameLight");
        lightObj.transform.SetParent(parent, false);
        lightObj.transform.localPosition = new Vector3(0f, 0f, -0.15f);
        thrusterLight = lightObj.AddComponent<Light>();
        // Gradient's mid-tone (core), not the white-hot tip.
        FlameColors(out _, out Color core, out _);
        thrusterLight.color = core;
        thrusterLight.intensity = 0f;
        // Wide enough to visibly light nearby ground/geometry, not just
        // read as a glow confined to the flame VFX itself.
        thrusterLight.range = 7f;
    }

    // Encapsulates every renderer under root — used once to scale the
    // aura VFX relative to the character's ACTUAL size instead of a
    // guessed constant, since the player model isn't a fixed prefab (see
    // CacheLimbBones' class comment on skin variety).
    static Bounds ComputeCharacterBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one * 2f);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // Wraps flame around the WHOLE character per the confirmed reference
    // direction, not just the feet: one VFX_Fire instance each at the
    // torso (main plume), both hands, and both feet. Head/face
    // deliberately excluded so it never obscures the character's
    // readability. Each instance is parented straight to its bone, so it
    // follows the live animated pose (dive pitch, limb trail, bank) with
    // zero extra per-frame code.
    void CreateAura()
    {
        GameObject prefab = Resources.Load<GameObject>(AuraPrefabResourcePath);
        if (prefab == null) return;

        Bounds charBounds = ComputeCharacterBounds(visualRoot);
        float charHeight = Mathf.Max(charBounds.size.y, 0.5f);

        AddAuraPoint(prefab, spine2, new Vector3(0f, 0f, -0.1f), charHeight * 0.55f);
        AddAuraPoint(prefab, leftHand, Vector3.zero, charHeight * 0.28f);
        AddAuraPoint(prefab, rightHand, Vector3.zero, charHeight * 0.28f);
        AddAuraPoint(prefab, leftFoot, Vector3.zero, charHeight * 0.3f);
        AddAuraPoint(prefab, rightFoot, Vector3.zero, charHeight * 0.3f);
    }

    // Instantiates one VFX_Fire clone at a bone and recolors it purple.
    // The pack's flame texture is a clean white/alpha silhouette (no
    // baked-in orange), so a colorOverLifetime tint takes the palette
    // cleanly instead of muddying against existing color.
    void AddAuraPoint(GameObject prefab, Transform bone, Vector3 localOffset, float scale)
    {
        if (bone == null) return;
        GameObject inst = Instantiate(prefab, bone);
        inst.name = "JetpackAura_" + bone.name;
        inst.transform.localPosition = localOffset;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one * scale;

        FlameColors(out Color hot, out Color core, out Color deep);
        var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            bool isFlameLayer = psRenderer != null && psRenderer.sharedMaterial != null
                && psRenderer.sharedMaterial.name.Contains("Flame");

            // The pack's default startColor is a baked-in orange constant
            // — final particle color is startColor * colorOverLifetime, a
            // pure multiply, so an orange startColor caps how purple the
            // result can ever read regardless of the gradient below (the
            // low blue channel in orange can't be multiplied back up).
            // Neutralize it to white so the gradient fully controls hue.
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient g = new Gradient();
            if (isFlameLayer)
            {
                g.SetKeys(
                    new[] { new GradientColorKey(hot, 0f), new GradientColorKey(core, 0.4f), new GradientColorKey(deep, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            }
            else
            {
                // Smoke layer — dimmer/deeper so it reads as atmosphere
                // behind the flame layer, not competing with it.
                g.SetKeys(
                    new[] { new GradientColorKey(deep, 0f), new GradientColorKey(deep, 1f) },
                    new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0f, 1f) });
            }
            colorOverLifetime.color = g;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            auraParticles.Add(ps);
        }

        inst.SetActive(false);
        auraInstances.Add(inst);
    }

    public void Activate(float duration)
    {
        if (IsActive || visualRoot == null) return;
        StartCoroutine(FlightCoroutine(duration));
    }

    void SetAuraVisible(bool on)
    {
        if (on)
        {
            for (int i = 0; i < auraInstances.Count; i++)
                if (auraInstances[i] != null) auraInstances[i].SetActive(true);
            for (int i = 0; i < auraParticles.Count; i++)
                if (auraParticles[i] != null) auraParticles[i].Play();
        }
        else
        {
            for (int i = 0; i < auraParticles.Count; i++)
                if (auraParticles[i] != null) auraParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < auraInstances.Count; i++)
                if (auraInstances[i] != null) auraInstances[i].SetActive(false);
        }
    }

    IEnumerator FlightCoroutine(float duration)
    {
        IsActive = true;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayJetpackActivate();

        // +1s covers the descend tail below so invincibility doesn't run
        // out while the player is still visually airborne.
        if (GameManager.Instance != null)
            GameManager.Instance.ActivateInvincibility(duration + 1f);

        SetAuraVisible(true);

        // The pitch (dive) is fixed for the whole flight — only the roll
        // on top of it oscillates. Applying pitch first, then roll,
        // means the roll banks around the character's OWN current
        // (now-horizontal) forward axis, not the original standing one.
        Quaternion pitchedRot = baseLocalRot * Quaternion.Euler(flightPitchDegrees, 0f, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            visualRoot.localPosition = Vector3.Lerp(
                baseLocalPos, baseLocalPos + Vector3.up * flightHeight, eased);
            visualRoot.localRotation = Quaternion.Slerp(
                baseLocalRot, pitchedRot, eased);
            poseBlend = eased;
            if (thrusterLight != null)
                thrusterLight.intensity = Mathf.Lerp(0f, 2.2f, eased);
            yield return null;
        }
        poseBlend = 1f;

        float flicker = 0f;
        float elapsed = 0f;
        Vector3 flightPos = baseLocalPos + Vector3.up * flightHeight;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            flicker += Time.deltaTime;
            // Gentle continuous bob plus a slower side-to-side ambient
            // wobble — different speeds so the two never lock into one
            // rigid combined motion.
            float bob = Mathf.Sin(flicker * bobSpeed) * bobAmplitude;
            visualRoot.localPosition = flightPos + Vector3.up * bob;

            // Bank into lane-switches like a plane rolling into a turn:
            // a fixed-duration envelope (quick rise, slower self-righting
            // fall) triggered by the lane-change EVENT, independent of how
            // fast the actual lane-offset lerp resolves.
            float bank = 0f;
            if (playerController != null)
            {
                float sinceChange = Time.time - playerController.LastLaneChangeTime;
                float bankDuration = bankRiseTime + bankFallTime;
                if (sinceChange >= 0f && sinceChange < bankDuration)
                {
                    float envelope = sinceChange < bankRiseTime
                        ? Mathf.SmoothStep(0f, 1f, sinceChange / bankRiseTime)
                        : Mathf.SmoothStep(1f, 0f, (sinceChange - bankRiseTime) / bankFallTime);
                    bank = -playerController.LastLaneChangeDir * bankAmplitude * envelope;
                }
            }

            // Y-axis here, not Z — measured live (raw quaternion, not
            // eulerAngles, which is ambiguous at this exact 90-degree
            // pitch): a Z rotation in this pre-pitch frame carries through
            // pitchedRot to spin around world (0,-1,0), i.e. pure vertical
            // yaw ("turning flat", as reported) — a Y rotation carries
            // through to spin around the actual travel axis, i.e. a real
            // side-to-side bank/roll.
            float roll = Mathf.Sin(flicker * rollSpeed) * rollAmplitude;
            CurrentBankDegrees = roll + bank;
            visualRoot.localRotation = pitchedRot * Quaternion.Euler(0f, CurrentBankDegrees, 0f);
            if (thrusterLight != null)
            {
                // Synced to the shield's own pulse (see UpdateShieldVisuals)
                // rather than an independent flicker, so the light visibly
                // brightens/dims WITH the shield's glow.
                thrusterLight.intensity = Mathf.Lerp(1.6f, 2.8f, flameDriveT) * Mathf.Lerp(0.85f, 1.15f, auraPulse);
            }
            yield return null;
        }

        Vector3 fromPos = visualRoot.localPosition;
        Quaternion fromRot = visualRoot.localRotation;
        float bankAtLandingStart = CurrentBankDegrees;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            // Rotation/pose unwinds on an ACCELERATED timeline (2x),
            // deliberately decoupled from position's own pace — with both
            // tied to the same `eased` value, the character spent the
            // MIDDLE of the descent pitched diagonally at a LOW height,
            // and the legs (still pointed "back" along the tilted body,
            // per PointLimb) swept through an arc that visibly dipped
            // below the floor before the pose finished resolving.
            // Confirmed live: "legs land below the ground and then it
            // adjusts". Finishing the pitch/pose FIRST, while position is
            // still safely elevated, means the risky diagonal-legs
            // configuration and "close to the ground" never overlap.
            float rotEased = Mathf.Clamp01(eased * 2f);
            visualRoot.localPosition = Vector3.Lerp(fromPos, baseLocalPos, eased);
            visualRoot.localRotation = Quaternion.Slerp(fromRot, baseLocalRot, rotEased);
            poseBlend = 1f - rotEased;
            CurrentBankDegrees = Mathf.Lerp(bankAtLandingStart, 0f, rotEased);
            if (thrusterLight != null)
                thrusterLight.intensity = Mathf.Lerp(2.2f, 0f, eased);
            yield return null;
        }
        visualRoot.localPosition = baseLocalPos;
        visualRoot.localRotation = baseLocalRot;
        poseBlend = 0f;
        CurrentBankDegrees = 0f;
        // Explicit final snap, matching visualRoot's own hard reset above —
        // PointLimb's per-frame blend already converges these to bind by
        // now, but LateUpdate is about to stop running entirely (IsActive
        // goes false below) and never touch these bones again, so there's
        // no next frame to correct any last sliver of drift.
        if (leftArm != null) leftArm.localRotation = leftArmBindRot;
        if (rightArm != null) rightArm.localRotation = rightArmBindRot;
        if (leftUpLeg != null) leftUpLeg.localRotation = leftUpLegBindRot;
        if (rightUpLeg != null) rightUpLeg.localRotation = rightUpLegBindRot;

        SetAuraVisible(false);
        if (thrusterLight != null)
            thrusterLight.intensity = 0f;
        IsActive = false;
    }
}
