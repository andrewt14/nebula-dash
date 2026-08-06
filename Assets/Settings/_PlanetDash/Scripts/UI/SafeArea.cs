using UnityEngine;

// Fits its RectTransform to Screen.safeArea so children never sit under a
// notch, Dynamic Island, or the home indicator. Drop this on the top-level
// container of any UI panel that touches the screen edges (HUD, pause
// menu, game over screen, start screen, achievements panel) — nested
// content lays out normally relative to it.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    // Extra clearance pulled in from the OS-reported safe area, in
    // reference/canvas units (scaled the same way the Canvas scales
    // everything else), applied top and bottom only — the notch/Dynamic
    // Island and home indicator are the only edges a portrait phone
    // actually cuts into. Cutout size isn't identical across iPhone
    // generations, so anchoring content flush against whatever THIS
    // device happens to report reads as fine on the device it was tuned
    // against and clipped-or-touching on the next one. This buffer is
    // the system-level fix for that instead of hand-tuning every child
    // element's own offset per device.
    public float verticalPadding = 16f;

    private RectTransform rect;
    private Canvas parentCanvas;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);
    private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        Apply();
    }

    void Update()
    {
        // Safe area can change at runtime (device rotation, iOS
        // multitasking split view) — cheap to recheck, only reapplies
        // when it actually changes.
        if (Screen.safeArea != lastSafeArea || Screen.orientation != lastOrientation)
            Apply();
    }

    void Apply()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastOrientation = Screen.orientation;

        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        float scale = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
        float padPx = verticalPadding * scale;
        anchorMin.y += padPx;
        anchorMax.y -= padPx;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
    }
}
