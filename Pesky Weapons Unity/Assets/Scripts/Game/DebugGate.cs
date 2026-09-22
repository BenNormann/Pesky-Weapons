using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Pesky.Game
{
    /// <summary>
    /// Who may see the debug overlay and the debug console lines. In the Editor and in a development
    /// build they are always available; in a release build only when the page URL carries ?debug=1,
    /// read through PeskyPlatform.jslib's Pesky_QueryFlag. The answer is worked out once, because the
    /// URL cannot change under a running player.
    ///
    /// The same gate owns the one other URL flag: ?nolock=1, honoured only beside ?debug=1, under which
    /// every system that asks "is the pointer locked" is told yes although the page never granted the
    /// lock. It exists for embedded browsers that forbid pointer lock outright (the desktop app's
    /// browser pane), where without it the camera cannot be driven at all. Nothing else changes: mouse
    /// look reads the same deltas, the lock is simply never asked for and never waited on. It is off in
    /// the Editor, off in an ordinary build, and off on any page that does not carry both flags.
    ///
    /// Ported from ATCK's DebugGate.
    /// </summary>
    public static class DebugGate
    {
        /// <summary>The query parameter a release build wants to see.</summary>
        public const string Flag = "debug";
        /// <summary>The query parameter that stands in for a pointer lock the page will not grant.</summary>
        public const string NoLockFlag = "nolock";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int Pesky_QueryFlag(string name);
        [DllImport("__Internal")] static extern int Pesky_PageHidden();
#endif

        static bool _resolved;
        static bool _available;
        static bool _lockResolved;
        static bool _lockBypass;

        /// <summary>True when this build may show the overlay and print debug lines at all.</summary>
        public static bool Available
        {
            get
            {
                if (_resolved) return _available;
                _resolved = true;
                _available = Application.isEditor || Debug.isDebugBuild || UrlFlag(Flag);
                return _available;
            }
        }

        /// <summary>
        /// True when the page asked for the pointer-lock bypass (?nolock=1 beside ?debug=1). Worked out
        /// once. Off everywhere but a WebGL page that carries both flags, so the Editor and an ordinary
        /// build never see it.
        /// </summary>
        public static bool PointerLockBypass
        {
            get
            {
                if (_lockResolved) return _lockBypass;
                _lockResolved = true;
                _lockBypass = !Application.isEditor && Available && UrlFlag(NoLockFlag);
                return _lockBypass;
            }
        }

        /// <summary>The pointer lock as every gameplay gate should read it: the real lock, or the bypass standing in for it.</summary>
        public static bool PointerLocked
        {
            get { return Cursor.lockState == CursorLockMode.Locked || PointerLockBypass; }
        }

        /// <summary>The browser tab is in the background (document.hidden). Always false off WebGL.</summary>
        public static bool PageHidden
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try { return Pesky_PageHidden() != 0; }
                catch (System.Exception) { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>The page URL's flag; false off WebGL, where there is no page.</summary>
        public static bool UrlFlag(string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return Pesky_QueryFlag(name) != 0; }
            catch (System.Exception) { return false; }
#else
            return false;
#endif
        }

        /// <summary>A console line that exists only where the gate is open. Prefixed so the browser console can be filtered on "[pesky]".</summary>
public static void Log(string line)
        {
            // No stack trace: these lines are read in a browser console, where the trace is only noise.
            if (Available) Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[pesky] {0}", line);
        }

        /// <summary>Tells the Session assembly (which cannot see this class) whether its debug lines may print.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ArmSessionLogging()
        {
            Pesky.Session.NetDebug.Enabled = Available;
        }
    }
}
