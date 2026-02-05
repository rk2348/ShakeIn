using UnityEngine;

public class RespawnPoint : MonoBehaviour
{
    public float deadLineY = -2.5f;

    public Transform respawnPosition;

    void Update()
    {
        if(transform.position.y < deadLineY)
        {
            Respawn();
        }
    }

    void Respawn()
    {
        transform.position = respawnPosition.position;
    }

}
