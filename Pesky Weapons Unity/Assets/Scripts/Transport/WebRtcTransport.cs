using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Pesky.Transport
{
    /// <summary>
    /// The WebGL transport: After Hours' NetBridge, renamed. Owns the C# side of the
    /// AHNet.jslib seam (DllImports out, SendMessage receivers in), the connection flags
    /// and the PeerIds list kept from join/leave. It lives on a GameObject named exactly
    /// "NetBridge": net.js delivers with SendMessage("NetBridge", ...), and a wrong name
    /// is a silent no-op. Payloads cross the boundary base64-encoded.
    ///
    /// Threading contract: Ready, Error, PeerJoined, PeerLeft, Message, Activity and
    /// VoiceError fire from inside a SendMessage call, which on WebGL runs in a
    /// trystero / WebRTC / timer callback OUTSIDE the Unity frame. Listeners must only
    /// enqueue; never touch scene state or the sim from them.
    /// </summary>
    public sealed class WebRtcTransport : MonoBehaviour, INetTransport, IVoiceControl
    {
        public const string GameObjectName = "NetBridge";

        public static WebRtcTransport Instance { get; private set; }

        readonly List<string> _peerIds = new List<string>(8);

        public string SelfId { get; private set; } = "";
        public string RoomCode { get; private set; } = "";
        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public IReadOnlyList<string> PeerIds => _peerIds;

        public event Action<string> Ready;           // selfId
        public event Action<string> Error;           // message
        public event Action<string> PeerJoined;      // peerId
        public event Action<string> PeerLeft;        // peerId
        public event Action<string, byte[]> Message; // peerId, payload
        public event Action<bool> Activity;          // local mic above/below the VAD gate
        public event Action<string> VoiceError;      // "MIC BLOCKED"

        // IVoiceControl.Error is the voice error, not the network one.
        event Action<string> IVoiceControl.Error
        {
            add => VoiceError += value;
            remove => VoiceError -= value;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void AHNet_Start(string roomCode, bool isHost);
        [DllImport("__Internal")] static extern void AHNet_Leave();
        [DllImport("__Internal")] static extern void AHNet_Broadcast(string payloadB64);
        [DllImport("__Internal")] static extern void AHNet_SendTo(string peerId, string payloadB64);
        [DllImport("__Internal")] static extern string AHNet_GetSelfId();
        [DllImport("__Internal")] static extern void AHNet_UnityReady();
        [DllImport("__Internal")] static extern void AHNet_VoiceSetMode(int mode);
        [DllImport("__Internal")] static extern void AHNet_VoiceSetPeerVolume(string peerId, float gain);
        [DllImport("__Internal")] static extern void AHNet_VoiceSetThreshold(float dbfs);
#endif

        /// <summary>
        /// Spawns the DontDestroyOnLoad GameObject named NetBridge and returns its
        /// transport, or the existing one. TransportFactory is the only intended caller.
        /// </summary>
        public static WebRtcTransport Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(GameObjectName);
            return go.AddComponent<WebRtcTransport>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Assert(gameObject.name == GameObjectName,
                "[AHNet] GameObject must be named exactly 'NetBridge' or SendMessage from JS silently no-ops");
#if UNITY_WEBGL && !UNITY_EDITOR
            // Tells net.js it can stop buffering and start delivering events. Done here
            // rather than in a Start() message so the interface's Start(room, isHost) is
            // the only method of that name.
            AHNet_UnityReady();
#endif
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---- INetTransport ----

        // Explicit so Unity's MonoBehaviour scan does not see a "Start" with parameters
        // (it logs "Start() can not take parameters" otherwise). Callers hold INetTransport.
        void INetTransport.Start(string roomCode, bool isHost) => Connect(roomCode, isHost);

        /// <summary>Joins the room through net.js; INetTransport.Start forwards here.</summary>
        public void Connect(string roomCode, bool isHost)
        {
            RoomCode = (roomCode ?? "").ToUpperInvariant();
            IsHost = isHost;
            _peerIds.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
            AHNet_Start(RoomCode, isHost);
#else
            // There is no editor stub here any more: TransportFactory hands out a
            // LoopbackTransport outside WebGL. Say so loudly instead of hanging.
            Debug.LogWarning($"[AHNet] WebRtcTransport only works in a WebGL player (room={RoomCode} isHost={isHost}); use LoopbackTransport");
            OnNetError("WebRtcTransport is WebGL-only");
#endif
        }

        public void Leave()
        {
            IsConnected = false;
            _peerIds.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
            AHNet_Leave();
#endif
        }

        public void Broadcast(byte[] payload)
        {
            if (!IsConnected || payload == null) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            AHNet_Broadcast(Convert.ToBase64String(payload));
#endif
        }

        public void SendTo(string peerId, byte[] payload)
        {
            if (!IsConnected || payload == null) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            AHNet_SendTo(peerId, Convert.ToBase64String(payload));
#endif
        }

        // ---- IVoiceControl: policy lives in VoiceDirector, machinery in net.js ----

        public void SetMode(VoiceMode mode)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AHNet_VoiceSetMode((int)mode);
#else
            Debug.Log($"[AHNet] (no-op outside WebGL) SetMode {mode}");
#endif
        }

        public void SetPeerGain(string peerId, float gain)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AHNet_VoiceSetPeerVolume(peerId, gain);
#endif
            // silent outside WebGL: this runs several times a second per remote
        }

        public void SetThreshold(float dbfs)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AHNet_VoiceSetThreshold(dbfs);
#endif
        }

        // ---- receivers: called from net.js via SendMessage, single string arg ----
        // Names and signatures are the wire contract with net.js; do not rename.

        public void OnNetReady(string selfId)
        {
            SelfId = selfId;
            IsConnected = true;
            Debug.Log($"[AHNet] ready, self id: {selfId}");
            Ready?.Invoke(selfId);
        }

        public void OnNetError(string message)
        {
            Debug.LogError($"[AHNet] error: {message}");
            Error?.Invoke(message);
        }

        public void OnPeerJoined(string peerId)
        {
            Debug.Log($"[AHNet] peer joined: {peerId}");
            if (!_peerIds.Contains(peerId)) _peerIds.Add(peerId);
            PeerJoined?.Invoke(peerId);
        }

        public void OnPeerLeft(string peerId)
        {
            Debug.Log($"[AHNet] peer left: {peerId}");
            _peerIds.Remove(peerId);
            PeerLeft?.Invoke(peerId);
        }

        public void OnNetMessage(string packed)
        {
            // "<peerId>|<payloadB64>" — two values in one string because
            // SendMessage takes a single argument. Peer ids contain no '|',
            // so split on the first one.
            var sep = packed.IndexOf('|');
            Debug.Assert(sep > 0, "[AHNet] malformed OnNetMessage — no '|' separator");
            if (sep <= 0 || sep == packed.Length - 1) return;
            var peerId = packed.Substring(0, sep);
            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(packed.Substring(sep + 1));
            }
            catch (FormatException)
            {
                Debug.LogError($"[AHNet] bad base64 from {peerId}");
                return;
            }
            Message?.Invoke(peerId, payload);
        }

        public void OnVoiceActivity(string on)
        {
            Activity?.Invoke(on == "1");
        }

        public void OnVoiceError(string message)
        {
            Debug.LogWarning($"[AHNet] voice: {message}");
            VoiceError?.Invoke(message);
        }
    }
}
