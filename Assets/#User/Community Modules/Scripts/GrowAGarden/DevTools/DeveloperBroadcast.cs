using UnityEngine;
using Vulps.GrowAGarden.Notifications;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>
    /// A notification to everyone in the world, the sender included, from anyone with developer access (World Settings →
    /// Developer → Notify Everyone). The sender asks; the master checks their access and sends the text to all, and each client
    /// shows it through the <see cref="Notifier"/>. See docs/dev-tools.md.
    /// </summary>
    public class DeveloperBroadcast : MonoBehaviour
    {
        private const byte ASK_ID = 12;
        private const byte SHOW_ID = 13;

        /// <summary>The longest notification, in characters.</summary>
        public const int MAX_LENGTH = 120;

        [Tooltip("SceneManager's messenger. Message ids 12-13 on it; WorldManager has 11.")]
        [SerializeField] private NetworkMessenger _bridge;

        private static DeveloperBroadcast _instance;

        /// <summary>Asks the master to show this text to everyone. False if it could not be sent.</summary>
        public static bool Send(string text)
        {
            if (_instance == null || !Developer.IsLocal || _instance._bridge == null || !_instance._bridge.CanSend) return false;
            text = text == null ? string.Empty : text.Trim();
            if (text.Length == 0) return false;
            if (text.Length > MAX_LENGTH) text = text.Substring(0, MAX_LENGTH);

            string me = PlayerManager.LocalPlayerId ?? string.Empty;
            var writer = new BytesWriter(sizeof(short) * 2 + System.Text.Encoding.UTF8.GetByteCount(me) + System.Text.Encoding.UTF8.GetByteCount(text));
            writer.AddAutoString(me);
            writer.AddAutoString(text);
            _instance._bridge.SendToAll(ASK_ID, writer.Data);
            Logger.Info($"Send() 'DeveloperBroadcast' — asked to notify everyone: '{text}'");
            return true;
        }

        private void Awake() => _instance = this;

        private void Start()
        {
            if (_bridge == null) { Logger.Warn($"Start() '{gameObject.name}' — no messenger; no developer notifications"); return; }
            _bridge.MessageToAll += OnMessageToAll;
        }

        private void OnDestroy()
        {
            if (_bridge != null) _bridge.MessageToAll -= OnMessageToAll;
            if (_instance == this) _instance = null;
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            if (id == ASK_ID) OnAsked(data);
            else if (id == SHOW_ID) Notifier.Push(new BytesReader(data).NextAutoString());
        }

        /// <summary>Master only: an ask from someone without developer access is refused.</summary>
        private void OnAsked(byte[] data)
        {
            if (!PlayerManager.IsMaster || !_bridge.CanSend) return;
            var reader = new BytesReader(data);
            string requester = reader.NextAutoString();
            string text = reader.NextAutoString();
            if (!Developer.Has(requester)) { Logger.Warn($"OnAsked() '{gameObject.name}' — '{requester}' has no developer access; refused"); return; }
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > MAX_LENGTH) text = text.Substring(0, MAX_LENGTH);

            var writer = new BytesWriter(sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(text));
            writer.AddAutoString(text);
            _bridge.SendToAll(SHOW_ID, writer.Data);
            Logger.Info($"OnAsked() '{gameObject.name}' — '{requester}' notified everyone: '{text}'");
        }
    }
}
