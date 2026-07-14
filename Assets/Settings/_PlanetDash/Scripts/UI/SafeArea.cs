using UnityEngine;

// Fits its RectTransform to Screen.safeArea so children never sit under a
// notch, Dynamic Island, or the home indicator. Drop this on the top-level
// container of any UI panel that touches the screen edges (HUD, pause
// menu, game over screen, start screen, achievements panel) — nested
// content lays out normally relative to it.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform rect;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);
    private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
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
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
    }
}
