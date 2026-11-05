using System;
using System.Collections.Generic;
using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace Vulps.GrowAGarden
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

        /// <summary>
        /// True while the NetworkObject is spawned and attached to the runner, so RPCs can be sent. Not just "it exists":
        /// a late joiner's scene objects exist before they are attached, and an RPC then throws inside Fusion.
        /// </summary>
        public bool CanSend => Object != null && Object.IsValid;

        /// <summary>Reserved: marks a piece of a message too large for one RPC. Never a game message id.</summary>
        public const byte FRAGMENT_ID = 255;

        private const int MAX_FRAME = 400;       // Fusion drops an RPC over 512 bytes; this leaves room for its own framing
        private const int FRAGMENT_HEADER = 8;   // marker, id, key (4), index, count

        private uint _nextKey;
        private readonly Dictionary<(byte channel, uint key), byte[][]> _partial = new Dictionary<(byte, uint), byte[][]>();

        /// <summary>Tells everyone, sender included.</summary>
        public void SendToAll(byte id, byte[] data) { foreach (byte[] f in Frames(id, data)) Transport.RPC_SendMessageToAll(f); }

        /// <summary>Tells everyone but the state authority, which already has the state.</summary>
        public void SendToProxies(byte id, byte[] data) { foreach (byte[] f in Frames(id, data)) Transport.RPC_SendMessageToProxies(f); }

        /// <summary>Tells the state authority.</summary>
        public void SendToController(byte id, byte[] data) { foreach (byte[] f in Frames(id, data)) Transport.RPC_SendMessageToController(f); }

        /// <summary>Tells one player — the late-join push, rather than telling the room.</summary>
        public void SendToPlayer(PlayerRef player, byte id, byte[] data) { foreach (byte[] f in Frames(id, data)) Transport.RPC_SendMessageToPlayer(player, f); }

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

        /// <summary>One frame if it fits, else numbered pieces the receiver puts back together before raising it.</summary>
        private List<byte[]> Frames(byte id, byte[] data)
        {
            var frames = new List<byte[]>(1);
            if (id == FRAGMENT_ID) { Logger.Error($"Frames() '{gameObject.name}' — message id {FRAGMENT_ID} is reserved; not sent"); return frames; }
            int length = data?.Length ?? 0;
            if (length + 1 <= MAX_FRAME) { frames.Add(Pack(id, data)); return frames; }

            int chunk = MAX_FRAME - FRAGMENT_HEADER;
            int count = (length + chunk - 1) / chunk;
            if (count > byte.MaxValue) { Logger.Error($"Frames() '{gameObject.name}' — message {id} is {length} bytes, too large even in pieces; not sent"); return frames; }

            int peer = Object != null && Object.Runner != null ? Object.Runner.LocalPlayer.PlayerId : 0;
            uint key = ((uint)(peer & 0xFF) << 24) | (++_nextKey & 0xFFFFFF);   // unique per sender, so two senders' pieces never mix
            for (int i = 0; i < count; i++)
            {
                int offset = i * chunk;
                int size = Math.Min(chunk, length - offset);
                byte[] f = new byte[FRAGMENT_HEADER + size];
                f[0] = FRAGMENT_ID;
                f[1] = id;
                f[2] = (byte)(key >> 24); f[3] = (byte)(key >> 16); f[4] = (byte)(key >> 8); f[5] = (byte)key;
                f[6] = (byte)i;
                f[7] = (byte)count;
                Buffer.BlockCopy(data, offset, f, FRAGMENT_HEADER, size);
                frames.Add(f);
            }
            return frames;
        }

        /// <summary>A whole message, or false while a fragmented one is still arriving.</summary>
        private bool Receive(byte channel, byte[] framed, out byte id, out byte[] data)
        {
            if (framed == null || framed.Length < FRAGMENT_HEADER || framed[0] != FRAGMENT_ID) return Unpack(framed, out id, out data);

            id = framed[1];
            data = null;
            uint key = ((uint)framed[2] << 24) | ((uint)framed[3] << 16) | ((uint)framed[4] << 8) | framed[5];
            int index = framed[6], count = framed[7];
            var slot = (channel, key);
            if (!_partial.TryGetValue(slot, out byte[][] parts)) _partial[slot] = parts = new byte[count][];
            if (index >= parts.Length || parts[index] != null) return false;

            parts[index] = new byte[framed.Length - FRAGMENT_HEADER];
            Buffer.BlockCopy(framed, FRAGMENT_HEADER, parts[index], 0, parts[index].Length);
            int total = 0;
            foreach (byte[] part in parts) { if (part == null) return false; total += part.Length; }

            _partial.Remove(slot);
            data = new byte[total];
            int at = 0;
            foreach (byte[] part in parts) { Buffer.BlockCopy(part, 0, data, at, part.Length); at += part.Length; }
            return true;
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

        private void RaiseToAll(byte[] f) { if (Receive(0, f, out byte id, out byte[] d)) MessageToAll?.Invoke(id, d); }
        private void RaiseToProxies(byte[] f) { if (Receive(1, f, out byte id, out byte[] d)) MessageToProxies?.Invoke(id, d); }
        private void RaiseToController(byte[] f) { if (Receive(2, f, out byte id, out byte[] d)) MessageToController?.Invoke(id, d); }
        private void RaiseToPlayer(byte[] f) { if (Receive(3, f, out byte id, out byte[] d)) MessageToPlayer?.Invoke(id, d); }
    }
}
