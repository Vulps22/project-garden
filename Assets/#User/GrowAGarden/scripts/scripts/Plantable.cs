using Fusion;
using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public abstract class Plantable : MonoBehaviour
    {
        public float growDurationSeconds;
        public float maxScale;
        public float scaleMultiplier = 1f;

        private long _plantedTimestamp;
        [SerializeField] private XRGrabInteractable _grabInteractable;
        [SerializeField] public NetworkObject networkObject;

        private void Start()
        {
            Logger.Info($"Start() '{gameObject.name}' — growDuration={growDurationSeconds}s, maxScale={maxScale}, scaleMultiplier={scaleMultiplier}, grabInteractable={(_grabInteractable != null ? _grabInteractable.name : "NULL")}");
            ResetForPool();
        }

        public void ResetForPool()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.enabled = false;
                Logger.Log($"ResetForPool() '{gameObject.name}' — grab interactable disabled");
            }
            else
            {
                Logger.Warn($"ResetForPool() '{gameObject.name}' — _grabInteractable is NULL, harvesting via grab won't work!");
            }
            _lastLoggedCompletion = -1f;
        }

        public void OnPlanted()
        {
            _plantedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Logger.Info($"OnPlanted() '{gameObject.name}' — timestamp={_plantedTimestamp}, growDuration={growDurationSeconds}s, maxScale={maxScale}, scaleMultiplier={scaleMultiplier}");

            if (growDurationSeconds <= 0f)
                Logger.Error($"OnPlanted() '{gameObject.name}' — growDurationSeconds={growDurationSeconds}, plant will NEVER grow (division by zero/negative)!");
            if (maxScale <= 0f)
                Logger.Warn($"OnPlanted() '{gameObject.name}' — maxScale={maxScale}, plant will be invisible!");
            if (scaleMultiplier <= 0f)
                Logger.Warn($"OnPlanted() '{gameObject.name}' — scaleMultiplier={scaleMultiplier}, plant will be invisible!");

            UpdateVisual(0f);
        }

        public void OnLoaded(long savedTimestamp)
        {
            _plantedTimestamp = savedTimestamp;
            float completion = GetGrowthCompletion();
            Logger.Info($"OnLoaded() '{gameObject.name}' — savedTimestamp={savedTimestamp}, growDuration={growDurationSeconds}s, maxScale={maxScale}, scaleMultiplier={scaleMultiplier}, completion={completion:F3}");

            if (growDurationSeconds <= 0f)
                Logger.Error($"OnLoaded() '{gameObject.name}' — growDurationSeconds={growDurationSeconds}, plant will NEVER grow!");
            if (maxScale <= 0f)
                Logger.Warn($"OnLoaded() '{gameObject.name}' — maxScale={maxScale}, plant will be invisible!");

            UpdateVisual(completion);
        }

        public float GetGrowthCompletion()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float raw = (now - _plantedTimestamp) / growDurationSeconds;
            return Mathf.Clamp01(raw);
        }

        public bool IsReadyToHarvest()
        {
            return GetGrowthCompletion() >= 1f;
        }

        private float _lastLoggedCompletion = -1f;

        private void Update()
        {
            if (networkObject.HasStateAuthority)
            {

                float completion = GetGrowthCompletion();

                if (Mathf.Abs(completion - _lastLoggedCompletion) >= 0.1f)
                {
                    Logger.Log($"Update() '{gameObject.name}' — completion={completion:F3}, scale={transform.localScale}, isReady={IsReadyToHarvest()}");
                    _lastLoggedCompletion = completion;
                }

                UpdateVisual(completion);
            }

            if (IsReadyToHarvest() && _grabInteractable != null && !_grabInteractable.enabled)
            {
                _grabInteractable.enabled = true;
                Logger.Info($"Update() '{gameObject.name}' — READY TO HARVEST, grab interactable enabled");
            }
        }

        private void UpdateVisual(float completion)
        {
            float scale = completion * maxScale * scaleMultiplier;
            if (scale <= 0f && completion > 0f)
                Logger.Warn($"UpdateVisual() '{gameObject.name}' — scale={scale} despite completion={completion:F3}! maxScale={maxScale}, scaleMultiplier={scaleMultiplier}");
            transform.localScale = Vector3.one * scale;
        }

        public abstract void OnHarvested();
    }
}
