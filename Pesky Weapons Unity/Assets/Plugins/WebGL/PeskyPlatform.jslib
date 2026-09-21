// Pesky — platform queries the C# side cannot make itself.
// Must live under Assets/Plugins/WebGL/ or Unity will not link it, and any
// change here needs a full rebuild, not a script recompile.
mergeInto(LibraryManager.library, {
  Pesky_GetDevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  },

  Pesky_ClampDevicePixelRatio: function (maxDpr) {
    // Unity's WebGL runtime sizes the canvas backbuffer from
    // Module.devicePixelRatio (the createUnityInstance config key of the
    // same name) when it is set, and from window.devicePixelRatio otherwise.
    // Writing it here takes effect on the runtime's next canvas size check,
    // no reload needed. Returns the value now in force.
    var dpr = window.devicePixelRatio || 1;
    var clamped = Math.min(dpr, maxDpr > 0 ? maxDpr : dpr);
    try {
      Module.devicePixelRatio = clamped;
    } catch (e) {
      console.warn('[Pesky] devicePixelRatio clamp failed:', e);
    }
    return clamped;
  },

  Pesky_QueryFlag: function (namePtr) {
    // One query parameter of the page URL as a flag: 1 when it is present and
    // not "0" or "false", else 0. Two flags are read through it, both by
    // DebugGate: ?debug=1 opens the debug pane in a release build, and
    // ?nolock=1 beside it makes every gameplay gate treat the pointer as
    // captured on a page that forbids pointer lock (an embedded browser).
    try {
      var name = UTF8ToString(namePtr);
      var search = window.location.search || '';
      var match = new RegExp('[?&]' + name.replace(/[^a-zA-Z0-9_-]/g, '') + '(=([^&]*))?(&|$)').exec(search);
      if (!match) return 0;
      var value = match[2] === undefined ? '1' : decodeURIComponent(match[2]);
      return (value === '0' || value === 'false' || value === '') ? 0 : 1;
    } catch (e) {
      return 0;
    }
  },

  // ---- browser keys (keys.js in the WebGL template owns the behavior) ----

  Pesky_KeyboardLockSupported: function () {
    // Chrome and Edge only, and it only bites while the page is fullscreen.
    // Without it Ctrl+W closes the tab whatever the page does.
    try {
      if (window.Pesky_Keys) return window.Pesky_Keys.keyboardLockSupported() ? 1 : 0;
      return (navigator.keyboard && typeof navigator.keyboard.lock === 'function') ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  Pesky_IsFullscreen: function () {
    try {
      if (window.Pesky_Keys) return window.Pesky_Keys.isFullscreen() ? 1 : 0;
      return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  // Only works inside a user gesture: browsers want transient activation, and
  // the activation from a click survives the trip through Unity's frame loop.
  Pesky_EnterFullscreen: function () {
    try {
      if (window.Pesky_Keys) return window.Pesky_Keys.enterFullscreen() ? 1 : 0;
      // A page built on an older template still gets plain fullscreen.
      var el = document.getElementById('unity-container') ||
        document.getElementById('unity-canvas') ||
        document.documentElement;
      if (!el || !el.requestFullscreen) return 0;
      var p = el.requestFullscreen();
      if (p && typeof p.catch === 'function') p.catch(function () { });
      return 1;
    } catch (e) {
      return 0;
    }
  },

  Pesky_ExitFullscreen: function () {
    try {
      if (window.Pesky_Keys) return window.Pesky_Keys.exitFullscreen() ? 1 : 0;
      if (!document.exitFullscreen) return 0;
      var p = document.exitFullscreen();
      if (p && typeof p.catch === 'function') p.catch(function () { });
      return 1;
    } catch (e) {
      return 0;
    }
  },

  // ---- the shift save: a text file out, a text file in ----

  // Hands the viewer a text file to keep. A blob URL and a synthetic click
  // is the only way a page can start a download, and it only works inside a
  // user gesture, which the SAVE SHIFT click supplies. Returns 1 when the
  // click went out; the browser may still refuse it silently.
  Pesky_DownloadText: function (namePtr, textPtr) {
    try {
      var name = UTF8ToString(namePtr) || 'atck-shift.json';
      var text = UTF8ToString(textPtr) || '';
      var blob = new Blob([text], { type: 'application/json;charset=utf-8' });
      var url = URL.createObjectURL(blob);
      var a = document.createElement('a');
      a.href = url;
      a.download = name;
      a.rel = 'noopener';
      a.style.display = 'none';
      document.body.appendChild(a);
      a.click();
      setTimeout(function () {
        try { document.body.removeChild(a); } catch (e2) { }
        URL.revokeObjectURL(url);
      }, 0);
      return 1;
    } catch (e) {
      console.warn('[Pesky] shift download failed:', e);
      return 0;
    }
  },

  // Feature detection for the picker: an <input type="file"> that reports a
  // files list, and a FileReader to read it. Old and locked-down browsers
  // fall back to the paste box, which needs nothing.
  Pesky_FilePickSupported: function () {
    try {
      if (typeof document === 'undefined' || !document.createElement) return 0;
      if (typeof FileReader === 'undefined') return 0;
      var input = document.createElement('input');
      input.type = 'file';
      return ('files' in input) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  // Opens the file dialog and sends the text back into Unity with
  // SendMessage(object, method, text). Like the download it needs the user
  // gesture the LOAD FROM FILE click carries. Nothing is sent when the
  // viewer cancels, so the C# side must not wait on the answer.
  Pesky_PickTextFile: function (acceptPtr, objectPtr, methodPtr) {
    try {
      var accept = UTF8ToString(acceptPtr) || '.json,.txt';
      var objectName = UTF8ToString(objectPtr);
      var methodName = UTF8ToString(methodPtr);
      var input = document.createElement('input');
      input.type = 'file';
      input.accept = accept;
      input.style.display = 'none';
      input.addEventListener('change', function () {
        var file = input.files && input.files[0];
        try { document.body.removeChild(input); } catch (e2) { }
        if (!file) return;
        var reader = new FileReader();
        reader.onload = function () {
          try {
            var text = String(reader.result || '');
            var unity = window.AH_UnityInstance;
            if (unity && typeof unity.SendMessage === 'function') unity.SendMessage(objectName, methodName, text);
            else if (typeof SendMessage === 'function') SendMessage(objectName, methodName, text);
            else console.warn('[Pesky] no Unity instance to hand the shift file to');
          } catch (e3) {
            console.warn('[Pesky] shift file delivery failed:', e3);
          }
        };
        reader.onerror = function () { console.warn('[Pesky] shift file could not be read'); };
        reader.readAsText(file);
      });
      document.body.appendChild(input);
      input.click();
      return 1;
    } catch (e) {
      console.warn('[Pesky] shift file picker failed:', e);
      return 0;
    }
  }
});
