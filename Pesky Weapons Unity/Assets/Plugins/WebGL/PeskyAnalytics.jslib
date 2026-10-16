// Pesky - playtest analytics delivery (docs/ANALYTICS.md). The host's AnalyticsSender hands the page a
// run's report; the page POSTs it to window.PESKY_ANALYTICS_URL with navigator.sendBeacon, which also
// works while the tab is closing. Content-Type text/plain keeps the browser from sending a CORS preflight
// the Apps Script web app cannot answer. Must live under Assets/Plugins/WebGL/; a change here needs a
// full rebuild, not a script recompile.
mergeInto(LibraryManager.library, {
  Pesky_AnalyticsConfigured: function () {
    try {
      var url = window.PESKY_ANALYTICS_URL;
      return (typeof url === 'string' && /^https:\/\//.test(url)) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  Pesky_AnalyticsHold: function (jsonPtr) {
    // The run so far: sent only if the page goes away before the run ends (pagehide), as an abandoned run.
    try {
      var a = window.Pesky_Analytics || (window.Pesky_Analytics = { held: null, armed: false });
      a.held = UTF8ToString(jsonPtr);
      if (!a.armed) {
        a.armed = true;
        window.addEventListener('pagehide', function () {
          var json = a.held;
          a.held = null;
          if (!json || !navigator.sendBeacon) return;
          try { navigator.sendBeacon(window.PESKY_ANALYTICS_URL, new Blob([json], { type: 'text/plain' })); } catch (e) { }
        });
      }
    } catch (e) { }
  },

  Pesky_AnalyticsSend: function (jsonPtr) {
    // The finished run: sent now, and nothing is held for the page closing any more. 1 when queued.
    try {
      var a = window.Pesky_Analytics;
      if (a) a.held = null;
      var json = UTF8ToString(jsonPtr);
      var url = window.PESKY_ANALYTICS_URL;
      if (!url || !navigator.sendBeacon) return 0;
      var ok = navigator.sendBeacon(url, new Blob([json], { type: 'text/plain' }));
      console.log('[pesky] analytics: run report ' + (ok ? 'sent' : 'NOT queued by the browser') + ' (' + json.length + ' bytes)');
      return ok ? 1 : 0;
    } catch (e) {
      return 0;
    }
  }
});
