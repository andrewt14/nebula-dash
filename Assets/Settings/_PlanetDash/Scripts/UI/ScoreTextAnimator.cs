using UnityEngine;
using TMPro;

public class ScoreTextAnimator : MonoBehaviour
{
    private TextMeshProUGUI text;
    private Vector3 startPos;
    private float floatSpeed = 1.5f;
    private float floatAmount = 3f;

    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
        startPos = transform.localPosition;
    }

    void Update()
    {
        // Subtle up and down float
        transform.localPosition = startPos + 
            Vector3.up * Mathf.Sin(
                Time.time * floatSpeed) * floatAmount;
    }
}