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
            PlayerManager.PlayerJoined += OnPlayerJoined;
            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;
            PlayerManager.PlayerDestroyed += OnPlayerDestroyed;
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
            PlayerManager.PlayerJoined -= OnPlayerJoined;
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
            PlayerManager.PlayerDestroyed -= OnPlayerDestroyed;
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

        private void OnLocalPlayerJoined(string playerId, string playerName)
        {
            _localPlayerId = playerId;
            if (_balances.Count > 0) return;
            _balances.Add(playerId, new PlayerBalance(playerId, playerName, _startingBalance));
            if (SceneNetworking.IsMasterClient) BroadcastBalances();
        }

        private void OnPlayerJoined(string playerId, string playerName)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (_balances.ContainsKey(playerId)) return;
            _balances.Add(playerId, new PlayerBalance(playerId, playerName, _startingBalance));
            BroadcastBalances();
        }

        /// <summary>
        /// A departed player did not come back, so they leave the table and the balance board.
        /// Master only, and no new message type is needed: every client clears and rebuilds
        /// _balances from each broadcast, so a removal propagates exactly like a change does.
        ///
        /// Nothing removed them before this, which is why a leaver's name stayed on the board for
        /// the rest of the session.
        /// </summary>
        private void OnPlayerDestroyed(string playerId)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (!_balances.Remove(playerId)) return;

            Logger.Info($"OnPlayerDestroyed() '{gameObject.name}' — removed '{playerId}' from the balance table");
            BroadcastBalances();
        }

        /// <summary>
        /// Adds Thatch to a player's balance and broadcasts the updated state to all clients.
        /// Master client only.
        /// </summary>
        public void AddBalance(string playerId, int amount)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (string.IsNullOrEmpty(playerId))
            {
                Logger.Warn("[EconomyManager] AddBalance - null/empty playerId, ignoring");
                return;
            }
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
            if (string.IsNullOrEmpty(playerId))
            {
                Logger.Warn("[EconomyManager] RemoveBalance - null/empty playerId, ignoring");
                return;
            }
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

            PlayerBalance oldBalance = GetLocalPlayer();

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

            // Compare by value, not by reference. The rebuild above replaces every
            // PlayerBalance with a fresh instance, so a reference comparison is always
            // unequal and would raise the event on every broadcast whether or not the
            // balance actually moved. oldBalance still holds its pre-broadcast value —
            // Clear() drops the dictionary entries, not the objects themselves.
            PlayerBalance localBalance = GetLocalPlayer();
            if (localBalance != null && oldBalance != null)
            {
                int newValue = localBalance.GetBalance();
                int oldValue = oldBalance.GetBalance();
                if (newValue != oldValue)
                    OnPlayerBalanceChanged?.Invoke(newValue, oldValue);
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

        /// <summary>
        /// Returns the local player's balance, or null if it is not currently known.
        /// _localPlayerId is unset until OnLocalPlayerJoined fires, and Dictionary lookups
        /// throw on a null key — so this must be guarded, not just null-checked by callers.
        /// </summary>
        public PlayerBalance GetLocalPlayer()
        {
            if (string.IsNullOrEmpty(_localPlayerId)) return null;
            _balances.TryGetValue(_localPlayerId, out PlayerBalance player);
            return player;
        }

        /// <summary>Returns the balance for <paramref name="id"/>, or null if unknown.</summary>
        public PlayerBalance GetPlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _balances.TryGetValue(id, out PlayerBalance player);
            return player;
        }
    }
}
