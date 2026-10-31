using System;
using System.Collections;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Islands
{
    /// <summary>The world seed: decided once by the first master, announced to every peer, and kept across master changes.</summary>
    public class WorldSeed : MonoBehaviour
    {
        private const byte MESSAGE_ID = 1;
        private const long SECONDS_PER_DAY = 86400;
        private const float EDITOR_FALLBACK_SECONDS = 5f;

        [Tooltip("SceneManager's messenger. Message id 1 on it; EconomyManager uses 0.")]
        [SerializeField] private NetworkMessenger _bridge;

        /// <summary>The seed, once known on this client.</summary>
        public static long Seed { get; private set; }

        /// <summary>Whether this client has a seed yet.</summary>
        public static bool HasSeed { get; private set; }

        /// <summary>Raised on every client the first time it learns the seed.</summary>
        public static event Action<long> SeedKnown;

        private void Awake()
        {
            Seed = 0;
            HasSeed = false;
            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;
        }

        private void Start()
        {
            _bridge.MessageToAll += OnMessageToAll;
            PlayerManager.OtherPlayerJoined += OnOtherPlayerJoined;
            PlayerManager.BecameWorldMaster += OnBecameWorldMaster;
            if (Application.isEditor) StartCoroutine(EditorFallback());
        }

        /// <summary>Editor only: with no Somnium client nobody joins as master, so take today's seed locally after a short wait.</summary>
        private IEnumerator EditorFallback()
        {
            yield return new WaitForSeconds(EDITOR_FALLBACK_SECONDS);
            if (HasSeed) yield break;
            Learn(TodaysMidnight(), "taken locally (Editor, no master joined)");
        }

        private static long TodaysMidnight()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now - now % SECONDS_PER_DAY;
        }

        private void OnDestroy()
        {
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
            if (_bridge != null) _bridge.MessageToAll -= OnMessageToAll;
            PlayerManager.OtherPlayerJoined -= OnOtherPlayerJoined;
            PlayerManager.BecameWorldMaster -= OnBecameWorldMaster;
        }

        private void OnLocalPlayerJoined(string playerId, string playerName) => DecideIfFirstMaster();

        private void OnOtherPlayerJoined() => Announce();

        private void OnBecameWorldMaster() => StartCoroutine(AnnounceNextFrame());

        private IEnumerator AnnounceNextFrame()
        {
            yield return null;
            DecideIfFirstMaster();
            Announce();
        }

        /// <summary>Master only: takes today's UTC midnight as the seed, unless one was already received.</summary>
        private void DecideIfFirstMaster()
        {
            if (!PlayerManager.IsMaster) return;
            if (HasSeed) return;

            Learn(TodaysMidnight(), "decided");
            Announce();
        }

        /// <summary>Master only: sends the seed to every client.</summary>
        private void Announce()
        {
            if (!PlayerManager.IsMaster) return;
            if (!HasSeed) return;
            if (_bridge == null || !_bridge.CanSend)
            {
                Logger.Warn($"Announce() '{gameObject.name}' — messenger not ready; seed {Seed} not sent");
                return;
            }

            var writer = new BytesWriter(8);
            writer.AddInt((int)(Seed >> 32));
            writer.AddInt((int)(Seed & 0xFFFFFFFF));
            _bridge.SendToAll(MESSAGE_ID, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            if (id != MESSAGE_ID) return;

            var reader = new BytesReader(data);
            long high = reader.NextInt();
            long low = (uint)reader.NextInt();
            long seed = (high << 32) | low;

            if (HasSeed)
            {
                if (seed != Seed) Logger.Warn($"OnMessageToAll() '{gameObject.name}' — seed {seed} ignored; already using {Seed}");
                return;
            }

            Learn(seed, "received");
        }

        private void Learn(long seed, string how)
        {
            Seed = seed;
            HasSeed = true;
            Logger.Info($"Learn() '{gameObject.name}' — world seed {seed} {how} ({DateTimeOffset.FromUnixTimeSeconds(seed):yyyy-MM-dd} UTC)");
            SeedKnown?.Invoke(seed);
        }

        private void OnValidate()
        {
            if (_bridge == null) _bridge = GetComponent<NetworkMessenger>();
        }
    }
}
