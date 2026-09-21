namespace Pesky.Transport
{
    /// <summary>
    /// The mic mode, numbered to match net.js (voiceSetMode 0 / 1 / 2) so the value
    /// crosses the jslib boundary as a plain int.
    /// </summary>
    public enum VoiceMode
    {
        Off = 0,
        OpenMic = 1,
        VoiceActivated = 2
    }
}
