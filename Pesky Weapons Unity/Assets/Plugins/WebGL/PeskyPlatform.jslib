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
  }
});
