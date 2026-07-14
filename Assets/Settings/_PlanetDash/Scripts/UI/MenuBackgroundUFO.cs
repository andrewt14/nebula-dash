using UnityEngine;

// Purely decorative — drifts slowly across the main menu background,
// bobbing and spinning gently, then loops back around once it's passed
// out of view. No gameplay logic (unlike UFOObstacle, which expects a
// live "Player" and PlayerController and would throw in this scene).
public class MenuBackgroundUFO : MonoBehaviour
{
    public float driftSpeed = 1.2f;
    public float bobSpeed = 0.6f;
    public float bobAmount = 0.4f;
    public float spinSpeed = 8f;
    public float loopWidth = 30f; // once it drifts this far past center, wrap to the other side

    private float startY;
    private Vector3 startPos;

    void Start()
    {
        startY = transform.position.y;
        startPos = transform.position;
    }

    void Update()
    {
        transform.position += Vector3.right * driftSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

        float bobY = startY + Mathf.Sin(Time.time * bobSpeed + startPos.x) * bobAmount;
        Vector3 p = transform.position;
        p.y = bobY;
        transform.position = p;

        if (transform.position.x > startPos.x + loopWidth)
        {
            Vector3 reset = transform.position;
            reset.x -= loopWidth * 2f;
            transform.position = reset;
        }
    }
}
