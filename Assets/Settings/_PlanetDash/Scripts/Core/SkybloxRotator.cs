using UnityEngine;

// The gameplay skybox (Skybox4.mat) uses the built-in Skybox/6 Sided
// shader — six separate flat face textures with no shared border pixels,
// not a seamless cubemap. That shader has a known seam artifact where
// filtering samples slightly across a face boundary; normally that seam
// just sits in one fixed, easy-to-miss spot. Continuously animating
// _Rotation swept that seam around the whole sky instead, which is what
// read as "a weird vertical line that occasionally shows up" — it's the
// same seam, periodically rotating back into view. Root fix would be
// baking the six faces into one seamless Cubemap asset and switching to
// Skybox/Cubemap, which handles rotation without a hard face boundary;
// until then, holding rotation still keeps the seam parked out of sight
// instead of sweeping it through frame on a cycle.
public class SkyboxRotator : MonoBehaviour
{
    public float rotationSpeed = 0f;

    void Update()
    {
        if (rotationSpeed == 0f) return;
        RenderSettings.skybox.SetFloat(
            "_Rotation", Time.time * rotationSpeed);
    }
}