using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// This class receive network events from photon and can register network objects in the scene.
    /// There can only be one instance of this class in the scene.
    /// </summary>
    [HelpURL("https://incrediworlds.gitbook.io/somnium-space-dendoc/worlds-creation/community-modules/community-networking")]
    public class SceneNetworking : MonoBehaviour, INetworkRunnerCallbacks
    {
        // Static Public
        public static event Action OnNetworkRunnerCreated;
        public static event Action OnLocalPlayerJoined; // Event when local player has joined
        public static event Action OnBecomeWorldMaster; // Event when local player become shared mode master
        public static event Action<PlayerRef> OnOtherPlayerJoined;
        public static event Action<PlayerRef> OnOtherPlayerLeft;
        public static event Action<ReliableKey, byte[]> OnReliableMessageReceived; // Special message received from other player

        // Recommended flags for NetworkObjects
        public const NetworkObjectFlags NETWORK_OBJECT_DEFAULT_FLAGS = NetworkObjectFlags.V1 | NetworkObjectFlags.AllowStateAuthorityOverride;

        // Serialized Private
        [Header("Alpha V3.0 [2026, 05, 01]")]
        [Tooltip("Enable if you want NetworkObjects to transfer authority when host quit")]
        [SerializeField] private bool _autoTransferObjectsAuthority = true;
        [Header("Editor Testing Mode")]
        [Tooltip("Enable if you want to use real photon server in editor mode")]
        [SerializeField] private bool _editorConnectPhotonNetwork = false;
        [Header("Network Prefabs")]
        [SerializeField] private NetworkObject[] _networkPrefabs;
        [SerializeField] private string[] _networkPrefabsGuid;

        // Fields Private 
        private bool _wasMasterClient = false;
        private float _t0 = 0; // Time since start of the scene
        private NetworkObject[] _sceneNetworkObjects = new NetworkObject[0];
        private Guid[] _networkPrefabsGuidConverted;
        private Dictionary<NetworkObject, NetworkPrefabId> _networkPrefabsID = new();
        private List<(NetworkPrefabId, INetworkPrefabSource)> _prefabTableSnapshot = null; // DANGER, DO NOT CHANGE, COULD BREAKE SOMNIUM
        private bool _isNetworkRunnerSetup = false;

        // Properties Public 
        public static SceneNetworking Instance { get; private set; } // Singleton instance
        public static NetworkRunner NetworkRunnerRef { get; private set; }
        public static bool IsNetworkReady { get; private set; } = false; // Check if local player has joined
        public static bool IsConnected => NetworkRunnerRef != null || NetworkRunnerRef.IsRunning; // Networking is connected
        public static bool IsDebugMode => !IsConnected; // Networking is not connected

        public static bool IsMasterClient
        {
            get
            {
                if (NetworkRunnerRef == null)
                    return false;
                else
                    return NetworkRunnerRef.IsSharedModeMasterClient;
            }
        }

        public IReadOnlyDictionary<NetworkObject, NetworkPrefabId> NetworkPrefabs => _networkPrefabsID;
        public SceneRef NetworkSceneRef { get; private set; }

        private void Awake()
        {
            Instance = this;
            _t0 = Time.time;

            GetAllNetworkObjects();

            if (_editorConnectPhotonNetwork && Application.isEditor)
                Editor_CreateDebugNetworkRunner();
            
            InvokeRepeating(nameof(WaitForNetworkRunner), 0.0f, 0.05f);
        }

        private void OnDestroy()
        {
            RestorePrefabTable(); // Critical, do not remove
            Instance = null;
            NetworkRunnerRef = null;
            IsNetworkReady = false;
            OnNetworkRunnerCreated = null;
            OnLocalPlayerJoined = null;
            OnBecomeWorldMaster = null;
            OnOtherPlayerJoined = null;
            OnOtherPlayerLeft = null;
            OnReliableMessageReceived = null;
        }

        // Play mode, debug Network Runner
        private void Editor_CreateDebugNetworkRunner()
        {
            Log("Debug mode, create debug NetworkRunner");
            var runnerGO = new GameObject("NetworkRunner");
            NetworkRunnerRef = runnerGO.AddComponent<NetworkRunner>();
            NetworkRunnerSetup(NetworkRunnerRef);

            NetworkRunnerRef.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = "TestSession",
            });
        }

        private void RegisterNetworkPrefabs()
        {
            // DANGER, DO NOT CHANGE, COULD BREAKE SOMNIUM
            if (_prefabTableSnapshot == null)
                _prefabTableSnapshot = NetworkProjectConfig.Global.PrefabTable.GetEntries().ToList();
            // Prepare and validate prefabs list as well as Guid list
            if (!PrepareNetworkPrefabs())
            {
                Debug.LogError("[SceneNetworking] Network major error, prefab list in not conformal, check inspector config");
                return;
            }
            // Register network prefab from list
            _networkPrefabsID = new Dictionary<NetworkObject, NetworkPrefabId>();
            for (int n = 0; n < _networkPrefabs.Length; n ++)
            {
                var source = new NetworkPrefabSourceStatic
                {
                    Object = _networkPrefabs[n],
                    AssetGuid = _networkPrefabsGuidConverted[n]
                };
                if (NetworkProjectConfig.Global.PrefabTable.TryAddSource(source, out NetworkPrefabId prefabId))
                {
                    _networkPrefabsID[_networkPrefabs[n]] = prefabId;
                    Debug.Log($"[SceneNetworking] Registering network prefab: {_networkPrefabs[n].gameObject.name}");
                }
                else
                {
                    _networkPrefabsID.Clear();
                    RestorePrefabTable();
                    Debug.LogError($"[SceneNetworking] Failed to register network prefab: {_networkPrefabs[n].gameObject.name}");
                    return;
                }
            }
        }

        // DANGER, DO NOT CHANGE, COULD BREAKE SOMNIUM
        private void RestorePrefabTable()
        {
            if (_prefabTableSnapshot == null)
                return;

            // Recover original prefab table
            Debug.Log($"[SceneNetworking] Recover Prefab table");
            NetworkProjectConfig.Global.PrefabTable.Clear();
            foreach (var (id, source) in _prefabTableSnapshot)
            {
                Debug.Log($"[SceneNetworking] PrefabTable AddSource {source}");
                NetworkProjectConfig.Global.PrefabTable.AddSource(source);
            }
        }

        private void WaitForNetworkRunner()
        {
            if (NetworkRunner.Instances.Count > 0)
            {
                if (NetworkRunner.Instances[0].IsRunning)
                {
                    Log("NetworkRunner ready");
                    CancelInvoke(nameof(WaitForNetworkRunner));
                    NetworkRunnerRef = NetworkRunner.Instances[0];
                    if (!_isNetworkRunnerSetup)
                        NetworkRunnerSetup(NetworkRunnerRef);
                }
            }
        }

        private void NetworkRunnerSetup(NetworkRunner runner)
        {
            _isNetworkRunnerSetup = true;
            runner.AddCallbacks(this);
            RegisterNetworkPrefabs();
            OnNetworkRunnerCreated?.Invoke();
        }

        private void InitScenePath()
        {
            NetworkSceneRef = SceneRef.FromPath(gameObject.scene.path);
            if (!NetworkSceneRef.IsValid)
                LogError($"Scene reference is not valid ! {gameObject.scene.name}, {gameObject.scene.path}");
        }

        private void GetAllNetworkObjects()
        {
            _sceneNetworkObjects = FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(_sceneNetworkObjects, (a, b) => a.SortKey.CompareTo(b.SortKey));
            Log($"Scene Network Objects Found: {_sceneNetworkObjects.Length}");
        }

        private void RegisterAllNetworkObjects()
        {
            int r = NetworkRunnerRef.RegisterSceneObjects(NetworkSceneRef, _sceneNetworkObjects);
            Log($"Scene Network Objects registered: {r}");
        }

        private void ReassignNullObjectsAuthority()
        {
            foreach (NetworkObject obj in _sceneNetworkObjects)
            {
                if (obj != null)
                {
                    if (obj.StateAuthority.IsNone)
                    {
                        Log($"ReassignNullObjectsAuthority {obj.name}");
                        obj.RequestStateAuthority();
                    }
                }
            }
        }

        private void SlowLoop()
        {
            if (IsMasterClient)
            {
                ReassignNullObjectsAuthority();
            }
            UpdateMasterClientState();
        }

        private void UpdateMasterClientState()
        {
            if (IsMasterClient != _wasMasterClient)
            {
                _wasMasterClient = IsMasterClient;
                if (IsMasterClient)
                {
                    Log("You are now the Master Client");
                    SafeInvokeOnBecomeWorldMaster();
                }
                else
                {
                    Log("You are no longer the Master Client");
                }
            }
        }

        /// Used by photon, do not call.
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (player == NetworkRunnerRef.LocalPlayer)
            {
                Log($"Local Player Joined");
                Log($"Is Master Client={IsMasterClient}");
                InitScenePath();
                RegisterAllNetworkObjects();
                IsNetworkReady = true;

                SafeInvokeOnLocalPlayerJoined();
                UpdateMasterClientState();

                CancelInvoke(nameof(SlowLoop));
                InvokeRepeating(nameof(SlowLoop), 1.0f, 1.0f);
            }
            else
            {
                SafeInvokeOnOtherPlayerJoined(player);
                UpdateMasterClientState();
                Log($"Remote Player Joined, id={player.PlayerId}");
            }
        }

        /// Used by photon, do not call.
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Log($"OnPlayerLeft id:{player.PlayerId}");
            if (_autoTransferObjectsAuthority && IsMasterClient)
            {
                Log($"OnPlayerLeft Transferring authority to local");
                ReassignNullObjectsAuthority();
            }

            // Probably always true, but just in case
            if (player != NetworkRunnerRef.LocalPlayer)
            {
                SafeInvokeOnOtherPlayerLeft(player);
            }

            UpdateMasterClientState();
        }

        /// Used by photon, do not call.
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            if (OnReliableMessageReceived != null)
            {
                byte[] dataBytes = data.ToArray();
                foreach (Delegate d in OnReliableMessageReceived.GetInvocationList())
                {
                    try
                    { ((Action<ReliableKey, byte[]>)d)(key, dataBytes); }
                    catch (Exception ex)
                    { Debug.LogException(ex); }
                }
            }
        }

        /// Used by photon, do not call.
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            //Log("OnReliableDataProgress, " + GetTime());
        }

        /// Used by photon, do not call.
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            //Log($"Object EnterAOI {obj.name}");
        }

        /// Used by photon, do not call.
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
            //Log($"Object ExitAOI {obj.name}");
        }

        /// Used by photon, do not call.
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
            //Log($"UserSimulationMessage received");
        }

        /// Used by photon, do not call.
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            // Warning, called a lot !
        }

        /// Used by photon, do not call.
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {

        }

        /// Used by photon, do not call.
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Log($"OnShutdown");
        }

        /// Used by photon, do not call.
        public void OnConnectedToServer(NetworkRunner runner)
        {
            Log("Connected To Server, t=" + GetTime());
        }

        /// Used by photon, do not call.
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Log("Disconnected From Server, t=" + GetTime());
        }

        /// Used by photon, do not call.
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            Log("Connect Request, t=" + GetTime());
        }

        /// Used by photon, do not call.
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Log("Connect Failed, t=" + GetTime());
        }

        /// Used by photon, do not call.
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            Log("Session List Updated, t=" + GetTime());
        }

        /// Used by photon, do not call.
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
            Log("Custom Authentication Response, t=" + GetTime());
        }

        /// Used by photon, do not call.
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
            Log("Host Migration, t=" + GetTime());
        }

        /// Used by photon, do not call.
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            Log("Scene LoadDone, t=" + GetTime());
        }

        /// Used by photon, do not call.
        public void OnSceneLoadStart(NetworkRunner runner)
        {
            Log("Scene Load Start, t=" + GetTime());
        }

        private void Log(string message)
        {
            Debug.Log($"[World] [{nameof(SceneNetworking)}] (t:{GetTime()}) {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[World] [{nameof(SceneNetworking)}] (t:{GetTime()}) {message}");
        }

        private string GetTime()
        {
            return (Time.time - _t0).ToString("F2") + "s";
        }

        // Non breaking invoking
        private void SafeInvokeOnBecomeWorldMaster()
        {
            if (OnBecomeWorldMaster != null)
            {
                foreach (Delegate d in OnBecomeWorldMaster.GetInvocationList())
                {
                    try
                    { ((Action)d)(); }
                    catch (Exception ex)
                    { Debug.LogException(ex); }
                }
            }
        }

        // Error resistant invoking
        private void SafeInvokeOnLocalPlayerJoined()
        {
            if (OnLocalPlayerJoined != null)
            {
                foreach (Delegate d in OnLocalPlayerJoined.GetInvocationList())
                {
                    try
                    { ((Action)d)(); }
                    catch (Exception ex)
                    { Debug.LogException(ex); }
                }
            }
        }

        // Error resistant invoking
        private void SafeInvokeOnOtherPlayerJoined(PlayerRef player)
        {
            if (OnOtherPlayerJoined != null)
            {
                foreach (Delegate d in OnOtherPlayerJoined.GetInvocationList())
                {
                    try
                    { ((Action<PlayerRef>)d)(player); }
                    catch (Exception ex)
                    { Debug.LogException(ex); }
                }
            }
        }

        // Error resistant invoking
        private void SafeInvokeOnOtherPlayerLeft(PlayerRef player)
        {
            if (OnOtherPlayerLeft != null)
            {
                foreach (Delegate d in OnOtherPlayerLeft.GetInvocationList())
                {
                    try
                    { ((Action<PlayerRef>)d)(player); }
                    catch (Exception ex)
                    { Debug.LogException(ex); }
                }
            }
        }

        // Assure all network prefab are conformal, and prepare the GUID list
        private bool PrepareNetworkPrefabs()
        {
            // Check if Network prefabs and guid list are same size
            if (_networkPrefabsGuid.Length != _networkPrefabs.Length)
            {
                Debug.LogError($"[SceneNetworking] ValidateNetworkPrefabs, Prefab list and Guid list size do not match, check Inspector config");
                return false;
            }
            // Check if no network prefabs are null
            foreach (NetworkObject no in _networkPrefabs)
            {
                if (no == null)
                {
                    Debug.LogError($"[SceneNetworking] ValidateNetworkPrefabs, Null network prefab, check Inspector config");
                    return false;
                }
            }
            // Check if all Guid string are valid
            _networkPrefabsGuidConverted = new Guid[_networkPrefabsGuid.Length];
            for (int n = 0; n < _networkPrefabsGuid.Length; n++)
            {
                if (!Guid.TryParse(_networkPrefabsGuid[n], out Guid guid) || guid == Guid.Empty)
                {
                    Debug.LogError($"[SceneNetworking] ValidateNetworkPrefabs, A network prefab GUID is invalid, check Inspector config");
                    return false;
                }
                _networkPrefabsGuidConverted[n] = guid;
            }
            // Check if there is no duplicated guid
            HashSet<Guid> guidHashSet = new(_networkPrefabsGuidConverted);
            if (guidHashSet.Count != _networkPrefabsGuidConverted.Length)
            {
                Debug.LogError("[SceneNetworking] ValidateNetworkPrefabs, duplicated network prefab guid found, check Inspector config");
                return false;
            }
            // Check if there is no duplicated prefabs
            HashSet<NetworkObject> networkPrefabH = new(_networkPrefabs);
            if (networkPrefabH.Count != _networkPrefabs.Length)
            {
                Debug.LogError("[SceneNetworking] ValidateNetworkPrefabs, duplicated network prefab found, check Inspector config");
                return false;
            }

            return true;
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Validate network prefab list
            bool isDirty = false;
            HashSet<NetworkObject> validPrefabs = new();
            foreach (NetworkObject no in _networkPrefabs)
            {
                // Runtime-safe equivalent of PrefabUtility.IsPartOfPrefabAsset(no): a prefab
                // asset has no valid scene, a scene instance does. The editor-namespace call it
                // replaces was rejected by the Somnium uploader, which scans this assembly for
                // editor-only code — and the #if guard does not help, since the Editor-compiled
                // assembly still carries the reference.
                if (no != null && !no.gameObject.scene.IsValid())
                {
                    validPrefabs.Add(no);
                }
            }
            if (_networkPrefabs.Length != validPrefabs.Count)
            {
                _networkPrefabs = validPrefabs.ToArray();
                isDirty = true;
            }

            // Regenerate GUID list
            if (isDirty || _networkPrefabsGuid.Length != _networkPrefabs.Length)
            {
                isDirty = false;
                _networkPrefabsGuid = new string[_networkPrefabs.Length];
                for (int n = 0; n < _networkPrefabsGuid.Length; n++)
                {
                    _networkPrefabsGuid[n] = Guid.NewGuid().ToString();
                }
            }

            // Validate GUID list
            for (int n = 0; n < _networkPrefabsGuid.Length; n++)
            {
                string guid = _networkPrefabsGuid[n];
                if (!Guid.TryParse(guid, out Guid _))
                {
                    _networkPrefabsGuid[n] = Guid.NewGuid().ToString();
                }
            }
        }
        #endif
    }
}