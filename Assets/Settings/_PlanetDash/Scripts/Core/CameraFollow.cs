using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float height = 5f;
    public float distance = 8f;
    public float smoothSpeed = 15f;
    public float tiltAmount = 5f;
    public float tiltSpeed = 8f;
 public float normalFOV = 90f;
public float sprintFOV = 110f;

    [Header("Intro (Temple Run style close chase-cam)")]
    public float introDuration = 2f;
    public float introHeight = 2.2f;
    public float introDistance = 3.2f;
    private float introElapsed = 0f;

    private float currentTilt = 0f;
    private float targetTilt = 0f;
    private Vector3 lastPlayerPos = Vector3.zero;
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

public UnityEngine.Rendering.Volume volume;
private UnityEngine.Rendering.Universal.Vignette vignette;

void Start()
{
    cam = GetComponent<Camera>();
    pc = FindObjectOfType<PlayerController>();
    if (volume != null)
        volume.profile.TryGet(out vignette);

    // Snap straight to the intro framing instead of lerping in from
    // wherever the camera happened to sit in the editor.
    if (target != null)
    {
        transform.position = target.position
            - target.forward * introDistance
            + Vector3.up * introHeight;
        lastPlayerPos = target.position;
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
        // of continuing to look down the old corridor.
        Vector3 desiredPos = target.position
            - target.forward * curDistance
            + Vector3.up * curHeight;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            smoothSpeed * Time.deltaTime
        ) + ScreenShake.CurrentOffset;

        // Detect lane change first — lateral movement along the
        // player's CURRENT right, not raw world X, so a lane change
        // right after a turn still pulses the FOV/tilt correctly.
        float playerXDiff = Vector3.Dot(
            target.position - lastPlayerPos, target.right);
        lastPlayerPos = target.position;

        // Speed feel: vignette closes in and baseline FOV widens as
        // the run gets faster, so velocity reads without any HUD.
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

        float baseFOV = normalFOV + speedPercent * 8f;

        // FOV pulse on lane change
        if (Mathf.Abs(playerXDiff) > 0.1f)
        {
            if (cam != null)
                cam.fieldOfView = Mathf.Lerp(
                    cam.fieldOfView, sprintFOV,
                    10f * Time.deltaTime);

            targetTilt = -playerXDiff * tiltAmount;
        }
        else
        {
            if (cam != null)
                cam.fieldOfView = Mathf.Lerp(
                    cam.fieldOfView, baseFOV,
                    5f * Time.deltaTime);

            targetTilt = 0f;
        }

        // Smooth tilt
        currentTilt = Mathf.Lerp(
            currentTilt, targetTilt,
            tiltSpeed * Time.deltaTime);

        // Base look direction now tracks the player's current heading
        // (yaw) instead of always facing world +Z, with the same fixed
        // downward pitch and lane-change tilt applied on top of it.
        Quaternion headingYaw = Quaternion.LookRotation(target.forward, Vector3.up);
        transform.rotation = headingYaw * Quaternion.Euler(
            20f, 0f, currentTilt);
    }
}