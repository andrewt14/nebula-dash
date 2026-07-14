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
    public float targetX = 0f;
    private float verticalVelocity = 0f;
    private CharacterController controller;
    private float slideDuration = 0.8f;
    private float slideTimer = 0f;
    // Ground-break pits remove their tile's collider entirely, so a missed
    // jump means the player keeps falling with nothing underneath — kill
    // once they've dropped far enough to be unrecoverable.
    public float fallDeathY = -8f;
public float GetTargetLaneX()
{
    return targetX;
}

    void Start()
    {
        controller = GetComponent<CharacterController>();
        targetX = 0f;
        currentLane = 1;
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

    float currentX = transform.position.x;
    float smoothX = Mathf.Lerp(currentX, targetX,
                    laneChangeSpeed * Time.deltaTime);

    Vector3 move = new Vector3(
        smoothX - currentX,
        verticalVelocity * Time.deltaTime,
        runSpeed * Time.deltaTime
    );

    controller.Move(move);

    // Force correct rotation
    transform.rotation = Quaternion.identity;

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
        GroundTileSpawner.IsInsidePit(transform.position.z, 0f) &&
        GameManager.Instance != null)
        GameManager.Instance.TriggerDeath();

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
            LaneLeft();

        // Lane right
        if (Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.RightArrow))
            LaneRight();

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            Jump();

        // Slide
        if (Input.GetKeyDown(KeyCode.S) && isGrounded)
            Slide();

        HandleSwipeInput();
    }

    // Matches the start screen's "SWIPE UP: JUMP / SWIPE DOWN: SLIDE /
    // SWIPE LEFT-RIGHT: MOVE" instructions — the actual touch controls
    // those instructions describe. Whichever axis moved further decides
    // whether it's a horizontal (lane) or vertical (jump/slide) swipe.
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
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            if (!touchTracking) return;
            touchTracking = false;

            Vector2 delta = t.position - touchStartPos;
            if (delta.magnitude < minSwipeDistance) return;

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                if (delta.x < 0f) LaneLeft(); else LaneRight();
            }
            else
            {
                if (delta.y > 0f) Jump(); else Slide();
            }
        }
    }

    void LaneLeft()
    {
        if (currentLane <= 0) return;
        currentLane--;
        targetX = (currentLane - 1) * laneWidth;
    }

    void LaneRight()
    {
        if (currentLane >= 2) return;
        currentLane++;
        targetX = (currentLane - 1) * laneWidth;
    }

    void Jump()
    {
        if (!isGrounded) return;
        verticalVelocity = jumpForce;
        isGrounded = false;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayJump();
        Handheld.Vibrate();
    }

    void Slide()
    {
        if (!isGrounded) return;
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
}