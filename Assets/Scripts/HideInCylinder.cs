using UnityEngine;

public class HideInCylinder : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Renderer r = other.GetComponent<Renderer>();
        if(r != null)
        {
            r.enabled = false;
        }
    }

    void OnTriggerExit(Collider other)
    {
        Renderer r = other.GetComponent<Renderer>();
        if(r != null)
        {
            r.enabled = true;
        }   
    }

}
