using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Pesky.Game
{
    /// <summary>
    /// The one clipboard write the room page needs: the browser clipboard through the
    /// AHNet_CopyClipboard export in AHNet.jslib on a WebGL build, the system copy buffer
    /// everywhere else. Not networking, so the DllImport is allowed to live in Game.
    /// </summary>
    public static class Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void AHNet_CopyClipboard(string text);
#endif

        public static void Copy(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AHNet_CopyClipboard(text ?? "");
#else
            GUIUtility.systemCopyBuffer = text ?? "";
#endif
        }
    }
}
