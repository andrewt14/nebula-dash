using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    public float rotationSpeed = 0.25f;

    void Update()
    {
        RenderSettings.skybox.SetFloat(
            "_Rotation", Time.time * rotationSpeed);
    }
}