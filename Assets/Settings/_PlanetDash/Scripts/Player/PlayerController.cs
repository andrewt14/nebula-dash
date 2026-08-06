using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float runSpeed = 12f;
    public float jumpForce = 10f;
    public float laneWidth = 2.5f;
    public float laneChangeSpeed = 20f;
    public float gravity = -25f;
    public float turnVisualDuration = 0.28f;

    [Header("State")]
    public bool isGrounded = true;
    public bool isAlive = true;
    public bool isSliding = false;

    private int currentLane = 1;
    // Lateral offset from the corridor centerline, measured along the
    // player's CURRENT transform.right rather than world X — this is what
    // lets lane changes keep working after a 90-degree turn re-orients
    // "sideways". Tracked as explicit state (not re-derived from world
    // position) so it survives a turn's instantaneous heading change.
    private float currentLaneOffset = 0f;
    public float targetLaneOffset = 0f;
    // Lane captured the instant a pending turn is armed (see
    // CaptureTurnArmLane). The arming swipe then falls through to its own
    // lane change, so by the time the turn fires currentLane has already
    // drifted one step toward the turn direction — carrying THAT lane over
    // the corner is exactly what made a turn taken from dead center land
    // one lane over afterward. The carried lane is the one the player was
    // actually in when they committed to the turn.
    private int laneCarriedIntoTurn = 1;
    private bool carryLanePending = false;
    private float verticalVelocity = 0f;
    private CharacterController controller;
    private Transform visualRoot;
    private Coroutine turnVisualCoroutine;
    private bool isTurning = false;
    // Was 0.8s — read as sluggish against the run/obstacle pace. A
    // slide only needs to clear a low obstacle, not linger.
    private float slideDuration = 0.5f;
    private float slideTimer = 0f;
    // Ground-break pits remove their tile's collider entirely, so a missed
    // jump means the player keeps falling with nothing underneath — kill
    // once they've dropped far enough to be unrecoverable. Also doubles as
    // the "missed a 90-degree turn" death: once the track stops generating
    // past a turn point, running off the edge falls through exactly the
    // same way.
    public float fallDeathY = -8f;
    private float groundY;

    // Kept for existing callers (Boulder's mid-slow-mo dodge check) — now
    // compares lane OFFSETS directly instead of world X, since "target lane
    // X" isn't a stable world coordinate once the corridor can turn.
    public float GetTargetLaneOffset()
    {
        return targetLaneOffset;
    }

    public float GetCurrentLaneOffset()
    {
        return currentLaneOffset;
    }

    // Called by GroundTileSpawner the instant a pending turn is armed,
    // BEFORE the arming swipe falls through to its ordinary lane change.
    // Records the lane the player was in when they committed to the turn;
    // ExecuteTurn then carries THAT lane over the corner. The arming
    // swipe's own lane change still happens (it stays a live dodge), it
    // just no longer leaks past the pivot.
    public void CaptureTurnArmLane()
    {
        laneCarriedIntoTurn = currentLane;
        carryLanePending = true;
    }

    public float GetGroundY()
    {
        return groundY;
    }

    public float DistanceTravelled { get; private set; }
    public bool IsTurning => isTurning;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Animator childAnimator = GetComponentInChildren<Animator>();
        if (childAnimator != null)
            visualRoot = childAnimator.transform;
        targetLaneOffset = 0f;
        currentLaneOffset = 0f;
        currentLane = 1;
        groundY = transform.position.y;
        DistanceTravelled = 0f;
    }

    void Update()
    {
        if (!isAlive) return;
        HandleInput();
        HandleSlideTimer();
        Move();
    }

void Move()
{
    if (controller.isGrounded)
    {
        isGrounded = true;
        if (verticalVelocity < 0f)
            verticalVelocity = -2f;
    }
    else
    {
        isGrounded = false;
        verticalVelocity += gravity * Time.deltaTime;
    }

    float newOffset = Mathf.Lerp(currentLaneOffset, targetLaneOffset,
                    laneChangeSpeed * Time.deltaTime);
    float lateralDelta = newOffset - currentLaneOffset;
    currentLaneOffset = newOffset;

    // Forward/lateral movement is expressed in the player's CURRENT
    // heading (transform.forward/right) rather than hardcoded world Z/X,
    // so a 90-degree turn (which rotates this transform) is all that's
    // needed to redirect movement into the new corridor direction.
    Vector3 move = transform.forward * (runSpeed * Time.deltaTime)
                  + transform.right * lateralDelta
                  + Vector3.up * (verticalVelocity * Time.deltaTime);

    controller.Move(move);
    DistanceTravelled += runSpeed * Time.deltaTime;

    // A CharacterController crossing a short pit fast enough can keep
    // reporting isGrounded the whole way across — its capsule radius
    // bridges the gap between the near and far tile edges before gravity
    // has pulled it down enough to actually fail the ground check. That
    // read as "running straight over the break" with no consequence.
    // Catch it directly: grounded while positioned inside a real open
    // pit is only possible via that bridging exploit, since the pit's
    // tile collider is gone — a real clearance means being airborne
    // (isGrounded false) the whole time above it.
    if (isAlive && isGrounded &&
        GroundTileSpawner.IsInsidePit(transform.position, 0f) &&
        GameManager.Instance != null)
        GameManager.Instance.TriggerDeath();

    // Also the "missed a 90-degree turn" death: once GroundTileSpawner
    // stops extending the track past a pending turn, running straight off
    // the edge in the wrong direction falls through here exactly the same
    // way a missed jump over a broken tile does.
    if (isAlive && transform.position.y < fallDeathY &&
        GameManager.Instance != null)
        GameManager.Instance.TriggerDeath();
}

    // Minimum finger travel (pixels) before a touch counts as a deliberate
    // swipe rather than a tap/jitter.
    public float minSwipeDistance = 50f;
    private Vector2 touchStartPos;
    private bool touchTracking = false;

    void HandleInput()
    {
        // Lane left
        if (Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.LeftArrow))
        {
            HandleHorizontalInput(-1);
        }

        // Lane right
        if (Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.RightArrow))
        {
            HandleHorizontalInput(1);
        }

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            Jump();

        // Slide (also cancels a jump mid-air)
        if (Input.GetKeyDown(KeyCode.S))
            Slide();

        HandleSwipeInput();
    }

    // Matches the start screen's "SWIPE UP: JUMP / SWIPE DOWN: SLIDE /
    // SWIPE LEFT-RIGHT: MOVE" instructions — the actual touch controls
    // those instructions describe. Whichever axis moved further decides
    // whether it's a horizontal (lane) or vertical (jump/slide) swipe.
    //
    // Fires the instant the drag crosses minSwipeDistance (TouchPhase.
    // Moved) instead of waiting for the finger to lift (TouchPhase.
    // Ended) — waiting for release added a full input-to-action lag on
    // top of the swipe travel itself, which is what read as "slide is
    // too slow": the delay was in recognizing the input, not the slide
    // animation.
    void HandleSwipeInput()
    {
        if (Input.touchCount == 0)
        {
            touchTracking = false;
            return;
        }

        Touch t = Input.GetTouch(0);
        if (t.phase == TouchPhase.Began)
        {
            touchStartPos = t.position;
            touchTracking = true;
        }
        else if (t.phase == TouchPhase.Moved)
        {
            if (!touchTracking) return;

            Vector2 delta = t.position - touchStartPos;
            if (delta.magnitude < minSwipeDistance) return;

            touchTracking = false; // consume — only one action per swipe

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                HandleHorizontalInput(delta.x < 0f ? -1 : 1);
            }
            else
            {
                if (delta.y > 0f) Jump(); else Slide();
            }
        }
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            touchTracking = false;
        }
    }

    void HandleHorizontalInput(int direction)
    {
        if (GroundTileSpawner.Instance != null &&
            GroundTileSpawner.Instance.TryHandleTurnSwipe(direction))
            return;

        // isTurning only tracks the cosmetic child-model rotation catching
        // up after a turn (see SmoothVisualTurn) — the actual gameplay
        // transform has already snapped instantly in ExecuteTurn, so there
        // is no real reason to ignore lane-change input during it. Gating
        // on it meant every turn silently swallowed dodge input for the
        // next ~0.28s, reading as "controls stopped working."
        if (direction < 0) LaneLeft();
        else LaneRight();
    }

    void LaneLeft()
    {
        if (currentLane <= 0) return;
        currentLane--;
        targetLaneOffset = (currentLane - 1) * laneWidth;
    }

    void LaneRight()
    {
        if (currentLane >= 2) return;
        currentLane++;
        targetLaneOffset = (currentLane - 1) * laneWidth;
    }

    void Jump()
    {
        if (!isGrounded) return;
        verticalVelocity = jumpForce;
        isGrounded = false;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayJump();
    }

    void Slide()
    {
        if (!isGrounded)
        {
            // Swiping down mid-jump cancels the jump immediately and
            // drops the player straight to the ground instead of
            // waiting for the arc to finish.
            Vector3 pos = transform.position;
            pos.y = groundY;
            transform.position = pos;
            verticalVelocity = -2f;
            isGrounded = true;
        }
        isSliding = true;
        slideTimer = slideDuration;
    }

    void HandleSlideTimer()
    {
        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
                isSliding = false;
        }
    }

    // Called by GroundTileSpawner once a pending 90-degree turn resolves
    // (a matching swipe landed inside the reaction window). Rotates the
    // player to the new heading — everything downstream (movement,
    // camera follow, obstacle spawn/kill checks) reads transform.forward/
    // right/InverseTransformPoint, so this single rotation is what
    // actually redirects the whole run into the new corridor.
    public void ExecuteTurn(int direction, Vector3 pivotWorldPos)
    {
        Quaternion visualWorldRotation = Quaternion.identity;
        Quaternion visualTargetLocalRotation = Quaternion.identity;
        bool hasVisual = visualRoot != null;
        if (hasVisual)
        {
            visualWorldRotation = visualRoot.rotation;
            visualTargetLocalRotation = visualRoot.localRotation;
        }

        transform.rotation = Quaternion.AngleAxis(90f * direction, Vector3.up)
            * transform.rotation;

        // Snap onto the corridor's centerline at the turn point so a
        // mid-lane-change position doesn't leave the player clipping the
        // new corridor's edge geometry right after the turn. Keep the
        // player in whatever lane they were already in — only recenter to
        // the corridor's centerline, then re-apply that same lane's
        // offset along the NEW transform.right so the physical position
        // and the tracked offset stay in sync (a turn should carry your
        // lane over, not recenter you, same as Temple Run). Clamped and
        // computed as one atomic offset from the pivot so there's no
        // separate "set then nudge" step that could leave the two out of
        // sync for a frame.
        // Lane carried over the corner. Normally this is just the current
        // lane, but when the turn was armed (CaptureTurnArmLane) the
        // arming swipe's fall-through lane change has already nudged
        // currentLane one step toward the turn direction — carrying that
        // would put a turn taken from the middle lane into a side lane.
        // Restore the lane the player committed the turn from instead, and
        // keep currentLane consistent with it for later lane changes.
        if (carryLanePending)
        {
            currentLane = Mathf.Clamp(laneCarriedIntoTurn, 0, 2);
            carryLanePending = false;
        }
        else
        {
            currentLane = Mathf.Clamp(currentLane, 0, 2);
        }
        float laneOffset = (currentLane - 1) * laneWidth;
        targetLaneOffset = laneOffset;
        currentLaneOffset = laneOffset;

        transform.position = new Vector3(pivotWorldPos.x, transform.position.y, pivotWorldPos.z)
            + transform.right * laneOffset;

        // CharacterController caches collision/contact state from before
        // the teleport — left alone, it can read the sudden jump as
        // pushing into nearby geometry (the corner tile's edge, the seam
        // with the next tile) and spend the next few Move() calls
        // correcting itself sideways, drifting the player a couple of
        // units off-center right after the turn even though the position
        // set above is exact. Disabling and re-enabling clears that
        // stale state so Move() starts fresh from the new position.
        if (controller != null)
        {
            controller.enabled = false;
            controller.enabled = true;
        }

        if (hasVisual)
        {
            visualRoot.rotation = visualWorldRotation;
            if (turnVisualCoroutine != null)
                StopCoroutine(turnVisualCoroutine);
            turnVisualCoroutine = StartCoroutine(
                SmoothVisualTurn(visualRoot.localRotation,
                    visualTargetLocalRotation));
        }

        // The position/rotation snap above is discontinuous (not purely
        // along the old or new forward axis), so the camera's incremental
        // forward-projection anchor can't track it on its own — snap it
        // directly or the camera lags/whip-pans at the corner.
        CameraFollow camFollow = FindObjectOfType<CameraFollow>();
        if (camFollow != null)
            camFollow.SnapToPlayer();
    }

    System.Collections.IEnumerator SmoothVisualTurn(
        Quaternion fromLocal, Quaternion toLocal)
    {
        isTurning = true;
        float duration = Mathf.Max(0.01f, turnVisualDuration);
        float elapsed = 0f;

        while (elapsed < duration && visualRoot != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            visualRoot.localRotation = Quaternion.Slerp(
                fromLocal, toLocal, t);
            yield return null;
        }

        if (visualRoot != null)
            visualRoot.localRotation = toLocal;
        isTurning = false;
        turnVisualCoroutine = null;
    }
}
