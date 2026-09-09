using UnityEngine;

namespace GrowAGarden
{
    public class FlightTutorialTrigger : TutorialTrigger
    {
        [SerializeField] private FlightController _flightController;
        [SerializeField] private float _durationBeforeTroughShot = 10f;
        [SerializeField] private float _durationBeforeWrapUp = 30f;

        private bool _flightStarted = false;
        private bool _troughShotTriggered = false;
        private bool _wrapUpTriggered = false;
        private float _flightTime = 0f;

        private void OnEnable()
        {
            base.OnEnable();
            if (_flightController == null)
                _flightController = FindObjectOfType<FlightController>();
        }

        private void Update()
        {
            if (_flightController == null || !_flightController.enabled)
                return;

            var playerRoot = SomniumSpace.Bridge.Player.SomniumPlayer.LocalPlayer?.Root;
            if (playerRoot == null)
                return;

            bool isFlying = DetectFlight(playerRoot);

            if (isFlying && !_flightStarted)
            {
                _flightStarted = true;
                _flightTime = 0f;
                TriggerStep(6);
            }
            else if (!isFlying && _flightStarted)
            {
                _flightStarted = false;
                _troughShotTriggered = false;
                _wrapUpTriggered = false;
            }
            else if (isFlying)
            {
                _flightTime += Time.deltaTime;

                if (!_troughShotTriggered && _flightTime >= _durationBeforeTroughShot)
                {
                    _troughShotTriggered = true;
                    TriggerStep(8);
                }

                if (!_wrapUpTriggered && _flightTime >= _durationBeforeWrapUp)
                {
                    _wrapUpTriggered = true;
                    TriggerStep(9);
                }
            }
        }

        private bool DetectFlight(Transform playerRoot)
        {
            var input = GetLocalHandPositions();
            if (input == null)
                return false;

            float armExtensionThreshold = 0.55f;
            return Vector3.Distance(input.Value.leftHand, playerRoot.position) > armExtensionThreshold &&
                   Vector3.Distance(input.Value.rightHand, playerRoot.position) > armExtensionThreshold;
        }

        private (Vector3 leftHand, Vector3 rightHand)? GetLocalHandPositions()
        {
            var leftHand = InputTracking.GetLocalPosition(XRNode.LeftHand);
            var rightHand = InputTracking.GetLocalPosition(XRNode.RightHand);

            if (leftHand == Vector3.zero || rightHand == Vector3.zero)
                return null;

            return (leftHand, rightHand);
        }
    }
}
