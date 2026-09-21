using System;
using System.Collections.Generic;
using Pesky.Protocol;

namespace Pesky.Session
{
    /// <summary>
    /// The slot table: slot 0 is the host, every slot carries a transport
    /// peer id, a display name, the host flag and the six appearance bytes,
    /// and a released slot is quarantined for two minutes so a returning
    /// peer gets it back and nobody else does. Also owns M0's HELLO
    /// bookkeeping (send once per peer, reply once on receipt) and the
    /// PEER_SLOTS encode/apply, raising SlotAdded and SlotRemoved from the
    /// diff. LocalAppearance is the look this peer announces (Game sets it
    /// before joining; it survives Clear): the host's own Assign and the
    /// client's JOIN_REQUEST both read it.
    /// </summary>
    public sealed class PeerSlots
    {
        public const byte HostSlot = 0;
        public const long QuarantineMs = 120_000;

        sealed class Entry
        {
            public bool present;
            public string peerId = "";
            public string name = "";
            public bool isHost;
            public Appearance appearance;
            public string lastPeerId = "";
            public long releasedAtMs = -1;
        }

        readonly Entry[] _slots = new Entry[Wire.MaxPlayers];
        readonly bool[] _seen = new bool[Wire.MaxPlayers];
        readonly Dictionary<string, byte> _slotByPeer = new Dictionary<string, byte>(Wire.MaxPlayers);
        readonly HashSet<string> _helloSentTo = new HashSet<string>();
        readonly Dictionary<string, string> _helloNames = new Dictionary<string, string>();

        public string LocalPeerId { get; private set; } = "";
        public string LocalName { get; private set; } = "";
        /// <summary>The look this peer joins with; set before Start, kept across sessions.</summary>
        public Appearance LocalAppearance { get; set; } = Appearance.Default;

        public event Action<byte> SlotAdded;
        public event Action<byte> SlotRemoved;
        public event Action Changed;

        public PeerSlots()
        {
            for (var i = 0; i < _slots.Length; i++) _slots[i] = new Entry();
        }

        public byte LocalSlot => SlotOf(LocalPeerId);
        public bool HasLocalSlot => LocalSlot != Wire.NoSlot;

        public int Count
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _slots.Length; i++) if (_slots[i].present) n++;
                return n;
            }
        }

        public void SetLocal(string peerId, string name)
        {
            LocalPeerId = peerId ?? "";
            LocalName = name ?? "";
        }

        public bool IsPresent(byte slot) => slot < _slots.Length && _slots[slot].present;

        /// <summary>The transport peer id in a slot, or "" when absent.</summary>
        public string PeerIdOf(byte slot) => IsPresent(slot) ? _slots[slot].peerId : "";

        public string NameOf(byte slot) => IsPresent(slot) ? _slots[slot].name : "";

        /// <summary>The look announced for a slot; the default when absent.</summary>
        public Appearance AppearanceOf(byte slot) => IsPresent(slot) ? _slots[slot].appearance : Appearance.Default;

        public bool IsHostSlot(byte slot) => IsPresent(slot) && _slots[slot].isHost;

        public byte SlotOf(string peerId) =>
            peerId != null && _slotByPeer.TryGetValue(peerId, out var slot) ? slot : Wire.NoSlot;

        // ---- host side ----

        /// <summary>Assigns with this peer's own look when the peer is the local one, else the default.</summary>
        public byte Assign(string peerId, string name, bool isHost, long nowMs) =>
            Assign(peerId, name, isHost, nowMs, peerId != null && peerId == LocalPeerId ? LocalAppearance : Appearance.Default);

        /// <summary>
        /// Gives a peer a slot: its existing one, its quarantined old one, or
        /// the lowest free slot that is not in quarantine. NoSlot when full.
        /// </summary>
        public byte Assign(string peerId, string name, bool isHost, long nowMs, Appearance appearance)
        {
            if (string.IsNullOrEmpty(peerId)) return Wire.NoSlot;
            name = name ?? "";
            appearance = appearance.Clamped();
            if (_slotByPeer.TryGetValue(peerId, out var existing))
            {
                var e = _slots[existing];
                if (e.name != name || !e.appearance.Same(appearance))
                {
                    e.name = name;
                    e.appearance = appearance;
                    Changed?.Invoke();
                }
                return existing;
            }
            var slot = isHost ? (_slots[HostSlot].present ? Wire.NoSlot : HostSlot) : FindFree(peerId, nowMs);
            if (slot == Wire.NoSlot) return Wire.NoSlot;
            Put(slot, peerId, name, isHost, appearance);
            Changed?.Invoke();
            return slot;
        }

        byte FindFree(string peerId, long nowMs)
        {
            for (byte i = 1; i < _slots.Length; i++)
            {
                var e = _slots[i];
                if (!e.present && e.releasedAtMs >= 0 && e.lastPeerId == peerId) return i;
            }
            for (byte i = 1; i < _slots.Length; i++)
            {
                var e = _slots[i];
                if (e.present) continue;
                if (e.releasedAtMs >= 0 && nowMs - e.releasedAtMs < QuarantineMs) continue;
                return i;
            }
            return Wire.NoSlot;
        }

        /// <summary>Frees the peer's slot and quarantines it; false when the peer had none.</summary>
        public bool Release(string peerId, long nowMs)
        {
            if (peerId == null || !_slotByPeer.TryGetValue(peerId, out var slot)) return false;
            Remove(slot, nowMs);
            Changed?.Invoke();
            return true;
        }

        /// <summary>The whole table as a PEER_SLOTS message (present slots only).</summary>
        public PeerSlotsMsg ToMessage()
        {
            var msg = new PeerSlotsMsg();
            msg.rows = new List<PeerSlotsMsg.Row>(Wire.MaxPlayers);
            for (byte i = 0; i < _slots.Length; i++)
            {
                var e = _slots[i];
                if (!e.present) continue;
                msg.rows.Add(new PeerSlotsMsg.Row { slot = i, peerId = e.peerId, name = e.name, isHost = e.isHost, appearance = e.appearance });
            }
            return msg;
        }

        // ---- client side ----

        /// <summary>Overwrites the table from PEER_SLOTS, raising SlotAdded and SlotRemoved for the difference.</summary>
        public void Apply(in PeerSlotsMsg msg)
        {
            var changed = false;
            for (var i = 0; i < _seen.Length; i++) _seen[i] = false;
            if (msg.rows != null)
            {
                for (var i = 0; i < msg.rows.Count; i++)
                {
                    var row = msg.rows[i];
                    if (row.slot >= _slots.Length) continue;
                    _seen[row.slot] = true;
                    var e = _slots[row.slot];
                    var peer = row.peerId ?? "";
                    var name = row.name ?? "";
                    var look = row.appearance.Clamped();
                    if (e.present && e.peerId == peer)
                    {
                        if (e.name != name || e.isHost != row.isHost || !e.appearance.Same(look))
                        {
                            e.name = name;
                            e.isHost = row.isHost;
                            e.appearance = look;
                            changed = true;
                        }
                        continue;
                    }
                    if (e.present) Remove(row.slot, -1);
                    Put(row.slot, peer, name, row.isHost, look);
                    changed = true;
                }
            }
            for (byte i = 0; i < _slots.Length; i++)
            {
                if (_seen[i] || !_slots[i].present) continue;
                Remove(i, -1);
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        /// <summary>Empties the table and the HELLO books; LocalAppearance is a preference and stays.</summary>
        public void Clear()
        {
            var changed = false;
            for (byte i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].present) continue;
                Remove(i, -1);
                changed = true;
            }
            _helloSentTo.Clear();
            _helloNames.Clear();
            if (changed) Changed?.Invoke();
        }

        // ---- HELLO ----

        /// <summary>True the first time for a peer: the caller sends HELLO exactly then.</summary>
        public bool MarkHelloSent(string peerId) => peerId != null && _helloSentTo.Add(peerId);

        /// <summary>Remembers the name a peer announced; slot names come from JOIN_REQUEST, this is the lobby's early label.</summary>
        public void OnHello(string peerId, string name)
        {
            if (peerId == null) return;
            _helloNames[peerId] = name ?? "";
        }

        public string HelloName(string peerId) =>
            peerId != null && _helloNames.TryGetValue(peerId, out var name) ? name : "";

        /// <summary>A peer left: it gets a fresh HELLO if it comes back.</summary>
        public void ForgetPeer(string peerId)
        {
            if (peerId == null) return;
            _helloSentTo.Remove(peerId);
            _helloNames.Remove(peerId);
        }

        void Put(byte slot, string peerId, string name, bool isHost, Appearance appearance)
        {
            var e = _slots[slot];
            e.present = true;
            e.peerId = peerId;
            e.name = name;
            e.isHost = isHost;
            e.appearance = appearance;
            e.releasedAtMs = -1;
            _slotByPeer[peerId] = slot;
            SlotAdded?.Invoke(slot);
        }

        void Remove(byte slot, long nowMs)
        {
            var e = _slots[slot];
            if (_slotByPeer.TryGetValue(e.peerId, out var mapped) && mapped == slot) _slotByPeer.Remove(e.peerId);
            e.lastPeerId = e.peerId;
            e.releasedAtMs = nowMs;
            e.present = false;
            e.peerId = "";
            e.name = "";
            e.isHost = false;
            e.appearance = default;
            SlotRemoved?.Invoke(slot);
        }
    }
}
