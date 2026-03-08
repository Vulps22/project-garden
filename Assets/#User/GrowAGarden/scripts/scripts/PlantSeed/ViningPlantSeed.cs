using UnityEngine;

namespace GrowAGarden
{
    public class ViningPlantSeed : PlantSeed
    {
        [SerializeField] private MeshRenderer _fruitModel;
        [SerializeField] private Collider _fruitCollider;
        [SerializeField] private float _vineDetachRadius = 0.5f;

        private bool _vineAnchored;
        private Vector3 _vineAnchoredWorldPos;
        private Quaternion _vineAnchoredWorldRot;
        private bool _isDecaying;
        private Vector3 _vineInitialLocalPos;
        private Quaternion _vineInitialLocalRot;

        private void Awake()
        {
            _vineInitialLocalPos = _SeedModel.transform.localPosition;
            _vineInitialLocalRot = _SeedModel.transform.localRotation;
        }

        protected override void Start()
        {
            base.Start();
            networkBridge.OnMessageToAll += OnVineMessageToAll;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            networkBridge.OnMessageToAll -= OnVineMessageToAll;
        }

        /// <summary>
        /// Handles vine-specific RPCs: anchoring the vine in world space and starting decay.
        /// </summary>
        private void OnVineMessageToAll(byte id, byte[] data)
        {
            switch ((PlantMessageType)id)
            {
                case PlantMessageType.vineAnchor:
                    BytesReader anchorReader = new BytesReader(data);
                    _vineAnchoredWorldPos = anchorReader.NextVector3();
                    _vineAnchoredWorldRot = anchorReader.NextQuaternion();
                    _vineAnchored = true;
                    break;

                case PlantMessageType.vineDecayStart:
                    BytesReader decayReader = new BytesReader(data);
                    long high = decayReader.NextInt();
                    long low = (uint)decayReader.NextInt();
                    _plantedTimestamp = (high << 32) | low;
                    _growthPhase = 2;
                    _isDecaying = true;
                    break;
            }
        }

        /// <summary>
        /// Scales the vine/_SeedModel (phase 0) or fruit (phase 1). Phase 2 decay is driven by Update.
        /// </summary>
        protected override void OnGrowthUpdated(float completion, float targetScale)
        {
            Logger.Log($"OnGrowthUpdated() '{gameObject.name}' — phase={_growthPhase}, completion={completion:F3}, targetScale={targetScale:F3}, rootScale={transform.localScale}, seedModelScale={_SeedModel.transform.localScale}");
            switch (_growthPhase)
            {
                case 0:
                    _SeedModel.transform.localScale = Vector3.one * targetScale;
                    break;
                case 1:
                    _fruitModel.transform.localScale = Vector3.one * targetScale;
                    break;
            }
        }

        /// <summary>
        /// Phase 0 complete: advance to fruit phase. Phase 1 complete: anchor vine and enable grab.
        /// </summary>
        protected override void OnFullyGrown()
        {
            if (_growthPhase == 0)
            {
                _growthPhase = 1;
                _plantedTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else
            {
                AnchorVine();
                base.OnFullyGrown();
            }
        }

        /// <summary>
        /// Stores vine world position and broadcasts it to all clients so proxies replicate the anchor.
        /// </summary>
        private void AnchorVine()
        {
            _vineAnchoredWorldPos = _SeedModel.transform.position;
            _vineAnchoredWorldRot = _SeedModel.transform.rotation;
            _vineAnchored = true;

            BytesWriter writer = new BytesWriter(BytesWriter.Vector3Size + BytesWriter.QuaternionSize);
            writer.AddVector3(_vineAnchoredWorldPos);
            writer.AddQuaternion(_vineAnchoredWorldRot);
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.vineAnchor, writer.Data);
        }

        /// <summary>
        /// Detects when the PlantSeed root has moved away from the anchored vine (distance check),
        /// then drives vine decay. Runs on all clients once decay starts.
        /// </summary>
        protected override void OnWillUpdate()
        {
            if (_vineAnchored && !_isDecaying)
            {
                if (networkBridge.Object != null && networkBridge.Object.HasStateAuthority)
                {
                    if (Vector3.Distance(transform.position, _vineAnchoredWorldPos) > _vineDetachRadius)
                        StartDecay();
                }
                return;
            }

            if (_isDecaying)
            {
                float completion = GetGrowthCompletion();
                GrowthPhase decayPhase = seedDefinition.phases[_growthPhase];
                float scale = (1f - completion) * decayPhase.maxScale * decayPhase.scaleMultiplier;
                _SeedModel.transform.localScale = Vector3.one * Mathf.Max(0f, scale);

                if (completion >= 1f)
                {
                    _isDecaying = false;
                    _vineAnchored = false;
                    _growthPhase = 0;
                    _SeedModel.transform.localPosition = _vineInitialLocalPos;
                    _SeedModel.transform.localRotation = _vineInitialLocalRot;
                    _SeedModel.transform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Holds the vine at its anchored world position while the PlantSeed root moves freely.
        /// </summary>
        private void LateUpdate()
        {
            if (_vineAnchored)
            {
                _SeedModel.transform.position = _vineAnchoredWorldPos;
                _SeedModel.transform.rotation = _vineAnchoredWorldRot;
            }
        }

        /// <summary>
        /// Authority only. Transitions to decay phase and broadcasts the start timestamp to all clients.
        /// </summary>
        private void StartDecay()
        {
            _growthPhase = 2;
            _plantedTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _isDecaying = true;

            BytesWriter writer = new BytesWriter(2 * BytesWriter.IntSize);
            writer.AddInt((int)(_plantedTimestamp >> 32));
            writer.AddInt((int)(_plantedTimestamp & 0xFFFFFFFFL));
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.vineDecayStart, writer.Data);
        }

        /// <summary>
        /// isSeed=true: vine (_SeedModel) shown at scale 1, fruit hidden.
        /// isSeed=false: vine resets to scale 0 and grows, fruit enabled at scale 0.
        /// If returning to pool mid-decay, vine is left alone so it can finish decaying.
        /// </summary>
        protected override void UpdateVisuals(bool isSeed)
        {
            if (isSeed)
            {
                _SeedCollider.enabled = true;
                _fruitCollider.enabled = false;
                _SeedModel.enabled = true;
                _fruitModel.enabled = false;

                if (!_isDecaying)
                {
                    _growthPhase = 0;
                    _vineAnchored = false;
                    _SeedModel.transform.localPosition = _vineInitialLocalPos;
                    _SeedModel.transform.localRotation = _vineInitialLocalRot;
                    _SeedModel.transform.localScale = Vector3.one;
                }
                // If _isDecaying, vine stays as-is and Update() continues the decay.
            }
            else //become planted
            {
                _growthPhase = 0;
                _vineAnchored = false;
                _isDecaying = false;
                transform.localScale = Vector3.one;
                _SeedCollider.enabled = false;
                _fruitCollider.enabled = true;
                _SeedModel.transform.localPosition = _vineInitialLocalPos;
                _SeedModel.transform.localRotation = _vineInitialLocalRot;
                _SeedModel.transform.localScale = Vector3.zero;
                _fruitModel.enabled = true;
                _fruitModel.transform.localScale = Vector3.zero;
            }
        }
    }
}
