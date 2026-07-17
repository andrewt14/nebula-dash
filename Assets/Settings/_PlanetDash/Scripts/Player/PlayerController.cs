using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float runSpeed = 12f;
    public float jumpForce = 10f;
    public float laneWidth = 2.5f;
    public float laneChangeSpeed = 20f;
    public float gravity = -25f;

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
    private float verticalVelocity = 0f;
    private CharacterController controller;
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

    void Start()
    {
        controller = GetComponent<CharacterController>();
        targetLaneOffset = 0f;
        currentLaneOffset = 0f;
        currentLane = 1;
        groundY = transform.position.y;
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

    // A CharacterController crossing a short pit fast enough can keep
    // reporting isGrounded the whole way across — its capsule radius
    // bridges the gap between the near and far tile edges before gravity
    // has pulled it down enough to actually fail the ground check. That
    // read as "running straight over the break" with no consequence.
    // Catch it directly: grounded while positioned inside a real open
    // pit is only possible via that bridging exploit, since the pit's
    // tile collider is gone — a real clearance means being airborne
    // (isGrounded false) the whole time above it. Pits only ever exist on
    // the pre-first-turn straight stretch, where world Z is still a
    // valid "along track" coordinate, so this check stays Z-based.
    if (isAlive && isGrounded &&
        GroundTileSpawner.IsInsidePit(transform.position.z, 0f) &&
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

    // Fired on every deliberate left/right input (swipe or key), with
    // -1 for left and +1 for right, regardless of whether the lane
    // change itself actually applied (e.g. already at the edge lane).
    // GroundTileSpawner listens for this to judge a pending 90-degree
    // turn — a swipe matching the turn's direction, while within its
    // reaction window, executes the turn instead of (in addition to) a
    // plain lane change.
    public static System.Action<int> OnSwipeDirection;

    void HandleInput()
    {
        // Lane left
        if (Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.LeftArrow))
        {
            OnSwipeDirection?.Invoke(-1);
            LaneLeft();
        }

        // Lane right
        if (Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.RightArrow))
        {
            OnSwipeDirection?.Invoke(1);
            LaneRight();
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
                if (delta.x < 0f) { OnSwipeDirection?.Invoke(-1); LaneLeft(); }
                else { OnSwipeDirection?.Invoke(1); LaneRight(); }
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
        transform.rotation = Quaternion.AngleAxis(90f * direction, Vector3.up)
            * transform.rotation;

        // Snap onto the corridor's centerline at the turn point so a
        // mid-lane-change position doesn't leave the player clipping the
        // new corridor's edge geometry right after the turn.
        Vector3 pos = transform.position;
        pos.x = pivotWorldPos.x;
        pos.z = pivotWorldPos.z;
        transform.position = pos;

        currentLaneOffset = 0f;
        targetLaneOffset = 0f;
        currentLane = 1;
    }
}
