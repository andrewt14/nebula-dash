using UnityEngine;

public class LaneLineFollower : MonoBehaviour
{
    public Transform player;

    void Update()
    {
        if (player == null) return;
        transform.position = new Vector3(
            transform.position.x,
            transform.position.y,
            player.position.z + 250f
        );
    }
}