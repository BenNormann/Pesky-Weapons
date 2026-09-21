using Pesky.Session;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The one static handle that outlives a scene load, the way ATCK's GameLocator does: the live
    /// NetSession the menu started, whether the menu started it at all, and the one line the next
    /// screen should print (why a run ended, why a join failed). Nothing else in the game is global
    /// and nothing calls Find: every other reference is a serialized Inspector field.
    ///
    /// The session itself is plain C#, so it survives SceneManager.LoadScene on its own; this is
    /// only where the next scene's SessionRunner (or the menu, coming back) picks it up again.
    /// </summary>
    public static class GameLocator
    {
        /// <summary>The session every scene shares; null when nothing has started one.</summary>
        public static NetSession Session { get; set; }

        /// <summary>
        /// True when the menu started the session. A gameplay scene opened straight from the Editor
        /// starts its own offline session instead, and must never bounce back to the menu.
        /// </summary>
        public static bool FromMenu { get; set; }

        /// <summary>The line for the next menu screen. Read once with <see cref="TakeMessage"/>.</summary>
        public static string Message { get; private set; } = "";

        public static bool MessageIsError { get; private set; }

        public static void SetMessage(string message, bool error = false)
        {
            Message = message ?? "";
            MessageIsError = error;
        }

        /// <summary>Reads the pending message and clears it, so the next screen does not show it twice.</summary>
        public static string TakeMessage(out bool error)
        {
            error = MessageIsError;
            string message = Message;
            Message = "";
            MessageIsError = false;
            return message;
        }

        /// <summary>Play mode with domain reload off keeps statics; this puts them back.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Session = null;
            FromMenu = false;
            Message = "";
            MessageIsError = false;
        }
    }
}
