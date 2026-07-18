using UnityEngine;
using System.Collections;

public class PlayerAnimator : MonoBehaviour
{
    public Animator animator;
    public PlayerController playerController;

    [Header("Squash & Stretch")]
    public float recoverSpeed = 9f;

    private static readonly Color DustColor = Color.white;
    private static Material dustMaterial;

    private string currentAnim = "";
    private Vector3 baseScale = Vector3.one;
    private Vector3 juiceScale = Vector3.one;
    private bool wasGrounded = true;
    private bool wasSliding = false;

    void Start()
    {
        if (animator != null)
            baseScale = animator.transform.localScale;
    }

    void Update()
    {
        if (animator == null || playerController == null) return;

        string targetAnim;

        if (!playerController.isAlive)
            targetAnim = "Death";
        else if (playerController.isSliding)
            targetAnim = "Slide";
        else if (!playerController.isGrounded)
            targetAnim = "Jump";
        else
            targetAnim = "Run";

        if (targetAnim != currentAnim)
        {
            animator.CrossFade(targetAnim, 0.1f);
            currentAnim = targetAnim;
        }

        ApplySquashStretch();
        HandleDust();
    }

    // Cartoon-style squash on landing and stretch on takeoff, applied
    // as a multiplier over the rig's authored scale and eased back to
    // normal, so the run reads as bouncy instead of stiff.
    void ApplySquashStretch()
    {
        if (playerController.isAlive)
        {
            bool grounded = playerController.isGrounded;

            if (grounded && !wasGrounded)
            {
                juiceScale = new Vector3(1.18f, 0.75f, 1.18f);
                SpawnLandDust();
            }
            else if (!grounded && wasGrounded)
            {
                juiceScale = new Vector3(0.85f, 1.2f, 0.85f);
            }

            wasGrounded = grounded;
        }

        juiceScale = Vector3.Lerp(
            juiceScale, Vector3.one,
            recoverSpeed * Time.deltaTime);
        animator.transform.localScale =
            Vector3.Scale(baseScale, juiceScale);
    }

    void HandleDust()
    {
        if (playerController.isAlive &&
            playerController.isSliding && !wasSliding)
        {
            SpawnSlideDust();
        }
        wasSliding = playerController.isSliding;
    }

    // Expanding ring of dust at the feet on touchdown, pairing with
    // the landing squash.
    void SpawnLandDust()
    {
        Vector3 feet = playerController.transform.position;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f;
            Vector3 dir = new Vector3(
                Mathf.Cos(angle), 0.05f, Mathf.Sin(angle));
            SpawnDustPuff(feet, dir, 3.5f, 0.1f, 0.4f);
        }
    }

    // Kicked-up spray behind the player when a slide starts.
    void SpawnSlideDust()
    {
        Vector3 feet = playerController.transform.position;
        for (int i = 0; i < 6; i++)
        {
            Vector3 dir = (Vector3.back +
                Vector3.up * Random.Range(0.2f, 0.6f) +
                Vector3.right * Random.Range(-0.4f, 0.4f))
                .normalized;
            SpawnDustPuff(feet, dir, 2.5f, 0.18f, 0.5f);
        }
    }

    void SpawnDustPuff(Vector3 origin, Vector3 dir,
                       float speed, float size, float duration)
    {
        GameObject puff = GameObject.CreatePrimitive(
            PrimitiveType.Sphere);
        puff.transform.position = origin + Vector3.up * 0.1f;
        puff.transform.localScale = Vector3.one * size;
        Destroy(puff.GetComponent<Collider>());

        Renderer r = puff.GetComponent<Renderer>();
        if (dustMaterial == null)
        {
            dustMaterial = r.material;
            dustMaterial.color = DustColor;
        }
        else
        {
            r.sharedMaterial = dustMaterial;
        }

        StartCoroutine(DustPuffMotion(puff, dir, speed, size, duration));
        // Failsafe in case this component is destroyed mid-effect.
        Destroy(puff, duration + 0.1f);
    }

    IEnumerator DustPuffMotion(GameObject puff, Vector3 dir,
                               float speed, float size, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            if (puff == null) yield break;
            puff.transform.position += dir * speed * Time.deltaTime;
            puff.transform.localScale = Vector3.one *
                Mathf.Lerp(size, 0f, timer / duration);
            yield return null;
        }
    }
}
