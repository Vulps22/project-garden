using System.Linq;
using UnityEngine;

namespace GrowAGarden
{
    public class ARSDJLasers : MonoBehaviour
    {
        [SerializeField] private Transform _lasersRoot;
        [SerializeField] private Transform[] _lasers;
        [SerializeField] private float _speedH0 = 1.0f;
        [SerializeField] private float _speedH1 = 2.0f;
        [SerializeField] private float _speedV0 = 0.66f;

        [SerializeField] private Vector2 _scanRangeH = new Vector2(-30.0f, 30.0f);
        [SerializeField] private Vector2 _laserWidthRange = new Vector2(5.0f, 90.0f);
        [SerializeField] private Vector2 _scanRangeV = new Vector2(-20.0f, 20.0f);

        private ARSController _ars;

        void Start()
        {
            // Check if any laser are null
            if (_lasers.Any(t => t == null))
            { 
                Debug.LogError($"[{nameof(ARSDJLasers)}] One or more lasers are not assigned, disabling component");
                enabled = false;
                return;
            }

            _ars = ARSController.Instance;
            if (_ars == null)
            {
                ARSController.ForceInit();
            }
            _ars.OnUpdate += OnUpdate;
        }

        [ContextMenu("#Editor : Force Update")]
        private void OnUpdate()
        {
            int laserCount = _lasers.Length;
            int spacesCount = laserCount - 1;

            float scanAngleH = Mathf.Lerp(_scanRangeH.x, _scanRangeH.y, Mathf.Sin(Time.time * _speedH0) * 0.5f + 0.5f);
            float laserWidthAngle = Mathf.Lerp(_laserWidthRange.x, _laserWidthRange.y, Mathf.Sin(Time.time * _speedH1) * 0.5f + 0.5f);
            float a3 = Mathf.Lerp(_scanRangeV.x, _scanRangeV.y, Mathf.Sin(Time.time * _speedV0) * 0.5f + 0.5f);

            float angleHMin = scanAngleH - (laserWidthAngle * 0.5f);
            float angleHStep = laserWidthAngle / spacesCount;

            // Split lasers over the range
            for (int n = 0; n < laserCount; n++)
            {
                float angle = (angleHStep * n) + angleHMin;
                _lasers[n].localRotation = Quaternion.Euler(a3, angle, 0.0f);
            }
        }

        // Cleanup events
        private void OnDestroy()
        {
            if (_ars != null)
            {
                _ars.OnUpdate -= OnUpdate;
            }
        }
    }
}
