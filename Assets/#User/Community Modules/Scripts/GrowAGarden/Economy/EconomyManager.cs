using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Economy
{
    public class EconomyManager : MonoBehaviour
    {
        private const byte MESSAGE_ID = 0;
        private const byte TABLE_REQUEST_ID = 22;   // payload: 0 a joiner asking the master, 1 a new master asking everyone
        private const byte TABLE_OFFER_ID = 23;     // a peer's copy of the table, for a new master that never had it

        public static EconomyManager Instance;
        public static event Action<int, int> PlayerBalanceChanged;

        [SerializeField] private NetworkMessenger _bridge;
        [SerializeField] private BalanceDisplayManager _balanceDisplayManager;
        [SerializeField] private int _startingBalance;

        private Dictionary<string, PlayerBalance> _balances = new Dictionary<string, PlayerBalance>();
        private string _localPlayerId;

        // A table has arrived, or this client opened it as the first master: until then it knows only its own row.
        private bool _hasTable;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;
        }

        private void Start()
        {
            _bridge.MessageToAll += OnMessageToAll;
            _bridge.OnSpawned += AskForTable;
            PlayerBridge.BecameWorldMaster += OnBecomeWorldMaster;
            PlayerManager.OtherPlayerJoined += OnOtherPlayerJoined;
            WorldBridge.NetworkReady += AskForTable;
            if (WorldBridge.IsNetworkReady) AskForTable();
        }

        private void OnDestroy()
        {
            if (_bridge != null) { _bridge.MessageToAll -= OnMessageToAll; _bridge.OnSpawned -= AskForTable; }
            PlayerBridge.BecameWorldMaster -= OnBecomeWorldMaster;
            PlayerManager.OtherPlayerJoined -= OnOtherPlayerJoined;
            WorldBridge.NetworkReady -= AskForTable;
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
        }

        /// <summary>A joiner's board is filled by the master's push on the join, or by asking once its messenger can
        /// talk: the push can arrive before the joiner's scene objects are attached, and is then lost.</summary>
        private void OnOtherPlayerJoined() => BroadcastBalances();

        private void AskForTable()
        {
            if (_hasTable || PlayerManager.IsMaster || _bridge == null || !_bridge.CanSend) return;
            _bridge.SendToAll(TABLE_REQUEST_ID, new byte[] { 0 });
        }

        private void OnBecomeWorldMaster()
        {
            StartCoroutine(BroadcastNextFrame());
        }

        /// <summary>A new master that never received the table would wipe everyone's balances with its own row: it asks
        /// the others for theirs instead, and broadcasts once one arrives (OnTableOffered).</summary>
        private IEnumerator BroadcastNextFrame()
        {
            yield return null;
            if (_hasTable) { BroadcastBalances(); yield break; }
            Logger.Warn($"BroadcastNextFrame() '{gameObject.name}' — master without the balance table; asking the others for it");
            if (_bridge != null && _bridge.CanSend) _bridge.SendToAll(TABLE_REQUEST_ID, new byte[] { 1 });
        }

        private void OnLocalPlayerJoined(string playerId, string playerName)
        {
            _localPlayerId = playerId;
            if (_balances.Count > 0) return;
            _balances.Add(playerId, new PlayerBalance(playerId, playerName, _startingBalance));
            if (PlayerManager.IsMaster) { _hasTable = true; BroadcastBalances(); }
        }

        /// <summary>
        /// Opens a balance for a player at the starting amount and broadcasts the table.
        ///
        /// Does nothing if they already have one — a rejoin inside the grace period reuses an id
        /// whose row was never removed.
        /// </summary>
        public void EnsureBalance(string playerId, string playerName)
        {
            if (!PlayerManager.IsMaster) return;
            if (_balances.ContainsKey(playerId)) return;
            _balances.Add(playerId, new PlayerBalance(playerId, playerName, _startingBalance));
            BroadcastBalances();
        }

        /// <summary>
        /// Drops a player from the balance table and the board, and broadcasts the table.
        ///
        /// The broadcast carries the removal by itself — every client rebuilds _balances from
        /// scratch each time — so there is no removal message.
        /// </summary>
        public void RemovePlayer(string playerId)
        {
            if (!PlayerManager.IsMaster) return;
            if (!_balances.Remove(playerId)) return;

            Logger.Info($"RemovePlayer() '{gameObject.name}' — removed '{playerId}' from the balance table");
            BroadcastBalances();
        }

        /// <summary>
        /// Adds Thatch to a player's balance and broadcasts the updated state to all clients.
        /// Master client only.
        /// </summary>
        public void AddBalance(string playerId, int amount)
        {
            if (!PlayerManager.IsMaster) return;
            if (string.IsNullOrEmpty(playerId))
            {
                Logger.Warn("[EconomyManager] AddBalance - null/empty playerId, ignoring");
                return;
            }
            if (!_balances.TryGetValue(playerId, out var balance))
            {
                return;
            }
            int before = balance.GetBalance();
            balance.AddBalance(amount);
            RaiseIfLocal(playerId, balance.GetBalance(), before);
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
            int before = balance.GetBalance();
            balance.RemoveBalance(amount);
            RaiseIfLocal(playerId, balance.GetBalance(), before);
            if(PlayerManager.IsMaster) BroadcastBalances();
        }

        /// <summary>
        /// Announces a balance change made locally, when it is the local player's.
        ///
        /// **The mutation paths have to raise this, not just the broadcast.** A mutation edits the
        /// PlayerBalance object in place and then sends; when that broadcast arrives back at the
        /// sender, OnMessageToAll compares the rebuilt value against an "old" reference that is
        /// the very object already mutated — so old equals new and the event is swallowed. The
        /// master therefore never heard about its own balance changing, and neither did any client
        /// that applied a deduction optimistically. The tutorial's 100-Thatch line could not fire
        /// for a solo player because of it, and the balance display only worked because it redraws
        /// from the table rather than listening to this.
        ///
        /// The value comparison in OnMessageToAll then correctly suppresses the duplicate when the
        /// broadcast lands, so nothing fires twice.
        /// </summary>
        private void RaiseIfLocal(string playerId, int newValue, int oldValue)
        {
            if (newValue == oldValue) return;
            if (string.IsNullOrEmpty(_localPlayerId) || playerId != _localPlayerId) return;

            PlayerBalanceChanged?.Invoke(newValue, oldValue);
        }

        private void BroadcastBalances()
        {
            if (!PlayerManager.IsMaster || _bridge == null || !_bridge.CanSend) return;
            _bridge.SendToAll(MESSAGE_ID, WriteTable());
        }

        private byte[] WriteTable()
        {
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
                writer.AddAutoString(b.GetID());
                writer.AddAutoString(b.GetPlayerName());
                writer.AddInt(b.GetBalance());
            }
            return writer.Data;
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch (id)
            {
                case MESSAGE_ID: ApplyTable(data); break;
                case TABLE_REQUEST_ID: OnTableRequested(data); break;
                case TABLE_OFFER_ID: OnTableOffered(data); break;
            }
        }

        /// <summary>The master answers a joiner; anyone holding the table answers a master that lacks it.</summary>
        private void OnTableRequested(byte[] data)
        {
            if (PlayerManager.IsMaster) { if (_hasTable) BroadcastBalances(); return; }
            bool fromNewMaster = data.Length > 0 && data[0] == 1;
            if (fromNewMaster && _hasTable && _bridge.CanSend) _bridge.SendToAll(TABLE_OFFER_ID, WriteTable());
        }

        /// <summary>A peer's table reaching a master that lacked it: the first one overrides the master's own rows, which
        /// were opened blank, and keeps any others; later offers add only what is still missing.</summary>
        private void OnTableOffered(byte[] data)
        {
            if (!PlayerManager.IsMaster) return;
            var reader = new BytesReader(data);
            int count = reader.NextByte();
            bool added = !_hasTable;
            for (int i = 0; i < count; i++)
            {
                string playerId = reader.NextAutoString();
                string playerName = reader.NextAutoString();
                int balance = reader.NextInt();
                if (!_hasTable || !_balances.ContainsKey(playerId))
                {
                    _balances[playerId] = new PlayerBalance(playerId, playerName, balance);
                    added = true;
                }
            }
            _hasTable = true;
            if (added) BroadcastBalances();
        }

        private void ApplyTable(byte[] data)
        {
            _hasTable = true;
            PlayerBalance oldBalance = GetLocalPlayer();

            var reader = new BytesReader(data);
            int count = reader.NextByte();

            _balances.Clear();
            for (int i = 0; i < count; i++)
            {
                string playerId = reader.NextAutoString();
                string playerName = reader.NextAutoString();
                int balance = reader.NextInt();
                _balances[playerId] = new PlayerBalance(playerId, playerName, balance);
            }

            UpdateDisplay();
            TableApplied?.Invoke();

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
                    PlayerBalanceChanged?.Invoke(newValue, oldValue);
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

        /// <summary>Everyone with a balance: the players here, and those gone but inside their grace period.</summary>
        public IEnumerable<string> PlayerIds => _balances.Keys;

        /// <summary>Raised on every client when a balance table has been applied.</summary>
        public static event Action TableApplied;

        /// <summary>Returns the balance for <paramref name="id"/>, or null if unknown.</summary>
        public PlayerBalance GetPlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _balances.TryGetValue(id, out PlayerBalance player);
            return player;
        }
    }
}
