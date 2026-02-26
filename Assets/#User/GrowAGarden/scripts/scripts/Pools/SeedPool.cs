using System.Collections;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class SeedPool : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;
        private const float AUTHORITY_TIMEOUT = 2f;

        public string SeedId => _seedDefinition.seedId;
        public int Available => GetComponentsInChildren<SeedObject>(true).Length;

        private void Awake()
        {
            var seeds = GetComponentsInChildren<SeedObject>(true);
            Logger.Info($"Awake() '{gameObject.name}' — seedId='{SeedId}', found {seeds.Length} seed(s) to restore");
            foreach (SeedObject seed in seeds)
                Restore(seed);
        }

        public SeedObject Claim()
        {
            SeedObject seed = GetComponentInChildren<SeedObject>();
            if (seed == null)
            {
                Logger.Warn($"Claim() — pool empty for '{SeedId}'!");
                return null;
            }
            Logger.Log($"Claim() '{SeedId}' — claiming '{seed.name}', remaining after={Available - 1}");
            seed.transform.SetParent(null);
            return seed;
        }

        public void Return(SeedObject seed)
        {
            Logger.Log($"Return() '{SeedId}' — returning '{seed?.name}'");
            Restore(seed);
        }

        private void Restore(SeedObject seed)
        {
            NetworkBridge seedBridge = seed.GetComponent<NetworkBridge>();
            bool hasAuthority = seedBridge != null && seedBridge.Object != null && seedBridge.Object.HasStateAuthority;

            Logger.Info($"Restore() '{SeedId}' — seed='{seed.name}', hasBridge={seedBridge != null}, hasAuthority={hasAuthority}, IsMasterClient={SceneNetworking.IsMasterClient}");

            if (hasAuthority)
            {
                Logger.Log($"Restore() '{SeedId}' — have authority, moving '{seed.name}' to pool immediately");
                seed.transform.SetParent(transform);
                seed.transform.position = transform.position;
                seed.transform.rotation = transform.rotation;
            }

            if (SceneNetworking.IsMasterClient)
            {
                if (seedBridge != null && seedBridge.Object != null)
                {
                    if (!seedBridge.Object.HasStateAuthority)
                    {
                        Logger.Info($"Restore() '{SeedId}' — requesting authority on '{seed.name}', will wait up to {AUTHORITY_TIMEOUT}s");
                        seedBridge.Object.RequestStateAuthority();
                        StartCoroutine(WaitForAuthorityAndFinishRestore(seed, seedBridge));
                    }
                    else
                    {
                        Logger.Log($"Restore() '{SeedId}' — already have authority, finishing restore immediately");
                        FinishRestore(seed);
                    }
                }
                else
                {
                    Logger.Log($"Restore() '{SeedId}' — no NetworkBridge on seed, finishing restore immediately");
                    FinishRestore(seed);
                }
            }
            else
            {
                Logger.Log($"Restore() '{SeedId}' — not master, skipping FinishRestore");
            }
        }

        private IEnumerator WaitForAuthorityAndFinishRestore(SeedObject seed, NetworkBridge seedBridge)
        {
            float startTime = Time.time;
            Logger.Log($"WaitForAuthority() '{SeedId}' — waiting for authority on '{seed.name}'");

            while (!seedBridge.Object.HasStateAuthority)
            {
                if (Time.time - startTime > AUTHORITY_TIMEOUT)
                {
                    Logger.Warn($"WaitForAuthority() '{SeedId}' — TIMEOUT after {AUTHORITY_TIMEOUT}s waiting for authority on '{seed.name}'");
                    yield break;
                }
                yield return null;
            }

            float elapsed = Time.time - startTime;
            Logger.Info($"WaitForAuthority() '{SeedId}' — got authority on '{seed.name}' after {elapsed:F2}s");
            FinishRestore(seed);
        }

        private void FinishRestore(SeedObject seed)
        {
            Logger.Log($"FinishRestore() '{SeedId}' — finalising '{seed.name}'");

            var grab = seed.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = false;

            var rb = seed.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            seed.transform.SetParent(transform);
            seed.transform.position = transform.position;
            seed.transform.rotation = transform.rotation;

            Logger.Info($"FinishRestore() '{SeedId}' — '{seed.name}' restored to pool at {transform.position}");
        }
    }
}
