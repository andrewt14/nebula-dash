using UnityEngine;

// Exposes the current shake as an offset for the camera to add onto its
// own position, instead of writing to transform.position directly — this
// component and CameraFollow live on the same GameObject, and
// CameraFollow.LateUpdate() unconditionally reassigns transform.position
// every frame, which was silently overwriting (and completely hiding)
// any shake this used to apply directly.
public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance;
    public static Vector3 CurrentOffset { get; private set; }

    private float timeRemaining = 0f;
    private float magnitude = 0f;

    void Awake()
    {
        Instance = this;
    }

    public void Shake(float duration, float magnitude)
    {
        // A stronger/longer shake in progress shouldn't get cut short by
        // a smaller one arriving on top of it.
        timeRemaining = Mathf.Max(timeRemaining, duration);
        this.magnitude = Mathf.Max(this.magnitude, magnitude);
    }

    void Update()
    {
        if (timeRemaining > 0f)
        {
            timeRemaining -= Time.deltaTime;
            CurrentOffset = new Vector3(
                Random.Range(-1f, 1f) * magnitude,
                Random.Range(-1f, 1f) * magnitude * 0.5f,
                0f);
            if (timeRemaining <= 0f)
                magnitude = 0f;
        }
        else
        {
            CurrentOffset = Vector3.zero;
        }
    }
}