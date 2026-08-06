using UnityEngine;
using TMPro;
using System.Collections;

public class ScorePopup : MonoBehaviour
{
    public static ScorePopup Instance;
    public GameObject popupPrefab;
    public Canvas canvas;

    void Awake()
    {
        Instance = this;
    }

    public void ShowPopup(string text, Vector3 worldPos)
    {
        ShowPopup(text, worldPos, 0.8f, Color.white);
    }

    // duration/tint let callers like the level-up banner hold longer and
    // read as a distinct "big" event instead of the default quick pickup
    // blip, without needing a second parallel popup system.
    public void ShowPopup(string text, Vector3 worldPos, float duration, Color tint)
    {
        if (popupPrefab == null || canvas == null) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        StartCoroutine(PopupCoroutine(text, screenPos, duration, tint));
    }

    // Fixed screen-space banner near the top, for events that aren't
    // anchored to any world position — achievement unlocks need to read
    // as distinct from pickup popups (which float from the player), not
    // compete with them at the same on-screen spot.
    //
    // Turn banners in particular fire twice in quick succession ("TURN
    // LEFT" the moment it's telegraphed, then "TURN READY" the instant
    // the player arms it, often well inside the first banner's 2.5s
    // life) — spawning an independent popup instance per call let both
    // exist on screen at once, overlapping/flickering over each other.
    // Cancelling any still-running banner before starting a new one
    // means only the latest is ever visible.
    private Coroutine topBannerCoroutine;
    private GameObject topBannerObj;

    // Was 0.88 — measured against the HUD's multiplier text (top-anchored
    // just under the safe area, so its own screen position already
    // shifts a bit across devices), a level-name banner with the longest
    // zone name wraps to 3 lines and, at 0.88, only cleared it by ~110px
    // on the exact target device (iPhone 17 Pro). The two use different
    // positioning systems (this is a raw screen-height fraction; the
    // multiplier text is a safe-area-anchored offset), so that margin
    // isn't guaranteed to hold on every aspect ratio. Lowered for a
    // comfortable buffer that can't collide regardless of device.
    //
    // Distinct from TurnBannerHeightFraction below — this banner and the
    // turn banner are two entirely independent GameObjects/coroutines
    // with no mutual-exclusion logic between them (only same-type calls
    // cancel each other), and a level-up can land while a turn is being
    // telegraphed. They used to share the exact same position (0.88 for
    // both), so whenever that happened the two texts rendered on top of
    // each other.
    private const float TopBannerHeightFraction = 0.72f;
    private const float TurnBannerHeightFraction = 0.80f;

    public void ShowTopBanner(string text, float duration, Color tint)
    {
        if (popupPrefab == null || canvas == null) return;
        if (topBannerCoroutine != null)
            StopCoroutine(topBannerCoroutine);
        if (topBannerObj != null)
            Destroy(topBannerObj);
        Vector2 screenPos = new Vector2(
            Screen.width * 0.5f, Screen.height * TopBannerHeightFraction);
        topBannerCoroutine = StartCoroutine(TopBannerCoroutine(text, screenPos, duration, tint));
    }

    IEnumerator TopBannerCoroutine(string text, Vector2 screenPos, float duration, Color tint)
    {
        yield return PopupCoroutine(text, screenPos, duration, tint, obj => topBannerObj = obj);
        topBannerObj = null;
        topBannerCoroutine = null;
    }

    // Single persistent object for the whole turn callout (was two
    // separate popups — a fire-and-forget "TURN LEFT" banner plus an
    // independent countdown number placed 260px to its right — which
    // could overlap/flicker against each other since they were driven by
    // separate coroutines with different lifetimes). Now it's one object
    // whose text is just updated in place every frame, same idea as the
    // score popup but with no fade/instantiate churn.
    private GameObject turnBannerObj;
    private TextMeshProUGUI turnBannerTmp;
    private Coroutine turnBannerHideCoroutine;

    public void ShowTurnBanner(string text, Color tint)
    {
        if (popupPrefab == null || canvas == null) return;
        if (turnBannerHideCoroutine != null)
        {
            StopCoroutine(turnBannerHideCoroutine);
            turnBannerHideCoroutine = null;
        }
        if (turnBannerObj == null)
        {
            turnBannerObj = Instantiate(popupPrefab, canvas.transform);
            turnBannerTmp = turnBannerObj.GetComponent<TextMeshProUGUI>();
        }
        turnBannerObj.SetActive(true);
        turnBannerObj.transform.localScale = Vector3.one;
        turnBannerObj.GetComponent<RectTransform>().position =
            new Vector2(Screen.width * 0.5f, Screen.height * TurnBannerHeightFraction);
        if (turnBannerTmp != null)
        {
            turnBannerTmp.text = text;
            turnBannerTmp.color = tint;
            turnBannerTmp.outlineWidth = 0f;
        }
    }

    // For the brief "TURN READY" flash once armed — shows then hides
    // itself, instead of being fought over by GroundTileSpawner's
    // per-frame Update (which would otherwise hide it again next frame).
    public void ShowTurnBannerTemporary(string text, Color tint, float duration)
    {
        ShowTurnBanner(text, tint);
        turnBannerHideCoroutine = StartCoroutine(HideTurnBannerAfter(duration));
    }

    IEnumerator HideTurnBannerAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideTurnBanner();
        turnBannerHideCoroutine = null;
    }

    public void HideTurnBanner()
    {
        if (turnBannerObj != null)
            turnBannerObj.SetActive(false);
    }

    IEnumerator PopupCoroutine(string text, Vector2 screenPos, float duration, Color tint,
        System.Action<GameObject> onCreated = null)
    {
        GameObject popup = Instantiate(
            popupPrefab, canvas.transform);
        onCreated?.Invoke(popup);
        TextMeshProUGUI tmp =
            popup.GetComponent<TextMeshProUGUI>();
        if (tmp == null) yield break;
        tmp.text = text;
        // Colored outline reads as a soft glow without needing a
        // dedicated glow shader/material.
        tmp.outlineWidth = tint == Color.white ? 0f : 0.25f;
        tmp.outlineColor = tint;

        popup.GetComponent<RectTransform>().position =
            screenPos;

        float timer = 0f;
        Vector3 startPos = popup.transform.position;
        // Fade-out only covers the last 0.8s (matching the original
        // popup's pace) — everything before that is a full-opacity hold,
        // so a longer duration reads as "on screen longer", not "floats
        // up more slowly".
        float fadeStart = Mathf.Max(0f, duration - 0.8f);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            popup.transform.position = startPos +
                Vector3.up * 30f * t;

            float fadeT = fadeStart >= duration ? t :
                Mathf.Clamp01((timer - fadeStart) / (duration - fadeStart));
            tmp.color = new Color(
                tint.r, tint.g, tint.b,
                Mathf.Lerp(1f, 0f, fadeT));

            float scale = t < 0.3f ?
                Mathf.Lerp(0f, 1.3f, t / 0.3f) :
                Mathf.Lerp(1.3f, 0.8f,
                    (t - 0.3f) / 0.7f);
            popup.transform.localScale =
                Vector3.one * scale;

            yield return null;
        }

        Destroy(popup);
    }
}