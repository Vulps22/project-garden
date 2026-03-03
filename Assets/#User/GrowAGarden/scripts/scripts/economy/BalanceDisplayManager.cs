using SomniumSpace.Network.Bridge;
using TMPro;
using UnityEngine;

namespace GrowAGarden
{
    public class BalanceDisplayManager : MonoBehaviour
    {
        private const byte MESSAGE_ID = 0;

        [SerializeField] private NetworkBridge _bridge;
        [SerializeField] private GameObject[] _displays;

        private TMP_Text[] _nameTexts;
        private TMP_Text[] _balanceTexts;

        private void Start()
        {
            Logger.Log($"[BalanceDisplayManager] Start - bridge={(_bridge == null ? "NULL" : "OK")}, displays={_displays?.Length ?? 0}");
            _bridge.OnMessageToAll += OnMessageToAll;

            _nameTexts = new TMP_Text[_displays.Length];
            _balanceTexts = new TMP_Text[_displays.Length];
            for (int i = 0; i < _displays.Length; i++)
            {
                _nameTexts[i] = _displays[i].transform.Find("NameText")?.GetComponent<TMP_Text>();
                _balanceTexts[i] = _displays[i].transform.Find("BalanceText")?.GetComponent<TMP_Text>();
                Logger.Log($"[BalanceDisplayManager] Start - display[{i}]: nameText={(_nameTexts[i] == null ? "NULL" : "OK")}, balanceText={(_balanceTexts[i] == null ? "NULL" : "OK")}");
            }
        }

        private void OnDestroy()
        {
            _bridge.OnMessageToAll -= OnMessageToAll;
        }

        /// <summary>
        /// Called by EconomyManager on master client with the sorted top-10 list.
        /// Serialises and broadcasts to all clients including master.
        /// </summary>
        public void Set(string[] names, int[] balances)
        {
            Logger.Log($"[BalanceDisplayManager] Set - IsMasterClient={SceneNetworking.IsMasterClient}, names={names?.Length ?? 0}, balances={balances?.Length ?? 0}");
            if (!SceneNetworking.IsMasterClient) return;

            int count = Mathf.Min(names.Length, balances.Length, _displays.Length);
            Logger.Log($"[BalanceDisplayManager] Set - sending {count} entries");

            int size = BytesWriter.ByteSize; // entry count byte
            for (int i = 0; i < count; i++)
                size += sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(names[i])
                      + BytesWriter.IntSize;

            Logger.Log($"[BalanceDisplayManager] Set - payload size={size} bytes");

            var writer = new BytesWriter(size);
            writer.AddByte((byte)count);
            for (int i = 0; i < count; i++)
            {
                Logger.Log($"[BalanceDisplayManager] Set - writing entry [{i}]: name={names[i]}, balance={balances[i]}");
                writer.AddString(names[i]);
                writer.AddInt(balances[i]);
            }

            _bridge.RPC_SendMessageToAll(MESSAGE_ID, writer.Data);
            Logger.Log($"[BalanceDisplayManager] Set - RPC sent");
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            Logger.Log($"[BalanceDisplayManager] OnMessageToAll - id={id}, dataLength={data?.Length ?? 0}");
            if (id != MESSAGE_ID)
            {
                Logger.Log($"[BalanceDisplayManager] OnMessageToAll - ignoring, expected MESSAGE_ID={MESSAGE_ID}");
                return;
            }

            var reader = new BytesReader(data);
            int count = reader.NextByte();
            Logger.Log($"[BalanceDisplayManager] OnMessageToAll - reading {count} entries");

            var names = new string[count];
            var balances = new int[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = reader.NextString();
                balances[i] = reader.NextInt();
                Logger.Log($"[BalanceDisplayManager] OnMessageToAll - entry [{i}]: name={names[i]}, balance={balances[i]}");
            }

            _Set(names, balances);
        }

        private void _Set(string[] names, int[] balances)
        {
            Logger.Log($"[BalanceDisplayManager] _Set - updating {_displays.Length} display slots");
            for (int i = 0; i < _displays.Length; i++)
            {
                bool active = i < names.Length;
                _displays[i].SetActive(active);
                if (active)
                {
                    _nameTexts[i].text = names[i];
                    _balanceTexts[i].text = balances[i].ToString();
                    Logger.Log($"[BalanceDisplayManager] _Set - slot[{i}] set to name={names[i]}, balance={balances[i]}");
                }
                else
                {
                    Logger.Log($"[BalanceDisplayManager] _Set - slot[{i}] hidden (no entry)");
                }
            }
        }
    }
}
