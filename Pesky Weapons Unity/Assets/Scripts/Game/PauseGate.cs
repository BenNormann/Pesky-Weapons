using System;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The PAUSE on this machine (docs/VOTING.md). While the host has the game frozen (WorldAuthority.IsPaused,
    /// from PAUSE_BEGIN to PAUSE_END) this keeps the local player's inputs off through the one overlay flag the
    /// settings screen uses (OrbitCamera.InputEnabled false: no launch, possess, release, roll, flight, nudge,
    /// curse or vote call), and holds the local body still (PlayerSoul.SetFrozen). On PAUSE_END it thaws the
    /// body and, unless the settings screen or the vote screen still owns the cursor, gives the input and the
    /// pointer lock back. The settings screen may open and close on top of a pause: SettingsFlow asks this
    /// before it re-enables the input. Polled, not event driven, so a soul spawned mid-pause (a late joiner) is
    /// frozen too. Order 90: after the soul reads its input, before SettingsFlow (100).
    /// </summary>
    [DefaultExecutionOrder(90)]
    [DisallowMultipleComponent]
    public sealed class PauseGate : MonoBehaviour
    {
        [SerializeField] WorldAuthority authority;
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] OrbitCamera orbitCamera;
        [Tooltip("The settings screen, which may be open when the pause ends: then the input stays with it.")]
        [OptionalRef][SerializeField] SettingsFlow settings;
        [Tooltip("The vote screen: while it is open the cursor is free for it, settings screen or not.")]
        [OptionalRef][SerializeField] VoteScreen voteScreen;
        [Tooltip("The run's end screen (Run.unity only): while it is open the game is frozen on this machine exactly as in a pause, and the cursor is its.")]
        [OptionalRef][SerializeField] RunEndScreen endScreen;

        bool _paused;
        float _pausedAt;

        /// <summary>True from PAUSE_BEGIN to PAUSE_END, as this component has applied it.</summary>
        public bool IsPaused { get { return _paused; } }

        /// <summary>The vote screen or the end screen owns the cursor: a pointer freed for it stays free when the settings screen closes on top of it.</summary>
        public bool PointerFree
        {
            get { return (voteScreen != null && voteScreen.IsOpen) || (endScreen != null && endScreen.IsOpen); }
        }

        /// <summary>(seconds) the pause just ended and took this long on Time.time; local cooldown guides shift by it.</summary>
        public event Action<float> Unpaused;

        void Update()
        {
            // The host's pause (PAUSE_BEGIN to PAUSE_END), or the run's end screen: SESSION_END resets the shared pause
            // state on the same tick as ROUND_RESULT, so the end is frozen here, on each machine, the same way.
            bool paused = (authority != null && authority.IsPaused) || (endScreen != null && endScreen.IsOpen);
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            if (paused != _paused)
            {
                _paused = paused;
                if (paused)
                {
                    _pausedAt = Time.time;
                    if (orbitCamera != null) orbitCamera.InputEnabled = false;
                }
                else
                {
                    if (soul != null) soul.SetFrozen(false);
                    bool overlay = (settings != null && settings.IsOpen) || PointerFree;
                    if (orbitCamera != null && !overlay)
                    {
                        orbitCamera.InputEnabled = true;
                        orbitCamera.LockPointer();
                    }
                    if (Unpaused != null) Unpaused(Time.time - _pausedAt);
                }
            }
            if (!paused) return;
            if (soul != null && !soul.IsFrozen) soul.SetFrozen(true);
            if (orbitCamera != null && orbitCamera.InputEnabled) orbitCamera.InputEnabled = false;
        }
    }
}
