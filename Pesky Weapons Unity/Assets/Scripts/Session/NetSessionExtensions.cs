using System;
using Pesky.Data;
using Pesky.Transport;

namespace Pesky.Session
{
    /// <summary>
    /// The Game-facing edge of NetSession. Game may not reference the Transport
    /// assembly, so it can name neither INetTransport nor IVoiceControl and cannot
    /// call NetSession.Start or NetSession.Voice directly. These extensions start a
    /// session on the platform transport (or offline) and drive voice with plain
    /// types only. Voice modes are the net.js numbers: 0 off, 1 open mic, 2 voice
    /// activated.
    /// </summary>
    public static class NetSessionExtensions
    {
        public const int VoiceOff = 0;
        public const int VoiceOpenMic = 1;
        public const int VoiceActivated = 2;

        /// <summary>Starts on TransportFactory.ForPlatform(): WebRTC in a WebGL player, loopback everywhere else.</summary>
        public static void StartOnline(this NetSession session, string roomCode, bool isHost, string playerName, GameData data) =>
            session.Start(TransportFactory.ForPlatform(), roomCode, isHost, playerName, data);

        /// <summary>Starts as the host of a solo room on the loopback transport.</summary>
        public static void StartOffline(this NetSession session, string roomCode, string playerName, GameData data) =>
            session.Start(TransportFactory.Offline(), roomCode, true, playerName, data);

        public static bool IsConnected(this NetSession session) =>
            session.Transport != null && session.Transport.IsConnected;

        public static string SelfId(this NetSession session) =>
            session.Transport != null ? session.Transport.SelfId : "";

        public static int PeerCount(this NetSession session) =>
            session.Transport != null ? session.Transport.PeerIds.Count : 0;

        public static bool HasVoice(this NetSession session) => session.Voice != null;

        public static void SetVoiceMode(this NetSession session, int mode)
        {
            if (session.Voice == null) return;
            if (mode < VoiceOff) mode = VoiceOff;
            if (mode > VoiceActivated) mode = VoiceActivated;
            session.Voice.SetMode((VoiceMode)mode);
        }

        /// <summary>Receiver-side playback gain for one peer, 0..1.</summary>
        public static void SetPeerGain(this NetSession session, string peerId, float gain)
        {
            if (session.Voice == null || string.IsNullOrEmpty(peerId)) return;
            if (gain < 0f) gain = 0f;
            else if (gain > 1f) gain = 1f;
            session.Voice.SetPeerGain(peerId, gain);
        }

        public static void SetVoiceThreshold(this NetSession session, float dbfs)
        {
            if (session.Voice != null) session.Voice.SetThreshold(dbfs);
        }

        /// <summary>Subscribes to the transport's voice events; call again after every Start, the transport may be new.</summary>
        public static void AddVoiceListeners(this NetSession session, Action<bool> activity, Action<string> error)
        {
            if (session.Voice == null) return;
            if (activity != null) session.Voice.Activity += activity;
            if (error != null) session.Voice.Error += error;
        }

        public static void RemoveVoiceListeners(this NetSession session, Action<bool> activity, Action<string> error)
        {
            if (session.Voice == null) return;
            if (activity != null) session.Voice.Activity -= activity;
            if (error != null) session.Voice.Error -= error;
        }
    }
}
