using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// THE AUDIO HOOK, A STUB (docs/SETTINGS.md). The game has no mixer yet, so of the four volumes in
    /// GameSettings only Master does anything: it sets AudioListener.volume. Music, SFX and Voice are stored
    /// and shown, and only logged here. When an AudioMixer exists, this one method is where the other three
    /// become exposed-parameter writes (decibels from 0 - 100); nothing else has to change.
    ///
    /// Note the name: inside Pesky.Game this is the class you get; UnityEngine.AudioSettings must be written
    /// out in full here.
    /// </summary>
    public static class AudioSettings
    {
        static int _loggedMusic = -1;
        static int _loggedSfx = -1;
        static int _loggedVoice = -1;

        /// <summary>Pushes the saved volumes out. Called at boot, when a gameplay scene loads, and on every change from the settings screen.</summary>
        public static void Apply()
        {
            AudioListener.volume = GameSettings.MasterVolume / (float)GameSettings.MaxVolume;

            int music = GameSettings.MusicVolume;
            int sfx = GameSettings.SfxVolume;
            int voice = GameSettings.VoiceVolume;
            if (music == _loggedMusic && sfx == _loggedSfx && voice == _loggedVoice) return;
            _loggedMusic = music;
            _loggedSfx = sfx;
            _loggedVoice = voice;
            DebugGate.Log("audio: master " + GameSettings.MasterVolume + " -> AudioListener.volume; music " + music
                + ", sfx " + sfx + ", voice " + voice + " stored only (no mixer yet)");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loggedMusic = -1;
            _loggedSfx = -1;
            _loggedVoice = -1;
        }
    }
}
