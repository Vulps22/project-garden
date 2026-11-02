using System;
using System.Collections;
using Fusion;
using UnityEngine;
using Vulps.GrowAGarden.Crops;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>
    /// The Developer Market's stock and its master half: the developer asks for an item, the master checks the ask
    /// came from the developer, spawns it free (a seed already theirs, a fruit already ripe) and says where it is so
    /// the developer's hand can take it. See docs/dev-tools.md.
    /// </summary>
    public class DeveloperMarket : MonoBehaviour
    {
        private const byte REQUEST_ID = 7;
        private const byte DELIVERED_ID = 8;

        [Tooltip("SceneManager's messenger. Message ids 7-8 on it; ShopKeeper has 3-6.")]
        [SerializeField] private NetworkMessenger _bridge;

        [Tooltip("What the market sells, in display order. Seeds, produce and collectibles.")]
        [SerializeField] private NetworkObject[] _items = new NetworkObject[0];

        [Tooltip("Each item's name in the market, matching _items.")]
        [SerializeField] private string[] _names = new string[0];

        [Tooltip("Each item's picture in the market, matching _items. Textures, not sprites: sprite references arrive empty in-world.")]
        [SerializeField] private Texture2D[] _previews = new Texture2D[0];

        private static DeveloperMarket _instance;

        /// <summary>An item the local developer asked for now exists: its NetworkId, and whether the right hand asked.</summary>
        public static event Action<uint, bool> Delivered;

        public static int Count => _instance == null ? 0 : _instance._items.Length;
        public static string NameOf(int i) => _instance != null && i >= 0 && i < _instance._names.Length ? _instance._names[i] : "?";
        public static Texture2D PreviewOf(int i) => _instance != null && i >= 0 && i < _instance._previews.Length ? _instance._previews[i] : null;

        /// <summary>Asks the master for an item, free, at a place. Only the developer's client asks.</summary>
        public static void Request(int index, Vector3 position, Quaternion rotation, bool rightHand)
        {
            if (_instance == null || !Developer.IsLocal || _instance._bridge == null || !_instance._bridge.CanSend) return;

            string me = PlayerManager.LocalPlayerId ?? string.Empty;
            var writer = new BytesWriter(sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(me) + BytesWriter.IntSize
                                         + BytesWriter.Vector3Size + BytesWriter.QuaternionSize + BytesWriter.ByteSize);
            writer.AddAutoString(me);
            writer.AddInt(index);
            writer.AddVector3(position);
            writer.AddQuaternion(rotation);
            writer.AddByte(rightHand ? (byte)1 : (byte)0);
            _instance._bridge.SendToAll(REQUEST_ID, writer.Data);
            Logger.Info($"Request() 'DeveloperMarket' — asked for '{NameOf(index)}' ({(rightHand ? "right" : "left")} hand)");
        }

        private void Awake() => _instance = this;

        private void Start()
        {
            if (_bridge != null) _bridge.MessageToAll += OnMessageToAll;
        }

        private void OnDestroy()
        {
            if (_bridge != null) _bridge.MessageToAll -= OnMessageToAll;
            if (_instance == this) _instance = null;
            Delivered = null;
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            if (id == REQUEST_ID) OnRequested(data);
            else if (id == DELIVERED_ID) OnDelivered(data);
        }

        /// <summary>Master only. Anyone else's ask, or an ask for nothing, is refused out loud.</summary>
        private void OnRequested(byte[] data)
        {
            if (!PlayerManager.IsMaster) return;

            var reader = new BytesReader(data);
            string requester = reader.NextAutoString();
            int index = reader.NextInt();
            Vector3 position = reader.NextVector3();
            Quaternion rotation = reader.NextQuaternion();
            bool rightHand = reader.NextByte() == 1;

            if (requester != Developer.ID) { Logger.Warn($"OnRequested() '{gameObject.name}' — '{requester}' is not the developer; refused"); return; }
            if (index < 0 || index >= _items.Length || _items[index] == null) { Logger.Warn($"OnRequested() '{gameObject.name}' — no item {index}"); return; }
            if (!WorldManager.CanSpawn) { Logger.Warn($"OnRequested() '{gameObject.name}' — cannot spawn yet"); return; }

            NetworkObject spawned = WorldManager.Spawn(_items[index], position, rotation, $"OnRequested() '{gameObject.name}' {_names[index]}");
            if (spawned == null) return;

            // Free, and ready to use: a seed already the developer's, a fruit already ripe. A collectible needs nothing.
            if (spawned.TryGetComponent(out Seed seed)) seed.Purchase(requester);
            if (spawned.TryGetComponent(out Produce produce))
            {
                float ripen = produce.seedDefinition == null ? 0f : produce.seedDefinition.ripenDuration;
                produce.Init(null, -1, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)Mathf.Ceil(ripen) - 1);
            }
            StartCoroutine(Announce(spawned));

            var writer = new BytesWriter(sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(requester) + BytesWriter.IntSize + BytesWriter.ByteSize);
            writer.AddAutoString(requester);
            writer.AddInt((int)spawned.Id.Raw);
            writer.AddByte(rightHand ? (byte)1 : (byte)0);
            _bridge.SendToAll(DELIVERED_ID, writer.Data);
            Logger.Info($"OnRequested() '{gameObject.name}' — spawned '{_names[index]}' for the developer at {position}");
        }

        /// <summary>Re-sends the new item's state: the spawn reaches other clients a few ticks after the first broadcast.</summary>
        private IEnumerator Announce(NetworkObject spawned)
        {
            foreach (float gap in new[] { 0.25f, 0.75f, 1f })
            {
                yield return new WaitForSeconds(gap);
                if (spawned == null) yield break;
                if (spawned.TryGetComponent(out Seed seed)) seed.broadcastState();
                if (spawned.TryGetComponent(out Produce produce)) produce.broadcastState();
            }
        }

        private void OnDelivered(byte[] data)
        {
            var reader = new BytesReader(data);
            string requester = reader.NextAutoString();
            uint id = (uint)reader.NextInt();
            bool rightHand = reader.NextByte() == 1;
            if (requester == PlayerManager.LocalPlayerId) Delivered?.Invoke(id, rightHand);
        }

        private void OnValidate()
        {
            if (_bridge == null) _bridge = GetComponent<NetworkMessenger>();
        }
    }
}
