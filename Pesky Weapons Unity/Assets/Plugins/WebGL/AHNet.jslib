// AFTERHOURS — Unity <-> net.js bridge.
// Must live under Assets/Plugins/WebGL/ or Unity will not link it, and any
// change here needs a full rebuild, not a script recompile (spec §8).
// Payloads are base64 strings — see net.js for why not heap pointers.
mergeInto(LibraryManager.library, {
  AHNet_Start: function (roomCodePtr, isHost) {
    try {
      window.AH_Net.start(UTF8ToString(roomCodePtr), !!isHost);
    } catch (e) {
      console.error('[AHNet] jslib start failed:', e);
    }
  },

  AHNet_Leave: function () {
    try {
      window.AH_Net.leave();
    } catch (e) {
      console.error('[AHNet] jslib leave failed:', e);
    }
  },

  AHNet_Broadcast: function (payloadPtr) {
    try {
      window.AH_Net.broadcast(UTF8ToString(payloadPtr));
    } catch (e) {
      console.error('[AHNet] jslib broadcast failed:', e);
    }
  },

  AHNet_SendTo: function (peerIdPtr, payloadPtr) {
    try {
      window.AH_Net.sendTo(UTF8ToString(peerIdPtr), UTF8ToString(payloadPtr));
    } catch (e) {
      console.error('[AHNet] jslib sendTo failed:', e);
    }
  },

  AHNet_GetSelfId: function () {
    var s = '';
    try {
      s = window.AH_Net.getSelfId() || '';
    } catch (e) {
      console.error('[AHNet] jslib getSelfId failed:', e);
    }
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },

  AHNet_GetPeerIds: function () {
    var s = '[]';
    try {
      s = window.AH_Net.getPeerIds() || '[]';
    } catch (e) {
      console.error('[AHNet] jslib getPeerIds failed:', e);
    }
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },

  AHNet_UnityReady: function () {
    try {
      window.AH_Net.unityReady();
    } catch (e) {
      console.error('[AHNet] jslib unityReady failed:', e);
    }
  },

  AHNet_CopyClipboard: function (textPtr) {
    // Needs a user gesture; always called from a click handler, which is one.
    var text = UTF8ToString(textPtr);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).catch(function (e) {
          console.warn('[AHNet] clipboard write failed:', e);
        });
      }
    } catch (e) {
      console.warn('[AHNet] clipboard threw:', e);
    }
  },

  AHNet_VoiceSetMode: function (mode) {
    try {
      window.AH_Net.voiceSetMode(mode);
    } catch (e) {
      console.error('[AHNet] jslib voiceSetMode failed:', e);
    }
  },

  AHNet_VoiceSetPeerVolume: function (peerIdPtr, gain) {
    try {
      window.AH_Net.voiceSetPeerVolume(UTF8ToString(peerIdPtr), gain);
    } catch (e) {
      console.error('[AHNet] jslib voiceSetPeerVolume failed:', e);
    }
  },

  AHNet_VoiceSetThreshold: function (dbfs) {
    try {
      window.AH_Net.voiceSetThreshold(dbfs);
    } catch (e) {
      console.error('[AHNet] jslib voiceSetThreshold failed:', e);
    }
  }
});
