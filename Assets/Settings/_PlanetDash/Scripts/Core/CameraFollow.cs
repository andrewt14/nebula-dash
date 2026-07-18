using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 5f;
    public float distance = 8f;
    public float smoothSpeed = 15f;
 public float normalFOV = 90f;
    // Slower rotation-only Slerp rate used for a short window right after
    // a 90-degree turn — rotating at the same rate as ordinary heading
    // drift (smoothSpeed) reads as a near-instant whip-pan across the
    // whole 90 degrees; easing it in more gradually reads as an actual
    // camera turn instead of a snap-cut.
    public float turnRotationDuration = 0.65f;
    // Captured start/end rotation for an explicit eased tween through a
    // turn, instead of an exponential Slerp-toward-target. An exponential
    // Slerp moves fastest at the start and asymptotically crawls the last
    // few degrees — it never reads as smooth because the tail is always
    // slightly "loose". A fixed-duration SmoothStep tween between two
    // captured endpoints eases in AND out and finishes exactly on time.
    private Quaternion turnRotFrom;
    private Quaternion turnRotTo;
    private float turnRotStartTime = -999f;
    private bool turnRotating = false;

    [Header("Intro (Temple Run style close chase-cam)")]
    public float introDuration = 2f;
    public float introHeight = 2.2f;
    public float introDistance = 3.2f;
    private float introElapsed = 0f;

    // Camera anchor: follows the player's forward/vertical movement only.
    // Lane changes are a right-axis offset on the player, and the camera
    // used to track that too, panning the whole view sideways every lane
    // change and shoving the opposite lanes toward/off the screen edge.
    private Vector3 anchorPos = Vector3.zero;
    private float dangerPulse = 0f;
    private Camera cam;
    private PlayerController pc;

    private static readonly Color DangerColor =
        new Color(0.8f, 0.05f, 0.05f);

    // Red vignette pulse used as an incoming-obstacle warning.
    public void DangerPulse()
    {
        dangerPulse = 1f;
    }

    // Called by PlayerController.ExecuteTurn the instant a 90-degree turn
    // resolves — the player's position and forward both jump discontinuously
    // at that moment (rotate + snap onto the new corridor), so the anchor's
    // usual incremental forward-projection can't track it (the jump isn't
    // purely along either the old or new forward axis) and the camera was
    // left lagging behind, reading as the player falling off-screen mid-turn.
    public void SnapToPlayer()
    {
        if (target != null)
        {
            // target.position here already has the carried-over lane
            // offset baked in along the NEW transform.right (ExecuteTurn
            // repositions onto pivot + right*laneOffset before calling
            // this). Anchoring straight to that position permanently
            // pinned the camera off-center toward whichever lane the
            // player was in when they turned — strip it back out so the
            // anchor sits on the corridor centerline, same as every
            // other frame.
            float laneOffset = pc != null ? pc.GetCurrentLaneOffset() : 0f;
            anchorPos = target.position - target.right * laneOffset;

            // Position snaps onto the new anchor, but stays behind the
            // camera's CURRENT (still old, pre-turn) facing direction —
            // not target.forward. Position and rotation must always agree
            // on where the camera is "looking from"; snapping position to
            // sit behind the NEW heading while rotation was still aimed at
            // the OLD one (LateUpdate eases rotation separately, below)
            // is exactly what made the turn look disconnected from the
            // character — the camera would teleport to a spot that only
            // made sense once the rotation caught up, moments later.
            // LateUpdate keeps this same invariant every frame after this
            // (see its own position derivation from transform.forward).
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = target.forward;
            flatForward.Normalize();
            transform.position = anchorPos
                - flatForward * distance + Vector3.up * height;

            turnRotFrom = transform.rotation;
            turnRotTo = Quaternion.LookRotation(target.forward, Vector3.up)
                * Quaternion.Euler(20f, 0f, 0f);
            turnRotStartTime = Time.time;
            turnRotating = true;
        }
    }

public UnityEngine.Rendering.Volume volume;
private UnityEngine.Rendering.Universal.Vignette vignette;

void Start()
{
    cam = GetComponent<Camera>();
    pc = FindObjectOfType<PlayerController>();
    if (volume != null)
        volume.profile.TryGet(out vignette);
    if (cam != null)
        cam.fieldOfView = normalFOV;

    // Snap straight to the intro framing instead of lerping in from
    // wherever the camera happened to sit in the editor.
    if (target != null)
    {
        transform.position = target.position
            - target.forward * introDistance
            + Vector3.up * introHeight;
        anchorPos = target.position;
    }
}

    void LateUpdate()
    {
        if (target == null) return;

        // First few seconds: close, low, right behind the character —
        // then ease out to the normal gameplay framing.
        introElapsed += Time.deltaTime;
        float introT = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(introElapsed / introDuration));
        float curHeight = Mathf.Lerp(introHeight, height, introT);
        float curDistance = Mathf.Lerp(introDistance, distance, introT);

        // Follows behind the player along their CURRENT heading
        // (transform.forward) instead of a hardcoded world Z offset, so
        // the camera swings around with them at a 90-degree turn instead
        // of continuing to look down the old corridor. Only the
        // forward-axis component of the player's movement is tracked —
        // right-axis (lane change) and vertical (jump) motion are both
        // stripped out, same as the original fixed-height camera, so
        // neither panning lanes nor jumping shifts the camera at all.
        // Forward-axis only. A previous "settled" fold-in of lateral
        // drift here looked defensive but actually fired on every lane
        // change (Mathf.Lerp hits the target value exactly once the
        // remaining gap rounds to zero, so Approximately went true right
        // as each lane change finished), dragging the camera sideways
        // into the new lane every time — that was the reported pan.
        Vector3 anchorDelta = target.position - anchorPos;
        anchorPos += Vector3.Dot(anchorDelta, target.forward) * target.forward;

        // Rotation updates BEFORE position, and position is derived from
        // the camera's own (just-updated) facing — not target.forward
        // directly. Position previously chased target.forward at full
        // smoothSpeed while rotation eased in separately/more slowly
        // during a turn, so for a few frames the camera sat in the spot
        // for the NEW heading while still visibly looking the OLD way —
        // disconnected from the character. Deriving position from
        // transform.forward keeps the two always in agreement, at every
        // point during (and outside) a turn's easing window.
        if (turnRotating)
        {
            float t = (Time.time - turnRotStartTime) / turnRotationDuration;
            if (t >= 1f)
            {
                transform.rotation = turnRotTo;
                turnRotating = false;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(
                    turnRotFrom, turnRotTo, Mathf.SmoothStep(0f, 1f, t));
            }
        }
        else
        {
            Quaternion desiredRot =
                Quaternion.LookRotation(target.forward, Vector3.up)
                * Quaternion.Euler(20f, 0f, 0f);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRot, smoothSpeed * Time.deltaTime);
        }

        Vector3 camFacing = transform.forward;
        camFacing.y = 0f;
        if (camFacing.sqrMagnitude < 0.0001f) camFacing = target.forward;
        camFacing.Normalize();

        Vector3 desiredPos = anchorPos
            - camFacing * curDistance
            + Vector3.up * curHeight;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            smoothSpeed * Time.deltaTime
        ) + ScreenShake.CurrentOffset;

        // Speed feel: vignette closes in as the run gets faster, so
        // velocity reads without any HUD (FOV itself stays fixed).
        float speedPercent = pc != null
            ? Mathf.InverseLerp(12f, 60f, pc.runSpeed)
            : 0f;

        if (dangerPulse > 0f)
            dangerPulse = Mathf.Max(
                0f, dangerPulse - Time.deltaTime * 2.5f);

        if (vignette != null)
        {
            // "Dark moment" mood (zone darkness + storm) closes the
            // vignette in around a still-readable center instead of
            // dimming the actual scene lighting — tunnel vision rather
            // than a genuinely dark screen.
            float moodDark = Mathf.Clamp01(
                ZoneManager.DarknessAmount * 0.5f +
                WeatherManager.StormIntensity * 0.5f);
            vignette.intensity.value = Mathf.Clamp01(Mathf.Lerp(
                0.3f, 0.7f, speedPercent) + moodDark * 0.25f +
                dangerPulse * 0.15f);
            vignette.color.value = Color.Lerp(
                Color.black, DangerColor, dangerPulse);
        }
    }
}