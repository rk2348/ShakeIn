using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        float x = 0;
        float z = 0;
        
        if(Keyboard.current.wKey.isPressed) z += 1;
        if(Keyboard.current.sKey.isPressed) z -= 1;
        if(Keyboard.current.aKey.isPressed) x -= 1;
        if(Keyboard.current.dKey.isPressed) x += 1;

        Vector3 move = new Vector3(x, 0, z).normalized;
        transform.Translate(move * speed * Time.deltaTime);
    }
}
