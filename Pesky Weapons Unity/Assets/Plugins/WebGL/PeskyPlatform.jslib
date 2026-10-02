// Pesky - platform queries the C# side cannot make itself.
// Must live under Assets/Plugins/WebGL/ or Unity will not link it, and any
// change here needs a full rebuild, not a script recompile.
mergeInto(LibraryManager.library, {
  Pesky_QueryFlag: function (namePtr) {
    // One query parameter of the page URL as a flag: 1 when it is present and
    // not '0' or 'false', else 0. Read by DebugGate: ?debug=1 unlocks the debug
    // overlay and the console lines in a release build, and ?nolock=1 beside it
    // makes every 'is the pointer locked' gate answer yes on a page that forbids
    // pointer lock (an embedded browser).
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

  Pesky_PageHidden: function () {
    // document.hidden: the tab is in the background. A hidden tab gets no
    // requestAnimationFrame at all, so the game loop stops; the overlay reports it.
    try {
      return document.hidden ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  Pesky_IsFullscreen: function () {
    try {
      return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  Pesky_ToggleFullscreen: function () {
    // The settings screen's FULL SCREEN button. Called from Unity's frame right after the pointer went
    // down on it, which is still inside the browser's user activation, so a request made here through
    // keys.js (Pesky_Keys.enterFullscreen, which also takes the Keyboard Lock) is granted at once. Unity's
    // own Screen.fullScreen goes through Emscripten, which defers the request to the NEXT input event:
    // that is why it used to take a second click. Returns 1 when full screen was asked for, 0 when left.
    try {
      var keys = window.Pesky_Keys;
      var now = !!(document.fullscreenElement || document.webkitFullscreenElement);
      if (now) {
        if (keys && keys.exitFullscreen) keys.exitFullscreen();
        else if (document.exitFullscreen) document.exitFullscreen();
        return 0;
      }
      if (keys && keys.enterFullscreen) return keys.enterFullscreen() ? 1 : 0;
      var el = document.documentElement;
      if (el && el.requestFullscreen) el.requestFullscreen();
      return 1;
    } catch (e) {
      return 0;
    }
  }
});
