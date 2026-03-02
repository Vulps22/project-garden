using SomniumSpace.Bridge.Components;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Network.Bridge;
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

        [SerializeField] private SomniumPlayersContainer _somniumPlayersContainer;
        [SerializeField] private NetworkBridge _bridge;
        [SerializeField] private BalanceDisplayManager _balanceDisplayManager;
        [SerializeField] private int _startingBalance;

        private Dictionary<string, PlayerBalance> _balances = new Dictionary<string, PlayerBalance>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Logger.Warn("[EconomyManager] Duplicate Instance Destroyed");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Logger.Log($"[EconomyManager] Awake - Instance set");
            _somniumPlayersContainer.PlayerAdded.AddListener(OnPlayerJoined);
            _somniumPlayersContainer.LocalPlayerAdded.AddListener(OnLocalPlayerJoined);
            Logger.Log($"[EconomyManager] Awake - PlayerAdded and LocalPlayerAdded listeners registered");
        }

        private void Start()
        {
            Logger.Log($"[EconomyManager] Start - IsMasterClient={SceneNetworking.IsMasterClient}, startingBalance={_startingBalance}");
            Logger.Log($"[EconomyManager] Start - bridge={(_bridge == null ? "NULL" : "OK")}, displayManager={(_balanceDisplayManager == null ? "NULL" : "OK")}, playersContainer={(_somniumPlayersContainer == null ? "NULL" : "OK")}");
            _bridge.OnMessageToAll += OnMessageToAll;
            SceneNetworking.OnBecomeWorldMaster += OnBecomeWorldMaster;
            Logger.Log($"[EconomyManager] Start - listeners registered");
        }

        private void OnDestroy()
        {
            _bridge.OnMessageToAll -= OnMessageToAll;
            SceneNetworking.OnBecomeWorldMaster -= OnBecomeWorldMaster;
        }

        private void OnBecomeWorldMaster()
        {
            Logger.Log($"[EconomyManager] OnBecomeWorldMaster - _balances.Count={_balances.Count}, deferring broadcast one frame");
            StartCoroutine(BroadcastNextFrame());
        }

        private IEnumerator BroadcastNextFrame()
        {
            yield return null;
            Logger.Log("[EconomyManager] BroadcastNextFrame - broadcasting now");
            BroadcastBalances();
        }

        private void OnLocalPlayerJoined(ISomniumPlayer player)
        {
            Logger.Log($"[EconomyManager] OnLocalPlayerJoined - name={player.Properties.NickName}, id={player.Properties.Id}, _balances.Count={_balances.Count}");
            if (_balances.Count > 0) return;
            var balance = new PlayerBalance(player, _startingBalance);
            _balances.Add(balance.GetID(), balance);
            Logger.Log($"[EconomyManager] OnLocalPlayerJoined - added self as first entry, IsMasterClient={SceneNetworking.IsMasterClient}");
            if (SceneNetworking.IsMasterClient) BroadcastBalances();
        }

        private void OnPlayerJoined(ISomniumPlayer player)
        {
            Logger.Log($"[EconomyManager] OnPlayerJoined - name={player.Properties.NickName}, id={player.Properties.Id}, IsMasterClient={SceneNetworking.IsMasterClient}");
            if (!SceneNetworking.IsMasterClient) return;
            if (_balances.ContainsKey(player.Properties.Id)) return;
            var balance = new PlayerBalance(player, _startingBalance);
            _balances.Add(balance.GetID(), balance);
            Logger.Log($"[EconomyManager] OnPlayerJoined - added remote player {player.Properties.NickName}, total players tracked={_balances.Count}");
            BroadcastBalances();
        }

        /// <summary>
        /// Adds Thatch to a player's balance and broadcasts the updated state to all clients.
        /// Master client only.
        /// </summary>
        public void AddBalance(string playerId, int amount)
        {
            Logger.Log($"[EconomyManager] AddBalance - playerId={playerId}, amount={amount}, IsMasterClient={SceneNetworking.IsMasterClient}");
            if (!SceneNetworking.IsMasterClient) return;
            if (!_balances.TryGetValue(playerId, out var balance))
            {
                Logger.Warn($"[EconomyManager] AddBalance - playerId={playerId} not found in _balances");
                return;
            }
            balance.AddBalance(amount);
            Logger.Log($"[EconomyManager] AddBalance - new balance for {balance.GetPlayerName()}={balance.GetBalance()}");
            BroadcastBalances();
        }

        /// <summary>
        /// Deducts Thatch from a player's balance and broadcasts the updated state to all clients.
        /// Master client only.
        /// </summary>
        public void RemoveBalance(string playerId, int amount)
        {
            Logger.Log($"[EconomyManager] RemoveBalance - playerId={playerId}, amount={amount}, IsMasterClient={SceneNetworking.IsMasterClient}");
            if (!SceneNetworking.IsMasterClient) return;
            if (!_balances.TryGetValue(playerId, out var balance))
            {
                Logger.Warn($"[EconomyManager] RemoveBalance - playerId={playerId} not found in _balances");
                return;
            }
            balance.RemoveBalance(amount);
            Logger.Log($"[EconomyManager] RemoveBalance - new balance for {balance.GetPlayerName()}={balance.GetBalance()}");
            BroadcastBalances();
        }

        private void BroadcastBalances()
        {
            if (!SceneNetworking.IsMasterClient) return;
            Logger.Log($"[EconomyManager] BroadcastBalances - sending {_balances.Count} entries");

            int size = BytesWriter.ByteSize; // entry count
            foreach (var b in _balances.Values)
            {
                size += sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(b.GetID())
                      + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(b.GetPlayerName())
                      + BytesWriter.IntSize;
            }

            Logger.Log($"[EconomyManager] BroadcastBalances - payload size={size} bytes");

            var writer = new BytesWriter(size);
            writer.AddByte((byte)_balances.Count);
            foreach (var b in _balances.Values)
            {
                Logger.Log($"[EconomyManager] BroadcastBalances - writing entry: id={b.GetID()}, name={b.GetPlayerName()}, balance={b.GetBalance()}");
                writer.AddString(b.GetID());
                writer.AddString(b.GetPlayerName());
                writer.AddInt(b.GetBalance());
            }

            _bridge.RPC_SendMessageToAll(MESSAGE_ID, writer.Data);
            Logger.Log($"[EconomyManager] BroadcastBalances - RPC sent");
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            Logger.Log($"[EconomyManager] OnMessageToAll - id={id}, dataLength={data?.Length ?? 0}, IsMasterClient={SceneNetworking.IsMasterClient}");
            if (id != MESSAGE_ID)
            {
                Logger.Log($"[EconomyManager] OnMessageToAll - ignoring, expected MESSAGE_ID={MESSAGE_ID}");
                return;
            }

            var reader = new BytesReader(data);
            int count = reader.NextByte();
            Logger.Log($"[EconomyManager] OnMessageToAll - reading {count} entries");

            _balances.Clear();
            for (int i = 0; i < count; i++)
            {
                string playerId = reader.NextString();
                string playerName = reader.NextString();
                int balance = reader.NextInt();
                Logger.Log($"[EconomyManager] OnMessageToAll - entry [{i}]: id={playerId}, name={playerName}, balance={balance}");
                _balances[playerId] = new PlayerBalance(playerId, playerName, balance);
            }

            Logger.Log($"[EconomyManager] OnMessageToAll - _balances rebuilt, calling UpdateDisplay");
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            var sorted = _balances.Values
                .OrderByDescending(b => b.GetBalance())
                .ToArray();

            Logger.Log($"[EconomyManager] UpdateDisplay - {sorted.Length} entries sorted by balance");

            var names = new string[sorted.Length];
            var balances = new int[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                names[i] = sorted[i].GetPlayerName();
                balances[i] = sorted[i].GetBalance();
                Logger.Log($"[EconomyManager] UpdateDisplay - [{i}] {names[i]} = {balances[i]}");
            }

            _balanceDisplayManager.Set(names, balances);
            Logger.Log($"[EconomyManager] UpdateDisplay - Set() called on BalanceDisplayManager");
        }
    }
}
