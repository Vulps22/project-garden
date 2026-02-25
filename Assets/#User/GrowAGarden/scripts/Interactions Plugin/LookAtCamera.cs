using UnityEngine;

namespace GrowAGarden
{
    public class LookAtCamera : MonoBehaviour
    {
        void Update()
        {
            Camera camera = Camera.main;
            if (camera != null)
            { 
                transform.LookAt(camera.transform.position, Vector3.up);
                transform.Rotate(0, 180, 0);
            }
        }
    }
}
