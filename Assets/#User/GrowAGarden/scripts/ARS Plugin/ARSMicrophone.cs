using Photon.Voice.Unity;
using SomniumSpace.Bridge.Components;
using SomniumSpace.Bridge.Player;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrowAGarden
{
    public class ARSMicrophone : MonoBehaviour
    {
        public event Action<AudioSource> OnAudioSourceChanged;

        private List<ISomniumPlayer> _amplifiedPlayers = new();

        public ISomniumPlayer AmplifiedPlayer { get; private set; }
        public AudioSource LocalAudioSource { get; private set; }
        public AudioSource AudioSource { get; private set; }
        private bool _isPlayerPresent;

        void Start()
        {
            InvokeRepeating(nameof(SlowUpdate), 1.0f, 1.0f);
            LocalAudioSource = GetLocalPlayerAudioSource();
        }

        private void SlowUpdate()
        {
            CheclNullPlayers();
        }

        public void PlayerEnter(SomniumTriggerActionArgs args)
        {
            ISomniumPlayer player = args.Player;
            Debug.Log($"[{nameof(ARSMicrophone)}][{nameof(PlayerEnter)}] Player enter, Name:{player.Properties.NickName}, ID:{player.Properties.Id}, IsLocal:{player.Properties.IsLocal}");

            if (!_amplifiedPlayers.Contains(player))
            {
                _amplifiedPlayers.Add(player);
                CheclNullPlayers();
                UpdateEmplifiedPlayer();
            }
        }

        public void PlayerExit(SomniumTriggerActionArgs args)
        {
            ISomniumPlayer player = args.Player;
            Debug.Log($"[{nameof(ARSMicrophone)}][{nameof(PlayerExit)}] Player exit, Name:{player.Properties.NickName}, ID:{player.Properties.Id}, IsLocal:{player.Properties.IsLocal}");
            if (_amplifiedPlayers.Contains(player))
            {
                _amplifiedPlayers.Remove(player);
                CheclNullPlayers();
                UpdateEmplifiedPlayer();
            }
        }

        private void CheclNullPlayers()
        {
            _amplifiedPlayers.RemoveAll(item => item == null);

            if (_isPlayerPresent)
            {
                if (AmplifiedPlayer == null || AudioSource == null)
                {
                    Debug.Log($"[{nameof(ARSMicrophone)}] Null player detected");
                    try
                    {
                        bool deleted = _amplifiedPlayers.Remove(AmplifiedPlayer);
                        if (deleted)
                            Debug.LogError($"[{nameof(ARSMicrophone)}] Delete player success");
                        else
                            Debug.LogError($"[{nameof(ARSMicrophone)}] Delete player failed");

                        List<ISomniumPlayer> toDelete = new();
                        foreach (var player in _amplifiedPlayers)
                        {
                            if (player == null || player?.References?.Body?.Root == null)
                            {
                                toDelete.Add(player);
                            }
                        }
                        foreach (var player in toDelete)
                        {
                            bool deleted2 = _amplifiedPlayers.Remove(player);
                            if(deleted2)
                                Debug.LogError($"[{nameof(ARSMicrophone)}] Delete player success");
                            else
                                Debug.LogError($"[{nameof(ARSMicrophone)}] Delete player failed");
                        }
                    }
                    catch (Exception ex) 
                    {
                        Debug.LogError($"[{nameof(ARSMicrophone)}] Error cleaning Null player");
                    }
                    AudioSource = null;
                    AmplifiedPlayer = null;
                    _isPlayerPresent = false;
                    UpdateEmplifiedPlayer();
                }
            }
        }

        private void UpdateEmplifiedPlayer()
        {
            // Check if list empty
            if (_amplifiedPlayers.Count == 0) // No player, don't amplify
            {
                AudioSource = null;
                AmplifiedPlayer = null;
                _isPlayerPresent = false;
                OnAudioSourceChanged?.Invoke(null);
                Debug.Log($"[{nameof(ARSMicrophone)}][UpdateEmplifiedPlayer] Now no player is amplified");
                return;
            }

            // Check if amplified player changed
            if (AmplifiedPlayer != _amplifiedPlayers[0])
            {
                AmplifiedPlayer = _amplifiedPlayers[0];
                _isPlayerPresent = true;
                if (AmplifiedPlayer.Properties.IsLocal)
                {
                    if(LocalAudioSource == null)
                        LocalAudioSource = GetLocalPlayerAudioSource();
                    if (LocalAudioSource == null)
                    {
                        Debug.LogError($"[{nameof(ARSMicrophone)}][UpdateEmplifiedPlayer] Cant find local player audio source");
                        AudioSource = null;
                        AmplifiedPlayer = null;
                        _isPlayerPresent = false;
                        OnAudioSourceChanged?.Invoke(null);
                        return;
                    }

                    Debug.Log($"[{nameof(ARSMicrophone)}][UpdateEmplifiedPlayer] Set amplified player to Local");

                    AudioSource = LocalAudioSource;
                    OnAudioSourceChanged?.Invoke(AudioSource);
                }
                else
                {
                    AudioSource audioSource = GetRemotePlayerAudioSource(AmplifiedPlayer);
                    if (audioSource == null)
                    {
                        Debug.LogError($"[{nameof(ARSMicrophone)}] Cant find player audio source:{AmplifiedPlayer.Properties.NickName}, ID:{AmplifiedPlayer.Properties.Id}, IsLocal:{AmplifiedPlayer.Properties.IsLocal}");
                        AudioSource = null;
                        AmplifiedPlayer = null;
                        _isPlayerPresent = false;
                        OnAudioSourceChanged?.Invoke(null);
                        return;
                    }
                    else
                    {
                        Debug.Log($"[{nameof(ARSMicrophone)}] Setting amplified player:{AmplifiedPlayer.Properties.NickName}, ID:{AmplifiedPlayer.Properties.Id}, IsLocal:{AmplifiedPlayer.Properties.IsLocal}");
                        AudioSource = audioSource;
                        OnAudioSourceChanged?.Invoke(AudioSource);
                    }
                }
            }
        }

        private AudioSource GetRemotePlayerAudioSource(ISomniumPlayer player)
        {
            if (player == null)
            {
                Debug.LogError($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] ISomniumPlayer is null");
                return null;
            }

            Transform root = player.References.Body.Root.transform.root;

            if (root == null)
            {
                Debug.LogError($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] Player Root is null");
                return null;
            }

            string name = root.name; // E.g. Simulator Player: [blibalbou] [822] [Player:2]
            Debug.Log($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] Root found: {name}");

            int photonPlayerId = GetPlayerIDInText(name);

            if (photonPlayerId == -1)
            {
                Debug.LogError($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] Failed to find player photon ID");
                return null;
            }

            Debug.Log($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] Player photon ID found: {photonPlayerId}");

            // Find network agent
            GameObject playerNetworkAgent = null;
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject obj in rootObjects)
            {
                string objName = obj.name;
                if (objName.StartsWith("Remote Player Network Agent:"))
                {
                    int playerID = GetPlayerIDInText(objName);
                    if (playerID == -1)
                    {
                        Debug.LogError($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] Error getting player PhotonID");
                    }
                    if (playerID == photonPlayerId)
                    {
                        Debug.Log($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] NetworkAgent found !");
                        playerNetworkAgent = obj;
                        break;
                    }
                }
            }

            if (playerNetworkAgent == null)
            {
                Debug.LogError($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] No NetworkAgent found");
                return null;
            }

            AudioSource playerAudioSource = playerNetworkAgent.GetComponentInChildren<AudioSource>();

            if (playerNetworkAgent == null)
            {
                Debug.LogError($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] No AudioSource found");
                return null;
            }

            Debug.Log($"[{nameof(ARSMicrophone)}][GetPlayerAudioSource] AudioSource found: {playerAudioSource.name}");
            return playerAudioSource;
        }

        private AudioSource GetLocalPlayerAudioSource()
        {
            Recorder recorder = FindObjectOfType<Recorder>();
            if (recorder == null)
            {
                Debug.LogError($"[{nameof(ARSMicrophone)}][GetLocalPlayerAudioSource] Can't find Local Recorder");
                return null;
            }

            Debug.Log($"[{nameof(ARSMicrophone)}][GetLocalPlayerAudioSource] Local Recorder found");

            AudioSource audioSource = recorder.GetComponentInChildren<AudioSource>();

            if (audioSource == null)
            {
                Debug.LogError($"[{nameof(ARSMicrophone)}][GetLocalPlayerAudioSource] Can't find Local AudioSource");
                return null;
            }

            Debug.Log($"[{nameof(ARSMicrophone)}][GetLocalPlayerAudioSource] Local AudioSource found");

            return audioSource;
        }

        private int GetPlayerIDInText(string name)
        {
            Debug.Log($"[{nameof(ARSMicrophone)}][GetPlayerIDInText] Root name : {name}");
            var match = Regex.Match(name, @"Player:(\d+)");
            if (match.Success)
            {
                if (match.Groups.Count >= 2)
                {
                    int photonPlayerID = int.Parse(match.Groups[1].Value);
                    Debug.Log($"[{nameof(ARSMicrophone)}][GetPlayerIDInText] Photon player is: {photonPlayerID}");
                    return photonPlayerID;
                }
                else
                {
                    Debug.LogError($"[{nameof(ARSMicrophone)}][GetPlayerIDInText, 1] Cant find Player:#");
                    return -1;
                }
            }
            else 
            {
                Debug.LogError($"[{nameof(ARSMicrophone)}][GetPlayerIDInText, 2] Cant find Player:#");
                return -1;
            }
        }
    }
}
