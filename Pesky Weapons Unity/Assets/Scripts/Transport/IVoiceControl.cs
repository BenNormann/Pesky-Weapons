using System;

namespace Pesky.Transport
{
    /// <summary>
    /// The voice machinery a transport exposes (NetSession.Voice). Policy lives in Game
    /// (VoiceDirector); this is only the mic mode, a per-peer playback gain and the VAD
    /// gate. No-op on LoopbackTransport.
    /// </summary>
    public interface IVoiceControl                       // NetSession.Voice; no-op on LoopbackTransport
    {
        void SetMode(VoiceMode mode);                    // Off, OpenMic, VoiceActivated
        void SetPeerGain(string peerId, float gain);
        void SetThreshold(float dbfs);
        event Action<bool> Activity;
        event Action<string> Error;
    }
}
