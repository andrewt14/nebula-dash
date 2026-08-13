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

    // Two emission points only — feet/ankles — per the "scrap the
    // multi-point body-emission approach" direction. Each foot gets 3
    // layered systems (core/outer/wisps), all children of a small "socket"
    // transform whose ROTATION is actively driven every frame in
    // LateUpdate to face backward (opposite travel), independent of the
    // foot bone's own bind-pose axes or PointLimb's per-frame repose —
    // guessing a fixed local shape-rotation offset here was exactly the
    // mistake made earlier with the torso roll axis, so this sidesteps it
    // entirely rather than risk it again on bones whose bind orientation
    // was never measured.
    private Transform leftFootSocket, rightFootSocket;
    private ParticleSystem[] flameCore = new ParticleSystem[2];
    private ParticleSystem[] flameOuter = new ParticleSystem[2];
    private ParticleSystem[] flameWisps = new ParticleSystem[2];
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
    private Transform leftFoot, rightFoot;
    private Transform spine2;

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

    void Start()
    {
        PlayerAnimator pa = FindObjectOfType<PlayerAnimator>();
        PlayerController pc = FindObjectOfType<PlayerController>();
        if (pc != null) { playerTransform = pc.transform; playerController = pc; }

        if (pa != null && pa.animator != null)
        {
            visualRoot = pa.animator.transform;
            baseLocalPos = visualRoot.localPosition;
            baseLocalRot = visualRoot.localRotation;
            CacheLimbBones(pa.animator);
            CreateLight();

            Transform lAnchor = leftFoot != null ? leftFoot : leftLeg;
            Transform rAnchor = rightFoot != null ? rightFoot : rightLeg;
            leftFootSocket = CreateFootSocket(lAnchor, "JetpackFlameSocketLeft");
            rightFootSocket = CreateFootSocket(rAnchor, "JetpackFlameSocketRight");

            flameCore[0] = CreateFlameCore(leftFootSocket, "FlameCoreLeft");
            flameCore[1] = CreateFlameCore(rightFootSocket, "FlameCoreRight");
            flameOuter[0] = CreateFlameOuter(leftFootSocket, "FlameOuterLeft");
            flameOuter[1] = CreateFlameOuter(rightFootSocket, "FlameOuterRight");
            flameWisps[0] = CreateFlameWisps(leftFootSocket, "FlameWispsLeft");
            flameWisps[1] = CreateFlameWisps(rightFootSocket, "FlameWispsRight");

            // Emission module defaults to enabled on a freshly added
            // ParticleSystem — without this, the pre-configured bursts
            // above fire on their own interval from the moment the game
            // starts (walking included), well before the first jetpack
            // Activate() call.
            SetFlameEmitting(false);
        }
    }

    // Pulls the current zone's palette (same signal ResourceOrb already
    // tints pickups with) instead of a fixed color, so every flight visual
    // stays consistent with whichever neon zone is active.
    static void ZoneColors(out Color core, out Color secondary)
    {
        Color sky = ZoneManager.CurrentSkyColor;
        core = Color.Lerp(sky, Color.white, 0.55f);
        secondary = sky;
    }

    // Stylized "flame" palette: a near-white hot tip plus the zone's own
    // two-tone neon range, so the layered color-over-lifetime gradients
    // read as flame-shaped (hot -> primary -> secondary -> fade) while
    // staying in the game's existing color language instead of literal
    // orange/red fire.
    //
    // hot/primary are pushed past 1.0 (HDR) on purpose: the active
    // GameplayVolume's Bloom sits at threshold 1.2, so plain 0-1 colors
    // never cross it and never actually bloom — additive blending alone
    // just looks like a flat translucent overlay, which is why the flame
    // read as dull rather than glowy. secondary is left at the zone's
    // normal LDR color so only the flame's core/tip blooms, not its fading
    // edges.
    static void FlameColors(out Color hot, out Color primary, out Color secondary)
    {
        ZoneColors(out Color core, out Color sky);
        hot = Brighten(Color.Lerp(core, Color.white, 0.6f), 3.6f);
        primary = Brighten(core, 2.4f);
        secondary = sky;
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
    void PointLimb(Transform bone, Transform child, Vector3 desiredWorldDir)
    {
        if (bone == null || child == null) return;
        Vector3 currentDir = (child.position - bone.position).normalized;
        if (currentDir.sqrMagnitude < 0.0001f) return;
        Quaternion delta = Quaternion.FromToRotation(currentDir, desiredWorldDir);
        // Partially applied per poseBlend rather than a full snap — at
        // poseBlend 1 this is identical to the old always-full-strength
        // behavior; ramping it up/down over the transition is what turns
        // the limb redirect into a blend instead of an instant cut.
        bone.rotation = Quaternion.Slerp(bone.rotation, delta * bone.rotation, poseBlend);
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

        UpdateZoneVisuals();
        UpdateFlameIntensity();

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
        PointLimb(leftArm, leftForeArm, leftArmDir);
        StraightenChild(leftForeArm);
        PointLimb(rightArm, rightForeArm, rightArmDir);
        StraightenChild(rightForeArm);

        // Legs straight back, together.
        PointLimb(leftUpLeg, leftLeg, back);
        StraightenChild(leftLeg);
        PointLimb(rightUpLeg, rightLeg, back);
        StraightenChild(rightLeg);

        // Flame sockets: actively driven to face "back" every frame,
        // independent of the foot bone's own rotation (which PointLimb
        // only partially constrains — see class comment above the socket
        // fields). visualRoot.up as the up-hint keeps the socket's roll
        // around its own facing axis consistent frame-to-frame instead of
        // drifting arbitrarily.
        if (leftFootSocket != null)
            leftFootSocket.rotation = Quaternion.LookRotation(back, visualRoot.up);
        if (rightFootSocket != null)
            rightFootSocket.rotation = Quaternion.LookRotation(back, visualRoot.up);
    }

    // Re-tints every flight visual to the CURRENT zone each frame — a
    // long flight can outlast a zone transition (transitionDuration 4s vs
    // a zone lasting zoneDuration 16s), so a one-time color pulled at
    // Activate() would go stale mid-flight.
    void UpdateZoneVisuals()
    {
        FlameColors(out Color hot, out Color primary, out Color secondary);

        if (thrusterLight != null)
            thrusterLight.color = hot;

        for (int i = 0; i < 2; i++)
        {
            if (flameCore[i] != null)
            {
                var main = flameCore[i].main;
                main.startColor = new ParticleSystem.MinMaxGradient(hot, primary);
            }
            if (flameOuter[i] != null)
            {
                var main = flameOuter[i].main;
                main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
            }
            if (flameWisps[i] != null)
            {
                var main = flameWisps[i].main;
                main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
            }
        }
    }

    // Emission rate and flame size scale with how "hard" the player is
    // currently flying — either the natural runSpeed ramp or an active
    // SpeedBoost pickup, whichever is stronger — settling to a calmer
    // baseline when neither is elevated (gliding).
    void UpdateFlameIntensity()
    {
        float speedT = playerController != null
            ? Mathf.InverseLerp(16f, 110f, playerController.runSpeed) : 0f;
        float boostT = SpeedBoost.Instance != null
            ? Mathf.InverseLerp(1f, SpeedBoost.Instance.boostMultiplier, SpeedBoost.Instance.Multiplier)
            : 0f;
        float driveT = Mathf.Clamp01(Mathf.Max(speedT, boostT));
        flameDriveT = driveT;
        float sizeScale = Mathf.Lerp(0.85f, 1.25f, driveT);

        for (int i = 0; i < 2; i++)
        {
            if (flameCore[i] != null)
            {
                ApplyFlameBurst(flameCore[i], Mathf.Lerp(2f, 4f, driveT), Mathf.Lerp(4f, 7f, driveT), 0.1f);
                var m = flameCore[i].main;
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f * sizeScale, 0.09f * sizeScale);
            }
            if (flameOuter[i] != null)
            {
                ApplyFlameBurst(flameOuter[i], Mathf.Lerp(1f, 2f, driveT), Mathf.Lerp(3f, 5f, driveT), 0.12f);
                var m = flameOuter[i].main;
                m.startSize = new ParticleSystem.MinMaxCurve(0.09f * sizeScale, 0.16f * sizeScale);
            }
            if (flameWisps[i] != null)
            {
                var e = flameWisps[i].emission;
                e.rateOverTime = Mathf.Lerp(1.5f, 4f, driveT);
            }
        }
    }

    // Small nozzle-style light kept at the spine (general character glow)
    // rather than duplicated per foot — flickers, zone-tinted, cheap.
    void CreateLight()
    {
        Transform parent = spine2 != null ? spine2 : visualRoot;
        GameObject lightObj = new GameObject("JetpackFlameLight");
        lightObj.transform.SetParent(parent, false);
        lightObj.transform.localPosition = new Vector3(0f, 0f, -0.15f);
        thrusterLight = lightObj.AddComponent<Light>();
        FlameColors(out Color hot, out _, out _);
        thrusterLight.color = hot;
        thrusterLight.intensity = 0f;
        // Wide enough to visibly light nearby ground/geometry, not just
        // read as a glow confined to the particles themselves.
        thrusterLight.range = 7f;
    }

    // Position-only anchor at the foot/ankle bone — its ROTATION is driven
    // every frame in LateUpdate (see there for why), so nothing here needs
    // to guess the bone's own bind-pose orientation.
    Transform CreateFootSocket(Transform bone, string name)
    {
        if (bone == null) return null;
        GameObject go = new GameObject(name);
        go.transform.SetParent(bone, false);
        return go.transform;
    }

    // Inner flame core — small, fast, short-lived, hot-colored at the
    // foot, shrinking and cooling as it trails away. The "tip intensity"
    // of the flame.
    ParticleSystem CreateFlameCore(Transform socket, string name)
    {
        if (socket == null) return null;
        GameObject go = new GameObject(name);
        go.transform.SetParent(socket, false);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        FlameColors(out Color hot, out Color primary, out _);
        main.startColor = new ParticleSystem.MinMaxGradient(hot, primary);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        // Clumped bursts instead of a smooth continuous rate — a uniform
        // rate reads as an even spray, not fire; real flame isn't
        // symmetrical. UpdateFlameIntensity/SetFlameEmitting rewrite this
        // burst's count range live.
        ApplyFlameBurst(ps, 2f, 4f, 0.1f);

        // Narrow cone along the socket's own forward (actively kept
        // pointing backward every frame — see LateUpdate).
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.02f;

        // Decelerates rather than shooting straight — billows.
        var limitVel = ps.limitVelocityOverLifetime;
        limitVel.enabled = true;
        limitVel.dampen = 0.7f;
        limitVel.limit = 0.5f;

        // World-space upward bias ON TOP OF the local backward emission —
        // real flame licks upward from heat, it doesn't just stream
        // backward off the mesh.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        // x/y/z must all share one MinMaxCurve mode (Unity logs "Particle
        // Velocity curves must all be in the same mode" and silently
        // misbehaves otherwise) — x/z pinned to 0 in the same
        // TwoConstants mode as y instead of left at their mismatched
        // Constant-mode default.
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Moderate-strong noise so each lick wavers unpredictably instead
        // of riding a smooth line — this is the single biggest driver of
        // "looks like fire" vs. "looks like colored particles".
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 0.9f;
        noise.scrollSpeed = 1.6f;

        // Per-particle twist so licks visibly dance instead of holding a
        // static orientation.
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-140f, 140f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(hot, 0f), new GradientColorKey(primary, 0.6f),
                    new GradientColorKey(primary, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.5f),
                    new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = g;

        // Moderate at spawn, brief ~15% billow right after, then tapers to
        // near-zero — a flat/shrinking-only curve reads as a puff; this
        // slight growth-then-taper is what makes it read as a pointed
        // flame tongue instead.
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve coreSizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.18f, 0.7f), new Keyframe(1f, 0.02f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, coreSizeCurve);

        // Stretched, not Billboard — round particles read as smoke/glow,
        // not fire. Aligned to velocity by default in Stretch mode, so a
        // tall lengthScale is enough to make each particle a small
        // elongated flame lick rather than a dot.
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5f;
        renderer.velocityScale = 0.3f;
        renderer.sortingOrder = 1; // in front of outer/wisps — dominant silhouette
        renderer.material = FlameMaterial();

        ps.Play();
        return ps;
    }

    // Additive-glow material shared by all three flame layers (see
    // FlameParticle.shader) — soft-additive blend + radial falloff so the
    // gradients configured below actually read as glowing licks instead
    // of flat alpha-blended quads. Falls back to a stock unlit shader if
    // the custom one isn't found (e.g. not yet imported).
    static Material flameMaterialTemplate;
    static Material FlameMaterial()
    {
        if (flameMaterialTemplate == null)
        {
            Shader shader = Shader.Find("NebulaDash/FlameParticle");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            flameMaterialTemplate = shader != null ? new Material(shader) : null;
        }
        return flameMaterialTemplate;
    }

    // Rewrites a system's repeating bursts in place — used both at
    // creation and by UpdateFlameIntensity to scale burst density with
    // speed/boost. Two staggered, differently-timed bursts instead of one
    // — a single evenly-spaced repeat reads as one uniform symmetrical
    // pulse; overlapping two independent periods produces an uneven beat
    // pattern that reads as a few overlapping flame tongues instead.
    static void ApplyFlameBurst(ParticleSystem ps, float minCount, float maxCount, float interval)
    {
        if (ps == null) return;
        var emission = ps.emission;
        emission.SetBursts(new[] {
            new ParticleSystem.Burst(0f, (short)minCount, (short)maxCount, 0, interval),
            new ParticleSystem.Burst(interval * 0.5f, (short)Mathf.Max(1, minCount * 0.5f),
                (short)Mathf.Max(1, maxCount * 0.7f), 0, interval * 1.37f)
        });
    }

    // Outer flame body — bigger, slower, longer-lived, the 3-color
    // layered gradient that sells "colorful flame" rather than a flat
    // single-tint trail. Widens then tapers, like a flame licking upward.
    ParticleSystem CreateFlameOuter(Transform socket, string name)
    {
        if (socket == null) return null;
        GameObject go = new GameObject(name);
        go.transform.SetParent(socket, false);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        FlameColors(out Color hot, out Color primary, out Color secondary);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        ApplyFlameBurst(ps, 1f, 3f, 0.12f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.03f;

        var limitVel = ps.limitVelocityOverLifetime;
        limitVel.enabled = true;
        limitVel.dampen = 0.6f;
        limitVel.limit = 0.4f;

        // Smaller world-space upward bias than the core — the outer body
        // trails a bit more before it licks upward.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.55f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 1.1f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-100f, 100f);

        // Full 4-stop gradient: hot -> primary -> secondary -> transparent.
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(hot, 0f), new GradientColorKey(primary, 0.4f),
                    new GradientColorKey(secondary, 0.75f), new GradientColorKey(secondary, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.4f),
                    new GradientAlphaKey(0.3f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = g;

        // Moderate at spawn, ~20% billow, taper to near-zero — same
        // tapered-lick shape as the core, at the outer body's own scale.
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.65f), new Keyframe(0.25f, 0.78f), new Keyframe(1f, 0.03f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        // Stretch, not Billboard — a Billboard quad always faces the
        // camera, so the shader's directional teardrop taper (aligned to
        // the stretch axis, which follows velocity) had no consistent
        // orientation on it and just looked like a flat glowing card.
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3.5f;
        renderer.velocityScale = 0.35f;
        renderer.sortingOrder = 0; // behind the core, in front of wisps
        renderer.material = FlameMaterial();

        ps.Play();
        return ps;
    }

    // Wisps/embers — sparse particles that break off the flame and drift
    // with random horizontal wander, gravity/drag, fading out. Stretched
    // billboard (unlike the other two layers) so they read as flicking
    // off rather than puffing.
    ParticleSystem CreateFlameWisps(Transform socket, string name)
    {
        if (socket == null) return null;
        GameObject go = new GameObject(name);
        go.transform.SetParent(socket, false);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Gentle upward buoyancy — the Drag module below provides the
        // "settling/arcing" half of the described gravity+drag motion.
        main.gravityModifier = -0.04f;
        FlameColors(out _, out Color primary, out Color secondary);
        main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);

        var emission = ps.emission;
        emission.rateOverTime = 0f; // sparse — set low in UpdateFlameIntensity/Activate

        // Slightly wider than the core/outer cones — these are meant to
        // visibly break off at an angle, not stay in the tight column.
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.03f;

        // No ParticleSystem.drag module exists in the scripting API (the
        // Inspector's "Drag" checkbox isn't independently scriptable) —
        // limitVelocityOverLifetime gives the same settling/arcing effect.
        var limitVel = ps.limitVelocityOverLifetime;
        limitVel.enabled = true;
        limitVel.dampen = 0.5f;
        limitVel.limit = 0.3f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.8f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(primary, 0f), new GradientColorKey(secondary, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.4f, 0.5f),
                    new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = g;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.8f;
        renderer.velocityScale = 0.2f;
        // Behind the core/outer layers — wisps are meant to add
        // atmosphere at the edges, not compete with the main flame
        // silhouette for attention.
        renderer.sortingOrder = -1;
        renderer.material = FlameMaterial();

        ps.Play();
        return ps;
    }

    public void Activate(float duration)
    {
        if (IsActive || visualRoot == null) return;
        StartCoroutine(FlightCoroutine(duration));
    }

    void SetFlameEmitting(bool on)
    {
        for (int i = 0; i < 2; i++)
        {
            // Core/outer use pre-configured bursts (see ApplyFlameBurst) —
            // toggling the whole emission module on/off starts/stops that
            // repeating burst cleanly without touching the burst config.
            if (flameCore[i] != null)
            {
                var e = flameCore[i].emission;
                e.enabled = on;
            }
            if (flameOuter[i] != null)
            {
                var e = flameOuter[i].emission;
                e.enabled = on;
            }
            if (flameWisps[i] != null)
            {
                var e = flameWisps[i].emission;
                e.rateOverTime = on ? 2.5f : 0f;
            }
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

        SetFlameEmitting(true);

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
                thrusterLight.intensity = Mathf.Lerp(0f, 3.5f, eased);
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
                // Perlin, not sine — a sine flicker is too regular/
                // mechanical to read as fire. Baseline scales with
                // flameDriveT (the same speed/boost driver the particle
                // bursts use) so the light visibly pulses WITH the flame,
                // not on its own disconnected schedule.
                float lightNoise = Mathf.PerlinNoise(flicker * 9f, 0.37f);
                thrusterLight.intensity = Mathf.Lerp(2.8f, 4.6f, flameDriveT) + (lightNoise - 0.5f) * 1.8f;
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
            visualRoot.localPosition = Vector3.Lerp(fromPos, baseLocalPos, eased);
            visualRoot.localRotation = Quaternion.Slerp(fromRot, baseLocalRot, eased);
            poseBlend = 1f - eased;
            CurrentBankDegrees = Mathf.Lerp(bankAtLandingStart, 0f, eased);
            if (thrusterLight != null)
                thrusterLight.intensity = Mathf.Lerp(3.5f, 0f, eased);
            yield return null;
        }
        visualRoot.localPosition = baseLocalPos;
        visualRoot.localRotation = baseLocalRot;
        poseBlend = 0f;
        CurrentBankDegrees = 0f;

        SetFlameEmitting(false);
        if (thrusterLight != null)
            thrusterLight.intensity = 0f;
        IsActive = false;
    }
}
