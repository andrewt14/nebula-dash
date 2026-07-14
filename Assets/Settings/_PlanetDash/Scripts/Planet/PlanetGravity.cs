using UnityEngine;


public class PlanetGravity : MonoBehaviour
{
    [Header("Planet Settings")]
    public Transform planetCenter;
    public float gravityStrength = 20f;
    public float rotationSpeed = 5f;


    private Rigidbody rb;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;          // Disable Unity default gravity
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }


    void FixedUpdate()
    {
        // Direction from player to planet center
        Vector3 gravityDir = (planetCenter.position - transform.position).normalized;
        rb.AddForce(gravityDir * gravityStrength, ForceMode.Acceleration);


        // Rotate player to align with planet surface
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, -gravityDir)
                               * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                             rotationSpeed * Time.fixedDeltaTime);
    }
}
