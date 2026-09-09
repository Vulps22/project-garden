using UnityEngine;

namespace GrowAGarden
{
    public abstract class TutorialTrigger : MonoBehaviour
    {
        [SerializeField] protected TutorialManager _tutorialManager;

        protected virtual void OnEnable()
        {
            if (_tutorialManager == null)
                _tutorialManager = TutorialManager.Instance;
        }

        protected void TriggerStep(int step)
        {
            if (_tutorialManager != null)
                _tutorialManager.TriggerStep(step);
        }

        protected void TriggerTip(string tipKey)
        {
            if (_tutorialManager != null)
                _tutorialManager.TriggerTip(tipKey);
        }
    }

    public class OnStartTutorialTrigger : TutorialTrigger
    {
        [SerializeField] private int _step;

        private void Start()
        {
            TriggerStep(_step);
        }
    }

    public class OnEnableTutorialTrigger : TutorialTrigger
    {
        [SerializeField] private int _step;

        protected override void OnEnable()
        {
            base.OnEnable();
            TriggerStep(_step);
        }
    }

    public class OnFirstHutEnterTutorialTrigger : TutorialTrigger
    {
        private bool _triggered = false;

        public void OnHutEntered()
        {
            if (!_triggered)
            {
                _triggered = true;
                TriggerStep(1);
            }
        }
    }

    public class OnPlantPlacedTutorialTrigger : TutorialTrigger
    {
        private bool _triggered = false;

        public void OnPlantPlaced()
        {
            if (!_triggered)
            {
                _triggered = true;
                TriggerStep(2);
            }
        }
    }

    public class OnProduceHarvestedTutorialTrigger : TutorialTrigger
    {
        private bool _triggered = false;

        public void OnProduceSpawned()
        {
            if (!_triggered)
            {
                _triggered = true;
                TriggerStep(3);
            }
        }
    }

    public class OnFirstSaleTutorialTrigger : TutorialTrigger
    {
        private bool _triggered = false;

        public void OnSaleCompleted()
        {
            if (!_triggered)
            {
                _triggered = true;
                TriggerStep(4);
            }
        }
    }

    public class OnBalanceThresholdTutorialTrigger : TutorialTrigger
    {
        [SerializeField] private int _balanceThreshold = 100;
        private bool _triggered = false;

        private void OnEnable()
        {
            base.OnEnable();
            EconomyManager.OnPlayerBalanceChanged += CheckBalance;
        }

        private void OnDisable()
        {
            if (EconomyManager.Instance != null)
                EconomyManager.OnPlayerBalanceChanged -= CheckBalance;
        }

        private void CheckBalance(int oldBalance, int newBalance)
        {
            if (!_triggered && newBalance >= _balanceThreshold)
            {
                _triggered = true;
                TriggerStep(5);
            }
        }
    }

    public class OnFallTutorialTrigger : TutorialTrigger
    {
        [SerializeField] private float _fallThreshold = -50f;
        private bool _falling = false;

        private void Update()
        {
            if (TutorialManager.Instance == null)
                return;

            var playerRoot = SomniumSpace.Bridge.Player.SomniumPlayer.LocalPlayer?.Root;
            if (playerRoot == null)
                return;

            bool isBelowThreshold = playerRoot.position.y < _fallThreshold;

            if (isBelowThreshold && !_falling)
            {
                _falling = true;
                TriggerTip("falling");
            }
            else if (!isBelowThreshold && _falling)
            {
                _falling = false;
            }
        }
    }

    public class OnUpgradeFoundTutorialTrigger : TutorialTrigger
    {
        private bool _triggered = false;

        public void OnUpgradeCollected()
        {
            if (!_triggered)
            {
                _triggered = true;
                TriggerTip("upgrades");
            }
        }
    }

    public class OnBuffFoundTutorialTrigger : TutorialTrigger
    {
        private bool _triggered = false;

        public void OnBuffCollected()
        {
            if (!_triggered)
            {
                _triggered = true;
                TriggerTip("buffing");
            }
        }
    }

    public class OnPlayerAddedToPlotTutorialTrigger : TutorialTrigger
    {
        private bool _triggered = false;

        public void OnPlayerAdded()
        {
            if (!_triggered)
            {
                _triggered = true;
                TriggerTip("teams");
            }
        }
    }
}
