/*
* Copyright (c) 2021 PlayEveryWare
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.Collections.Generic;

using UnityEngine;

using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.AntiCheatCommon;
using Epic.OnlineServices.AntiCheatClient;

namespace PlayEveryWare.EpicOnlineServices.Samples
{
    /// <summary>
    /// Class <c>EOSAntiCheatClientManager</c> is a simplified wrapper for EOS [AntiCheat Client Interface](https://dev.epicgames.com/docs/services/en-US/GameServices/AntiCheat/index.html).
    /// </summary>
    public class EOSAntiCheatClientManager : IEOSSubManager, IEOSOnConnectLogin, IEOSOnAuthLogin, IEOSOnAuthLogout
    {
        /// <summary>
        /// Indicates the mode in which the anti-cheat client manager is functioning.
        /// TODO: Why do we need this? The value is never *NOT* P2P
        /// </summary>
        public enum PeerToPeerMode
        {
            P2P,
            ClientServer
        };

        /// <summary>
        /// The mode in which the Anti-Cheat client manager is functioning.
        /// TODO: Why do we need PeerToPeerMode.ClientServer? It's never utilized.
        /// </summary>
        private PeerToPeerMode _mode = PeerToPeerMode.P2P;

        // TODO: Code pruning
        //public delegate void OnAntiCheatClientCallback(Result result);

        /// <summary>
        /// Cached copy of the local user id token.
        /// </summary>
        private IdToken? _localUserIdToken = null;

        /// <summary>
        /// Indicates whether a session is active or not.
        /// </summary>
        private bool _sessionActive = false;

        /// <summary>
        /// Used to store a list of registered peers.
        /// TODO: The data that exists within this list should always also be represented in the _registeredPeerMapping
        ///       field; figure out a way to only use the mapping instead of the list.
        /// OLD NOTES:
        /// - peer handles represent the index into this list.
        /// - in a real use case handles would ideally represent a pointer to player data or object.
        /// </summary>
        private List<ProductUserId> _registeredPeerList = new()
        {
            //avoid registering peer with zero index
            // TODO: What?
            null
        };

        /// <summary>
        /// Maps ProductUserId to the index of the ProductUserId within _registeredPeerList
        /// TODO: Evaluate why this is needed, it seems very extra. 
        /// </summary>
        private Dictionary<ProductUserId, int> _registeredPeerMapping = new();

        /// <summary>
        /// List to keep track of all callbacks for integrity violations.
        /// </summary>
        private List<OnClientIntegrityViolatedCallback> _clientIntegrityViolatedCallbacks = new();

        /// <summary>
        /// List to keep track of all message to peer callbacks.
        /// </summary>
        private List<OnMessageToPeerCallback> _messageToPeerCallbacks = new();

        /// <summary>
        /// List to keep track of all the peer auth status change callbacks.
        /// </summary>
        private List<OnPeerAuthStatusChangedCallback> _peerAuthStatusChangedCallbacks = new();

        /// <summary>
        /// List to keep track of all the peer action required callbacks.
        /// </summary>
        private List<OnPeerActionRequiredCallback> _peerActionRequiredCallbacks = new();
        

        public EOSAntiCheatClientManager()
        {
            if (AntiCheatClientHandle == null)
            {
                Debug.LogWarning("AntiCheatManager: Unable to get handle to AntiCheatClientInterface.");
                return;
            }

            var notifyIntegrityOptions = new AddNotifyClientIntegrityViolatedOptions();
            AntiCheatClientHandle.AddNotifyClientIntegrityViolated(ref notifyIntegrityOptions, null, OnClientIntegrityViolated);

            switch (_mode)
            {
                case PeerToPeerMode.P2P:
                    var messageOptions = new AddNotifyMessageToPeerOptions();
                    AntiCheatClientHandle.AddNotifyMessageToPeer(ref messageOptions, null, OnMessageToPeer);

                    var authStatusOptions = new AddNotifyPeerAuthStatusChangedOptions();
                    AntiCheatClientHandle.AddNotifyPeerAuthStatusChanged(ref authStatusOptions, null, OnPeerAuthStatusChanged);

                    var peerActionOptions = new AddNotifyPeerActionRequiredOptions();
                    AntiCheatClientHandle.AddNotifyPeerActionRequired(ref peerActionOptions, null, OnPeerActionRequired);
                    break;
                case PeerToPeerMode.ClientServer:
                    // TODO: AddNotifyMessageToServer when using ClientServer mode.
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            CacheAndVerifyLocalIdToken(ref _localUserIdToken);
        }

        #region LocalIdToken Cache & Verification

        /// <summary>
        /// Cache the ProductUserId, and verify it.
        /// </summary>
        /// <param name="userIdToken">Reference to the token to cache and verify.</param>
        private static void CacheAndVerifyLocalIdToken(ref IdToken? userIdToken)
        {
            if (EOSManager.Instance.GetProductUserId() == null)
            {
                Debug.LogWarning($"Could not get product user id");
                return;
            }

            CacheLocalIdToken(out userIdToken);
            VerifyIdToken(userIdToken);
        }

        /// <summary>
        /// Gets the ProductUserId, caching it in the given out variable.
        /// </summary>
        /// <param name="localUserIdToken">The ProductUserId.</param>
        private static void CacheLocalIdToken(out IdToken? localUserIdToken)
        {
            CopyIdTokenOptions options = new()
            {
                LocalUserId = EOSManager.Instance.GetProductUserId()
            };
            var result = ConnectHandle.CopyIdToken(ref options, out localUserIdToken);

            if (result != Result.Success)
            {
                Debug.LogError("AntiCheatClient (CacheLocalIdToken): failed to copy local user id token");
            }
        }

        //used to verify an ID token
        //currently does not successfully get platform or device type information
        private static void VerifyIdToken(IdToken? token, OnVerifyIdTokenCallback callback = null)
        {
            if (token == null)
            {
                Debug.LogError("AntiCheatClient (VerifyIdToken): id token is null");
                return;
            }

            VerifyIdTokenOptions options = new() { IdToken = token };

            // Assign default callback for logging purposes if one is not provided.
            callback ??= (ref VerifyIdTokenCallbackInfo data) =>
            {
                Debug.LogFormat(
                    $"AntiCheatClient (VerifyIdToken): Result: {data.ResultCode}, Platform:\"{data.Platform}\" DeviceType:\"{data.DeviceType}\"");
            };

            ConnectHandle.VerifyIdToken(ref options, null, callback);
        }

        #endregion

        /// <summary>
        /// Helper property to simplify references to the AntiCheatClientInterface.
        /// </summary>
        private static AntiCheatClientInterface AntiCheatClientHandle
        {
            get
            {
                var antiCheatHandle = EOSManager.Instance.GetEOSPlatformInterface().GetAntiCheatClientInterface();
                if (antiCheatHandle == null)
                {
                    Debug.LogWarning($"Could not get AntiCheatClientInterface handle.");
                }
                return antiCheatHandle;
            }
        }

        /// <summary>
        /// Helper property to simplify references to the ConnectInterface.
        /// </summary>
        private static ConnectInterface ConnectHandle
        {
            get
            {
                var connectHandle = EOSManager.Instance.GetEOSPlatformInterface().GetConnectInterface();
                if (connectHandle == null)
                {
                    Debug.LogWarning($"Could not get ConnectInterface handle.");
                }
                return connectHandle;
            }
        }

        /// <summary>
        /// Check if EAC functionality is availble
        /// </summary>
        /// <returns>False if EAC client functionality is not available i.e. game was launched without EAC bootstrapper</returns>
        public bool IsAntiCheatAvailable()
        {
            return AntiCheatClientHandle != null;
        }

        #region Login / Logout Callbacks

        public void OnConnectLogin(LoginCallbackInfo loginCallbackInfo)
        {
            CacheAndVerifyLocalIdToken(ref _localUserIdToken);
        }

        public void OnAuthLogin(Epic.OnlineServices.Auth.LoginCallbackInfo loginCallbackInfo)
        {
            CacheAndVerifyLocalIdToken(ref _localUserIdToken);
        }

        public void OnAuthLogout(Epic.OnlineServices.Auth.LogoutCallbackInfo logoutCallbackInfo)
        {
            _localUserIdToken = null;
        }

        #endregion

        // TODO: Provide usage examples in sample scenes such that all callbacks are tested.
        #region Management for Callbacks related uniquely to EAC

        private void OnClientIntegrityViolated(ref OnClientIntegrityViolatedCallbackInfo data)
        {
            Debug.LogErrorFormat("AntiCheatClient (OnClientIntegrityViolated): Type:{0}, Message:\"{1}\"", data.ViolationType, data.ViolationMessage);
            foreach (var callback in _clientIntegrityViolatedCallbacks)
            {
                callback?.Invoke(ref data);
            }
        }

        /// <summary>
        /// Use to access functionality of [EOS_AntiCheatClient_AddNotifyClientIntegrityViolated](https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_AddNotifyClientIntegrityViolated/index.html)
        /// </summary>
        /// <param name="Callback">Callback to receive notification of client integrity violation (modification of program memory or protected files, etc.</param>
        public void AddNotifyClientIntegrityViolated(OnClientIntegrityViolatedCallback Callback)
        {
            _clientIntegrityViolatedCallbacks.Add(Callback);
        }

        public void RemoveNotifyClientIntegrityViolated(OnClientIntegrityViolatedCallback Callback)
        {
            _clientIntegrityViolatedCallbacks.Remove(Callback);
        }

        private void OnMessageToPeer(ref OnMessageToClientCallbackInfo data)
        {
            Debug.Log("AntiCheatClient (OnMessageToPeer)");
            foreach (var callback in _messageToPeerCallbacks)
            {
                callback?.Invoke(ref data);
            }
        }

        /// <summary>
        /// Use to access functionality of [EOS_AntiCheatClient_AddNotifyMessageToPeer](https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_AddNotifyMessageToPeer/index.html)
        /// </summary>
        /// <param name="Callback">Callback to receive message data to send to peer</param>
        public void AddNotifyToMessageToPeer(OnMessageToPeerCallback Callback)
        {
            _messageToPeerCallbacks.Add(Callback);
        }

        public void RemoveNotifyMessageToPeer(OnMessageToPeerCallback Callback)
        {
            _messageToPeerCallbacks.Remove(Callback);
        }

        /// <summary>
        /// Wrapper for calling [EOS_AntiCheatClient_ReceiveMessageFromPeer]https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_ReceiveMessageFromPeer/index.html)
        /// </summary>
        /// <param name="PeerHandle"><c>IntPtr</c> referencing another player</param>
        /// <param name="Data"><c>ArraySegment&lt;byte&gt;</c> previously received from <c>AddNotifyToMessageToPeer</c> callback</param>
        public void ReceiveMessageFromPeer(IntPtr PeerHandle, ArraySegment<byte> Data)
        {
            var options = new ReceiveMessageFromPeerOptions()
            {
                PeerHandle = PeerHandle,
                Data = Data
            };

            if (AntiCheatClientHandle != null)
            {
                var result = AntiCheatClientHandle.ReceiveMessageFromPeer(ref options);

                if (result != Result.Success)
                {
                    Debug.LogErrorFormat("AntiCheatClient (ReceiveMessageFromPeer): result code: {0}", result);
                }
            }
        }

        private void OnPeerAuthStatusChanged(ref OnClientAuthStatusChangedCallbackInfo data)
        {
            Debug.LogFormat("AntiCheatClient (OnPeerAuthStatusChanged): handle: {0}, status: {1}", data.ClientHandle.ToInt32(), data.ClientAuthStatus);
            foreach (var callback in _peerAuthStatusChangedCallbacks)
            {
                callback?.Invoke(ref data);
            }
        }

        /// <summary>
        /// Use to access functionality of [EOS_AntiCheatClient_AddNotifyPeerAuthStatusChanged](https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_AddNotifyPeerAuthStatusChanged/index.html)
        /// </summary>
        /// <param name="Callback">Callback to receive notification when peer auth is complete</param>
        public void AddNotifyPeerAuthStatusChanged(OnPeerAuthStatusChangedCallback Callback)
        {
            _peerAuthStatusChangedCallbacks.Add(Callback);
        }

        public void RemoveNotifyPeerAuthStatusChanged(OnPeerAuthStatusChangedCallback Callback)
        {
            _peerAuthStatusChangedCallbacks.Remove(Callback);
        }

        private void OnPeerActionRequired(ref OnClientActionRequiredCallbackInfo data)
        {
            Debug.Log("AntiCheatClient (OnPeerActionRequired)");
            foreach (var callback in _peerActionRequiredCallbacks)
            {
                callback?.Invoke(ref data);
            }
        }

        /// <summary>
        /// Use to access functionality of [EOS_AntiCheatClient_AddNotifyPeerActionRequired](https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_AddNotifyPeerActionRequired/index.html)
        /// </summary>
        /// <param name="Callback">Callback to receive notification about action required for a peer (usually removal from the session)</param>
        public void AddNotifyPeerActionRequired(OnPeerActionRequiredCallback Callback)
        {
            _peerActionRequiredCallbacks.Add(Callback);
        }

        public void RemoveNotifyPeerActionRequired(OnPeerActionRequiredCallback Callback)
        {
            _peerActionRequiredCallbacks.Remove(Callback);
        }

        #endregion

        #region Peer Management

        /// <summary>
        /// Get <c>ProductUserId</c> of registered peer by index
        /// </summary>
        /// <returns><c>ProductUserId</c> of peer, or null if not registered</returns>
        public ProductUserId GetPeerId(IntPtr peerHandle)
        {
            int peerIndex = peerHandle.ToInt32();
            return peerIndex < _registeredPeerList.Count ? _registeredPeerList[peerIndex] : null;
        }

        /// <summary>
        /// Get index of registered peer by <c>ProductUserId</c> as <c>IntPtr</c>
        /// </summary>
        /// <returns><c>IntPtr</c>representation of peer index for use with <c>GetPeerId</c> or <c>ReceiveMessageFromPeer</c></returns>
        public IntPtr GetPeerHandle(ProductUserId userId)
        {
            _registeredPeerMapping.TryGetValue(userId, out int handle);
            return new IntPtr(handle);
        }

        /// <summary>
        /// Wrapper for calling [EOS_AntiCheatClient_RegisterPeer](https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_RegisterPeer/index.html)
        /// TODO: Use ID tokens to verify player platform to determine whether their client must be protected or not (console players would use UnprotectedClient)
        /// </summary>
        /// <param name="userId"><c>ProductUserId</c> of peer to register</param>
        /// <returns>True if peer was successfully registered, or was already registered</returns>
        public bool RegisterPeer(ProductUserId userId)
        {
            if (_registeredPeerMapping.ContainsKey(userId))
            {
                Debug.LogWarning("AntiCheatClient (RegisterPeer): peer already registered");
                return true;
            }

            int peerIndex = _registeredPeerList.Count;
            var options = new RegisterPeerOptions()
            {
                PeerHandle = new IntPtr(peerIndex),
                //TODO: get platform and protection status with connect interface
                ClientType = AntiCheatCommonClientType.ProtectedClient,
                ClientPlatform = AntiCheatCommonClientPlatform.Windows,
                AuthenticationTimeout = 60,
                PeerProductUserId = userId
            };
            var result = AntiCheatClientHandle.RegisterPeer(ref options);
            if (result == Result.Success)
            {
                // TODO: This is wonky - storing the index of a value in another list as the value
                //       to the key of the user id? Confusing.
                _registeredPeerMapping[userId] = peerIndex;

                Debug.Log("AntiCheatClient (RegisterPeer): successfully registered peer");
                return true;
            }
            else
            {
                Debug.LogFormat("AntiCheatClient (RegisterPeer): failed to register peer, result code: {0}", result);
                return false;
            }
        }

        /// <summary>
        /// Wrapper for calling [EOS_AntiCheatClient_UnregisterPeer](https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_UnregisterPeer/index.html)
        /// </summary>
        /// <param name="userId"><c>ProductUserId</c> of peer to unregister</param>
        /// <returns>True if peer was successfully unregistered, or was already not registered</returns>
        public bool UnregisterPeer(ProductUserId userId)
        {
            if (!_registeredPeerMapping.TryGetValue(userId, out int peerIndex))
            {
                Debug.LogWarning("AntiCheatClient (UnregisterPeer): peer not registered");
                return true;
            }

            var options = new UnregisterPeerOptions()
            {
                PeerHandle = new IntPtr(peerIndex)
            };
            var result = AntiCheatClientHandle.UnregisterPeer(ref options);
            if (result == Result.Success)
            {
                _registeredPeerMapping.Remove(userId);
                _registeredPeerList[peerIndex] = null;

                Debug.Log("AntiCheatClient (RegisterPeer): successfully unregistered peer");
                return true;
            }
            else
            {
                Debug.LogFormat("AntiCheatClient (RegisterPeer): failed to unregister peer, result code: {0}", result);
                return false;
            }
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Wrapper for calling [EOS_AntiCheatClient_BeginSession](https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_BeginSession/index.html)
        /// TODO: Add support for client-server mode
        /// </summary>
        public void BeginSession()
        {
            if (_sessionActive)
            {
                Debug.LogErrorFormat("AntiCheatClient (BeginSession): session already active");
                return;
            }

            var options = new BeginSessionOptions()
            {
                LocalUserId = EOSManager.Instance.GetProductUserId(),
                Mode = AntiCheatClientMode.PeerToPeer
            };
            var result = AntiCheatClientHandle.BeginSession(ref options);
            if (result != Result.Success)
            {
                Debug.LogErrorFormat("AntiCheatClient (BeginSession): failed to begin session, result code: {0}", result);
            }
            else
            {
                _sessionActive = true;
            }
        }

        /// <summary>
        /// Wrapper for calling [EOS_AntiCheatClient_EndSession](https://dev.epicgames.com/docs/services/en-US/API/Members/Functions/AntiCheatClient/EOS_AntiCheatClient_EndSession/index.html)
        /// </summary>
        public void EndSession()
        {
            if (!_sessionActive)
            {
                Debug.LogErrorFormat("AntiCheatClient (BeginSession): session not active");
                return;
            }

            var options = new EndSessionOptions();
            var result = AntiCheatClientHandle.EndSession(ref options);

            if (result != Result.Success)
            {
                Debug.LogErrorFormat("AntiCheatClient (EndSession): failed to end session, result code: {0}", result);
            }
            else
            {
                _sessionActive = false;
            }
        }

        /// <summary>
        /// Checks if a protected EAC sessionis active
        /// </summary>
        /// <returns>True if session is active</returns>
        public bool IsSessionActive()
        {
            return _sessionActive;
        }

        #endregion
    }
}