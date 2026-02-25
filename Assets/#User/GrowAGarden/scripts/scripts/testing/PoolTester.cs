using UnityEngine;

namespace GrowAGarden
{
    public class PoolTester : MonoBehaviour
    {
        [SerializeField] private string _seedId = "carrot";

        private void Start()
        {
            SeedObject seed = PoolManager.Instance.ClaimSeed(_seedId);
            if (seed == null)
            {
                Debug.Log($"[PoolTester] No seeds available for '{_seedId}'");
                return;
            }
            seed.transform.position = new Vector3(0, 5, 0);
            Debug.Log($"[PoolTester] Claimed seed '{_seedId}' � moved to (0,5,0)");
        }
    }
}