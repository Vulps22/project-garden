using System;
using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>Sends and receives this game's messages on one networked object.</summary>
    [RequireComponent(typeof(NetworkBridgeEvents))]
    public class NetworkMessenger : MonoBehaviour
    {
        [SerializeField] private NetworkBridgeEvents _transport;

        private NetworkBridgeEvents Transport => _transport != null ? _transport : _transport = GetComponent<NetworkBridgeEvents>();

        /// <summary>The NetworkObject this messenger sends on.</summary>
        public NetworkObject Object => Transport != null ? Transport.Object : null;

        /// <summary>The object was spawned on the network.</summary>
        public event Action OnSpawned { add => Transport.OnSpawned += value; remove { if (Transport != null) Transport.OnSpawned -= value; } }

        /// <summary>This client gained or lost state authority over the object.</summary>
        public event Action<bool> OnStateAuthorityChanged { add => Transport.OnStateAuthorityChanged += value; remove { if (Transport != null) Transport.OnStateAuthorityChanged -= value; } }

        /// <summary>A message reached every client, including whoever sent it.</summary>
        public event Action<byte, byte[]> MessageToAll;

        /// <summary>A message reached every client except the state authority.</summary>
        public event Action<byte, byte[]> MessageToProxies;

        /// <summary>A message reached the state authority.</summary>
        public event Action<byte, byte[]> MessageToController;

        /// <summary>A message reached this client alone.</summary>
        public event Action<byte, byte[]> MessageToPlayer;

        /// <summary>True while there is a live NetworkObject to send on.</summary>
        public bool CanSend => Object != null;

        /// <summary>Tells everyone, sender included.</summary>
        public void SendToAll(byte id, byte[] data) => Transport.RPC_SendMessageToAll(Pack(id, data));

        /// <summary>Tells everyone but the state authority, which already has the state.</summary>
        public void SendToProxies(byte id, byte[] data) => Transport.RPC_SendMessageToProxies(Pack(id, data));

        /// <summary>Tells the state authority.</summary>
        public void SendToController(byte id, byte[] data) => Transport.RPC_SendMessageToController(Pack(id, data));

        /// <summary>Tells one player — the late-join push, rather than telling the room.</summary>
        public void SendToPlayer(PlayerRef player, byte id, byte[] data) =>
            Transport.RPC_SendMessageToPlayer(player, Pack(id, data));

        private void Awake()
        {
            Transport.OnMessageToAll += RaiseToAll;
            Transport.OnMessageToProxies += RaiseToProxies;
            Transport.OnMessageToController += RaiseToController;
            Transport.OnMessageToPlayer += RaiseToPlayer;
        }

        private void OnDestroy()
        {
            if (Transport == null) return;
            Transport.OnMessageToAll -= RaiseToAll;
            Transport.OnMessageToProxies -= RaiseToProxies;
            Transport.OnMessageToController -= RaiseToController;
            Transport.OnMessageToPlayer -= RaiseToPlayer;
        }

        private void OnValidate()
        {
            if (_transport == null) _transport = GetComponent<NetworkBridgeEvents>();
        }

        // The transport carries a bare byte[]; the message id rides in byte 0. One copy per
        // message — callers pre-size their BytesWriter exactly, so the alternative is every
        // payload reserving a leading byte and every size calculation knowing it.
        private static byte[] Pack(byte id, byte[] data)
        {
            byte[] framed = new byte[(data?.Length ?? 0) + 1];
            framed[0] = id;
            if (data != null) Buffer.BlockCopy(data, 0, framed, 1, data.Length);
            return framed;
        }

        private static bool Unpack(byte[] framed, out byte id, out byte[] data)
        {
            id = 0;
            data = Array.Empty<byte>();
            if (framed == null || framed.Length == 0) return false;

            id = framed[0];
            data = new byte[framed.Length - 1];
            Buffer.BlockCopy(framed, 1, data, 0, data.Length);
            return true;
        }

        private void RaiseToAll(byte[] f) { if (Unpack(f, out byte id, out byte[] d)) MessageToAll?.Invoke(id, d); }
        private void RaiseToProxies(byte[] f) { if (Unpack(f, out byte id, out byte[] d)) MessageToProxies?.Invoke(id, d); }
        private void RaiseToController(byte[] f) { if (Unpack(f, out byte id, out byte[] d)) MessageToController?.Invoke(id, d); }
        private void RaiseToPlayer(byte[] f) { if (Unpack(f, out byte id, out byte[] d)) MessageToPlayer?.Invoke(id, d); }
    }
}
