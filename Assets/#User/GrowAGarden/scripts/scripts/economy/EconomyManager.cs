using SomniumSpace.Bridge.Components;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Network.Bridge;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GrowAGarden
{
    public class EconomyManager : MonoBehaviour
    {
        private const byte MESSAGE_ID = 0;

        public static EconomyManager Instance;
        public static event Action<int, int> OnPlayerBalanceChanged;

        [SerializeField] private SomniumPlayersContainer _somniumPlayersContainer;
        [SerializeField] private NetworkBridge _bridge;
        [SerializeField] private BalanceDisplayManager _balanceDisplayManager;
        [SerializeField] private int _startingBalance;

        private Dictionary<string, PlayerBalance> _balances = new Dictionary<string, PlayerBalance>();
        private string _localPlayerId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _somniumPlayersContainer.PlayerAdded.AddListener(OnPlayerJoined);
            _somniumPlayersContainer.LocalPlayerAdded.AddListener(OnLocalPlayerJoined);
        }

        private void Start()
        {
            _bridge.OnMessageToAll += OnMessageToAll;
            SceneNetworking.OnBecomeWorldMaster += OnBecomeWorldMaster;
        }

        private void OnDestroy()
        {
            _bridge.OnMessageToAll -= OnMessageToAll;
            SceneNetworking.OnBecomeWorldMaster -= OnBecomeWorldMaster;
        }

        private void OnBecomeWorldMaster()
        {
            StartCoroutine(BroadcastNextFrame());
        }

        private IEnumerator BroadcastNextFrame()
        {
            yield return null;
            BroadcastBalances();
        }

        private void OnLocalPlayerJoined(ISomniumPlayer player)
        {
            _localPlayerId = player.Properties.Id;
            if (_balances.Count > 0) return;
            var balance = new PlayerBalance(player, _startingBalance);
            _balances.Add(balance.GetID(), balance);
            if (SceneNetworking.IsMasterClient) BroadcastBalances();
        }

        private void OnPlayerJoined(ISomniumPlayer player)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (_balances.ContainsKey(player.Properties.Id)) return;
            var balance = new PlayerBalance(player, _startingBalance);
            _balances.Add(balance.GetID(), balance);
            BroadcastBalances();
        }

        /// <summary>
        /// Adds Thatch to a player's balance and broadcasts the updated state to all clients.
        /// Master client only.
        /// </summary>
        public void AddBalance(string playerId, int amount)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (!_balances.TryGetValue(playerId, out var balance))
            {
                return;
            }
            balance.AddBalance(amount);
            BroadcastBalances();
        }

        /// <summary>
        /// Deducts Thatch from a player's balance and broadcasts the updated state to all clients.
        /// Master client only.
        /// </summary>
        public void RemoveBalance(string playerId, int amount)
        {
            if (!_balances.TryGetValue(playerId, out var balance))
            {
                Logger.Warn($"[EconomyManager] RemoveBalance - playerId={playerId} not found in _balances");
                return;
            }
            balance.RemoveBalance(amount);
            if(SceneNetworking.IsMasterClient) BroadcastBalances();
        }

        private void BroadcastBalances()
        {
            if (!SceneNetworking.IsMasterClient) return;

            int size = BytesWriter.ByteSize; // entry count
            foreach (var b in _balances.Values)
            {
                size += sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(b.GetID())
                      + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(b.GetPlayerName())
                      + BytesWriter.IntSize;
            }


            var writer = new BytesWriter(size);
            writer.AddByte((byte)_balances.Count);
            foreach (var b in _balances.Values)
            {
                writer.AddString(b.GetID());
                writer.AddString(b.GetPlayerName());
                writer.AddInt(b.GetBalance());
            }

            _bridge.RPC_SendMessageToAll(MESSAGE_ID, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            if (id != MESSAGE_ID)
            {
                return;
            }

            _balances.TryGetValue(_localPlayerId, out PlayerBalance oldBalance);

            var reader = new BytesReader(data);
            int count = reader.NextByte();

            _balances.Clear();
            for (int i = 0; i < count; i++)
            {
                string playerId = reader.NextString();
                string playerName = reader.NextString();
                int balance = reader.NextInt();
                _balances[playerId] = new PlayerBalance(playerId, playerName, balance);
            }

            UpdateDisplay();
            _balances.TryGetValue(_localPlayerId, out var localBalance);
            if ((localBalance != null && oldBalance != null) && (localBalance != oldBalance))
            {
                OnPlayerBalanceChanged?.Invoke(localBalance.GetBalance(), oldBalance.GetBalance());
            }
        }

        private void UpdateDisplay()
        {
            var sorted = _balances.Values
                .OrderByDescending(b => b.GetBalance())
                .ToArray();


            var names = new string[sorted.Length];
            var balances = new int[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                names[i] = sorted[i].GetPlayerName();
                balances[i] = sorted[i].GetBalance();
            }

            _balanceDisplayManager.Set(names, balances);
        }

        public PlayerBalance GetLocalPlayer()
        {
            _balances.TryGetValue(_localPlayerId, out PlayerBalance player);
            return player;
        }

        public PlayerBalance GetPlayer(string id)
        {
            _balances.TryGetValue(id, out PlayerBalance player);
            return player;
        }
    }
}
