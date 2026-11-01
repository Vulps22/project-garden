using UnityEngine;

namespace Vulps.GrowAGarden.Islands
{
    /// <summary>A place on an island where a buy point could stand. The world seed decides whether one does; the stall stands exactly here.</summary>
    public class StallSocket : MonoBehaviour
    {
        /// <summary>The buy point standing here, or null when the seed left this socket empty.</summary>
        public BuyPoint Stall { get; private set; }

        /// <summary>Puts a buy point on this socket, favouring rare stock by a rarity from 1 to 100.</summary>
        public void Open(BuyPoint prefab, int rarity)
        {
            if (Stall != null || prefab == null) return;
            Stall = Instantiate(prefab, transform.position, transform.rotation, transform);
            Stall.SetRarityMultiplier(rarity);
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(0.9f, 0.72f, 0.8f));
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.9f, 0.72f, 0.8f));
            Gizmos.DrawLine(Vector3.zero, new Vector3(0f, 0f, -0.7f));   // the side the seed sits on
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
