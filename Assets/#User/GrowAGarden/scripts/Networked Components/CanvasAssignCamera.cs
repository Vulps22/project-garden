using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// This component is used to assign a camera to a Canvas, this is necesarry in VR for canvas to work properly
    /// </summary>
    public class CanvasAssignCamera : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;

        void Start()
        {
            if(_canvas != null)
                InvokeRepeating(nameof(WaitForCameraLoop), 0.5f, 0.5f);
        }

        private void WaitForCameraLoop()
        {
            if (Camera.main != null)
            {
                _canvas.worldCamera = Camera.main;
                CancelInvoke(nameof(WaitForCameraLoop));
            }
        }

        private void OnValidate()
        {
            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }
        }
    }
}
