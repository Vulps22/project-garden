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
            if (_networkBridge == null)
            {
                Logger.Error($"Start() '{gameObject.name}' — _networkBridge is NULL! RPC messages won't work!");
                return;
            }
            _networkBridge.OnMessageToAll += OnMessageToAll;
        }

        /// <summary>
        /// Update the IsOccupied variable and broadcast the change to all clients
        /// </summary>
        /// <param name="occupied"></param>
        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            _networkBridge.RPC_SendMessageToAll((byte) PlantSlotMessageType.OccupationChanged, new byte[] { occupied ? (byte) 1 : (byte) 0 });
        }

        private void OnMessageToAll(byte id, byte[] data)
        {

            switch ((PlantSlotMessageType)id)
            {
                case PlantSlotMessageType.OccupationChanged:
                {
                        IsOccupied = data[0] == 1;
                    break;
                }
                default:
                    break;
            }
        }

        enum PlantSlotMessageType : byte
        {
            OccupationChanged
        }
    }
}
