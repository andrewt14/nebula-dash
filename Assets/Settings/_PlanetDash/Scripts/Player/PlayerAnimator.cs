using UnityEngine;
using System.Collections;

public class PlayerAnimator : MonoBehaviour
{
    public Animator animator;
    public PlayerController playerController;

    [Header("Squash & Stretch")]
    public float recoverSpeed = 9f;

    // Run speed at which the animator plays at its authored 1x rate; higher
    // runSpeed scales the run cycle up proportionally (see Update).
    public float baseRunSpeed = 16f;
    // Run speed at which the animator hits maxAnimSpeed.
    //
    // This character has NO root motion (Animator.hasRootMotion/
    // applyRootMotion are both false, confirmed directly, not assumed) —
    // PlayerController.Move() drives 100% of the actual translation via
    // CharacterController, completely decoupled from the run clip. That
    // means animator.speed has no relationship to ground distance covered
    // per stride; it's purely "play the same loop faster in time." Cranking
    // maxAnimSpeed up (a prior pass tried 2.6, up from 1.8) to chase a
    // 15x runSpeed range (16->240) just made the mismatch worse in BOTH
    // directions at once: at moderate speed the legs cycle far faster than
    // the implied stride length justifies (reads as gliding — "the ground
    // running against the character" — because a real stride can't
    // plausibly cover that much ground that fast), and the sqrt easing
    // front-loads that rise early, then the ceiling is still reached long
    // before runSpeed's real top end, so it goes right back to reading as
    // stationary late-run anyway. There is no animator.speed curve that
    // fixes this — without root motion, no multiplier makes the legs
    // stride-match a variable world speed.
    //
    // Matches how this genre actually handles it (Temple Run, Subway
    // Surfers): the character's run cycle stays close to a constant,
    // natural jogging rate for the whole run — "faster" is sold by the
    // world (ground/obstacles closing distance quicker, which already
    // happens automatically since runSpeed drives real translation) and by
    // SpeedLines' emission rate (see SpeedLines.cs), not by the legs. Kept
    // a mild rise so the character doesn't feel completely disconnected
    // from runSpeed changing, but nowhere near proportional.
    //
    // Was 220 with a sqrt-eased Lerp — sqrt(t) rises steeply for small t,
    // so animSpeed was already ~90% of the way to maxAnimSpeed by
    // runSpeed~110-120 (score ~2000-3500 on the real difficulty curve),
    // then sat essentially flat for the rest of the run while runSpeed
    // kept climbing toward 240 — reported back as "fine until ~3500, then
    // the same stationary bug." Switched to a LINEAR ramp, which helped but
    // didn't fix it: even linear, this field can only ever pull animSpeed up
    // to maxAnimSpeed (1.3x) while runSpeed itself kept growing to 13x
    // baseline — a gap that grows forever with playtime and always
    // eventually reads as "stationary" again at a high enough score. Fixed
    // at the source instead (see DifficultyManager.maxRunSpeed) by
    // compressing runSpeed's own growth range from 240 down to a range
    // this curve can actually sell — 60, then 75, then 85, then explicitly
    // asked for a bigger ramp without the bug regressing: 110, paired with
    // maxAnimSpeed nudged 1.3->1.5 for extra divergence headroom (see
    // DifficultyManager.maxRunSpeed's comment for the verified numbers).
    // Must stay equal to DifficultyManager.maxRunSpeed.
    public float topRunSpeedForAnim = 110f;
    // Separate, slightly longer blend window just for entering/leaving
    // Flying — the Flying clip drives zero bones on this rig (see
    // JetpackEffect's class comment), so this is purely blending the
    // Animator layer's crossfade weight, not actual bone poses.
    public float flyBlendDuration = 0.2f;
    // Nudged from 1.3 alongside the 85->110 runSpeed cap increase — the
    // overall speed range is now controlled enough (was a 20x range at
    // the old 240 cap, is a ~9x range now) that a modest bump here is
    // safe headroom against divergence, nowhere near the 2.6 that was
    // rejected as "legs moving so fast... retarded" back when the range
    // was much wider.
    public float maxAnimSpeed = 1.5f;

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
        // A real dedicated clip (Flying.fbx) rather than freezing the run
        // cycle mid-stride — that first pass stopped it from reading as
        // "running in the air" but held a static frozen pose the whole
        // flight, not an actual flying animation. Takes priority over
        // every state except Death (dying mid-flight should still show
        // Death, not keep flying) — the real gameplay hitbox stays
        // grounded throughout (see JetpackEffect's own comment on why),
        // so isSliding/isGrounded would otherwise still resolve normally.
        bool flying = JetpackEffect.Instance != null && JetpackEffect.Instance.IsActive;

        if (!playerController.isAlive)
            targetAnim = "Death";
        else if (flying)
            targetAnim = "Flying";
        else if (playerController.isSliding)
            targetAnim = "Slide";
        else if (!playerController.isGrounded)
            targetAnim = "Jump";
        else
            targetAnim = "Run";

        if (targetAnim != currentAnim)
        {
            bool enteringOrLeavingFlight =
                targetAnim == "Flying" || currentAnim == "Flying";
            animator.CrossFade(targetAnim,
                enteringOrLeavingFlight ? flyBlendDuration : 0.1f);
            currentAnim = targetAnim;
        }

        // Scale playback rate with run speed so the character keeps
        // (mildly) reading as "running harder" for the WHOLE run instead
        // of looking stationary. Straight linear ramp across the full
        // base->top speed range — sqrt easing was tried and rejected (see
        // topRunSpeedForAnim comment): it front-loads the rise into early/
        // mid-game and then sits flat for the long back half of a run.
        // maxAnimSpeed itself is intentionally low (see above) — this is
        // not the system that's supposed to sell speed. Only the run
        // cycle scales; jump/slide/flying/death play at 1x.
        if (targetAnim == "Run")
        {
            float t = Mathf.InverseLerp(
                baseRunSpeed, topRunSpeedForAnim, playerController.runSpeed);
            animator.speed = Mathf.Lerp(1f, maxAnimSpeed, Mathf.Clamp01(t));
        }
        else
            animator.speed = 1f;

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
