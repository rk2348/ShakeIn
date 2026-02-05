using UnityEngine;
using UnityEngine.InputSystem;
public class RespawnPoint : MonoBehaviour
{
    public float deadLineY = -2.5f;
    public Transform respawnPosition;
    public int maxRespawn = 2;

    int respawnCount = 0;
    void Update()
    {
        if(Keyboard.current.enterKey.wasPressedThisFrame)
        {
            HandlRespawn();
        }
    }

    void HandlRespawn()
    {
        respawnCount++;

        if(respawnCount > maxRespawn)
        {
            gameObject.SetActive(false);
            return;
        }
        Rigidbody rb = GetComponent<Rigidbody>();
        if(rb != null)
        {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        }
        
        transform.position = respawnPosition.position;
        
    }

}
