#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Where a run's analytics report goes (docs/ANALYTICS.md): on the web page, to the Google Sheet at
    /// window.PESKY_ANALYTICS_URL through PeskyAnalytics.jslib (navigator.sendBeacon). Our own tests stay out
    /// of the data: nothing is sent from the Editor or from a page with the debug gate open (?debug=1, a
    /// development build) unless the page also says ?analytics=1. Where nothing is sent, the report is
    /// written to the console instead, so the pipeline can still be checked by eye.
    /// </summary>
    public static class AnalyticsSender
    {
        /// <summary>The URL flag that lets a debug page send for real, to test the Sheet end to end.</summary>
        public const string ForceFlag = "analytics";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int Pesky_AnalyticsConfigured();
        [DllImport("__Internal")] static extern void Pesky_AnalyticsHold(string json);
        [DllImport("__Internal")] static extern int Pesky_AnalyticsSend(string json);
#endif

        /// <summary>True when reports really leave this page: a URL is configured and this is not one of our test pages.</summary>
        public static bool Live
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try
                {
                    if (Pesky_AnalyticsConfigured() == 0) return false;
                }
                catch (System.Exception) { return false; }
                return !DebugGate.Available || DebugGate.UrlFlag(ForceFlag);
#else
                return false;
#endif
            }
        }

        /// <summary>The run so far, for the page to send if it closes before the run ends.</summary>
        public static void Hold(string json)
        {
            if (string.IsNullOrEmpty(json) || !Live) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { Pesky_AnalyticsHold(json); }
            catch (System.Exception) { }
#endif
        }

        /// <summary>The finished run: to the Sheet, or to the console where nothing is sent.</summary>
        public static void Send(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            if (!Live)
            {
                DebugGate.Log("analytics: not sent (Editor, debug page or no URL): " + json);
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            try { Pesky_AnalyticsSend(json); }
            catch (System.Exception) { }
#endif
        }
    }
}
