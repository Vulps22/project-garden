using SomniumSpace.Network.Bridge;
using System;
using UnityEngine;

namespace GrowAGarden
{
    public class PlantSlot : MonoBehaviour
    {
        [SerializeField] NetworkBridge _networkBridge;

        public bool IsOccupied { get; private set; } = false;

        private void Start()
        {
            Logger.Info($"Start() '{gameObject.name}' — networkBridge={(_networkBridge != null ? "found" : "NULL")}");
            if (_networkBridge == null)
            {
                Logger.Error($"Start() '{gameObject.name}' — _networkBridge is NULL! RPC messages won't work!");
                return;
            }
            _networkBridge.OnMessageToAll += OnMessageToAll;
            Logger.Log($"Start() '{gameObject.name}' — subscribed to OnMessageToAll");
        }

        /// <summary>
        /// Update the IsOccupied variable and broadcast the change to all clients
        /// </summary>
        /// <param name="occupied"></param>
        public void SetOccupied(bool occupied)
        {
            Logger.Info($"SetOccupied({occupied}) called on '{gameObject.name}'");
            IsOccupied = occupied;
            _networkBridge.RPC_SendMessageToAll((byte) PlantSlotMessageType.OccupationChanged, new byte[] { occupied ? (byte) 1 : (byte) 0 });
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            Logger.Info($"OnMessageToAll() '{gameObject.name}' — messageId={id} ({(PlantSlotMessageType)id}), dataLength={data?.Length}");

            switch ((PlantSlotMessageType)id)
            {
                case PlantSlotMessageType.OccupationChanged:
                {
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — Occupied State RPC received");
                        IsOccupied = data[0] == 1;
                    break;
                }
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — unknown messageId={id}");
                    break;
            }
        }

        enum PlantSlotMessageType : byte
        {
            OccupationChanged
        }
    }
}
