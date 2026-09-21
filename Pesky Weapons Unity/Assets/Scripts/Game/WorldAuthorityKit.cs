using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>One trip through a MagicDoor, as raised by WorldAuthority.MagicDoorTraversed.</summary>
    public struct MagicDoorTraversal
    {
        public MagicDoor from;
        public MagicDoor to;
        /// <summary>The weapon that went through, or null when it was a free soul.</summary>
        public WeaponBody weapon;
        /// <summary>The free soul that went through, or the soul driving the weapon (null for a loose weapon).</summary>
        public PlayerSoul soul;
        public Vector3 fromPosition;
        public Vector3 toPosition;
        /// <summary>Turns "into the entry door" into "out of the exit door". Apply it to anything that followed the traveller (the camera yaw).</summary>
        public Quaternion turn;
    }

    /// <summary>
    /// The tower-kit half of the netcode seam: magic doors, magnets, lifts, stuck weapons, the soul
    /// registry and the ordered key list. Same contract as the rest of WorldAuthority: request ->
    /// validate -> apply -> event, and views only react to the events.
    /// </summary>
    public sealed partial class WorldAuthority
    {
        [Header("Registry: tower kit (wired in the Inspector)")]
        [SerializeField] MagicDoor[] magicDoors = new MagicDoor[0];
        [SerializeField] MagnetZone[] magnets = new MagnetZone[0];
        [SerializeField] Lift[] lifts = new Lift[0];

        readonly Dictionary<int, MagicDoor> _magicDoorById = new Dictionary<int, MagicDoor>();
        readonly Dictionary<int, MagnetZone> _magnetById = new Dictionary<int, MagnetZone>();
        readonly Dictionary<int, Lift> _liftById = new Dictionary<int, Lift>();
        readonly List<PlayerSoul> _souls = new List<PlayerSoul>();
        readonly Dictionary<Rigidbody, float> _lastWarp = new Dictionary<Rigidbody, float>();
        readonly List<int> _heldKeys = new List<int>();

        /// <summary>Raised AFTER the traveller has been moved. Remote views must snap, not interpolate, across it.</summary>
        public event Action<MagicDoorTraversal> MagicDoorTraversed;
        /// <summary>(magnet, on)</summary>
        public event Action<MagnetZone, bool> MagnetChanged;
        /// <summary>(lift, on) - Lift.StartMs holds the clock time its cycle starts from.</summary>
        public event Action<Lift, bool> LiftChanged;
        /// <summary>(weapon, wood) a bladed weapon stuck in a WoodSurface.</summary>
        public event Action<WeaponBody, WoodSurface> WeaponStuck;
        public event Action<WeaponBody> WeaponUnstuck;

        public IReadOnlyList<MagicDoor> MagicDoors { get { return magicDoors; } }
        public IReadOnlyList<MagnetZone> Magnets { get { return magnets; } }
        public IReadOnlyList<Lift> Lifts { get { return lifts; } }
        /// <summary>Every soul in the level (one offline; one per player after the netcode port).</summary>
        public IReadOnlyList<PlayerSoul> Souls { get { return _souls; } }

        /// <summary>The key ids the party holds, ascending (for the HUD).</summary>
        public IReadOnlyList<int> HeldKeys
        {
            get
            {
                if (_heldKeys.Count != _partyKeys.Count)
                {
                    _heldKeys.Clear();
                    foreach (int k in _partyKeys) _heldKeys.Add(k);
                    _heldKeys.Sort();
                }
                return _heldKeys;
            }
        }

        public MagicDoor GetMagicDoor(int id) { MagicDoor d; return _magicDoorById.TryGetValue(id, out d) ? d : null; }
        public MagnetZone GetMagnet(int id) { MagnetZone m; return _magnetById.TryGetValue(id, out m) ? m : null; }
        public Lift GetLift(int id) { Lift l; return _liftById.TryGetValue(id, out l) ? l : null; }

        /// <summary>Called from Awake.</summary>
        void AwakeKit()
        {
            Index(magicDoors, _magicDoorById);
            Index(magnets, _magnetById);
            Index(lifts, _liftById);
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                weapons[i].Stuck += OnWeaponStuck;
                weapons[i].Unstuck += OnWeaponUnstuck;
            }
        }

        /// <summary>Called from OnDestroy.</summary>
        void OnDestroyKit()
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null) continue;
                weapons[i].Stuck -= OnWeaponStuck;
                weapons[i].Unstuck -= OnWeaponUnstuck;
            }
        }

        void OnWeaponStuck(WeaponBody weapon, WoodSurface wood) { if (WeaponStuck != null) WeaponStuck(weapon, wood); }
        void OnWeaponUnstuck(WeaponBody weapon) { if (WeaponUnstuck != null) WeaponUnstuck(weapon); }

        // ------------------------------------------------------------------ souls

        public void RegisterSoul(PlayerSoul soul)
        {
            if (soul != null && !_souls.Contains(soul)) _souls.Add(soul);
        }

        public void UnregisterSoul(PlayerSoul soul)
        {
            _souls.Remove(soul);
        }

        /// <summary>The soul driving a weapon, or null while it is loose.</summary>
        public PlayerSoul SoulOf(WeaponBody weapon)
        {
            PlayerSoul soul;
            return weapon != null && _soulOf.TryGetValue(weapon, out soul) ? soul : null;
        }

        // ------------------------------------------------------------------ request: magic door

        /// <summary>The door in this scene that shares a link id with <paramref name="door"/> (used when its twin reference is empty).</summary>
        public MagicDoor FindLinkedMagicDoor(MagicDoor door)
        {
            if (door == null || door.LinkId == 0) return null;
            for (int i = 0; i < magicDoors.Length; i++)
            {
                MagicDoor other = magicDoors[i];
                if (other != null && other != door && other.LinkId == door.LinkId) return other;
            }
            return null;
        }

        bool WarpedRecently(Rigidbody rb, float cooldown)
        {
            float last;
            return _lastWarp.TryGetValue(rb, out last) && Time.time - last < cooldown;
        }

        public bool RequestMagicDoorTraverse(int doorId, int weaponId)
        {
            return RequestMagicDoorTraverse(GetMagicDoor(doorId), GetWeapon(weaponId));
        }

        /// <summary>A weapon (possessed or loose) crossed a magic door's plane from the front.</summary>
        public bool RequestMagicDoorTraverse(MagicDoor door, WeaponBody weapon)
        {
            if (door == null || weapon == null || weapon.IsBroken || weapon.Body == null) return false;
            MagicDoor exit = door.Twin;
            if (exit == null || !door.GateOpen) return false;
            Rigidbody rb = weapon.Body;
            if (WarpedRecently(rb, door.ReentryCooldown)) return false;

            MagicDoorTraversal t = new MagicDoorTraversal();
            t.from = door;
            t.to = exit;
            t.weapon = weapon;
            t.soul = SoulOf(weapon);
            t.turn = door.TurnTo(exit);
            t.fromPosition = rb.position;
            t.toPosition = door.MapPosition(exit, rb.position, rb.worldCenterOfMass);

            weapon.Warp(t.toPosition, t.turn * rb.rotation, t.turn * rb.linearVelocity, t.turn * rb.angularVelocity);
            _lastWarp[rb] = Time.time;
            exit.NoteArrival(rb, rb.worldCenterOfMass);
            if (MagicDoorTraversed != null) MagicDoorTraversed(t);
            return true;
        }

        /// <summary>A free soul crossed a magic door's plane from the front.</summary>
        public bool RequestMagicDoorTraverse(MagicDoor door, PlayerSoul soul)
        {
            if (door == null || soul == null || soul.IsPossessing || soul.Body == null) return false;
            MagicDoor exit = door.Twin;
            if (exit == null || !door.GateOpen) return false;
            Rigidbody rb = soul.Body;
            if (WarpedRecently(rb, door.ReentryCooldown)) return false;

            MagicDoorTraversal t = new MagicDoorTraversal();
            t.from = door;
            t.to = exit;
            t.soul = soul;
            t.turn = door.TurnTo(exit);
            t.fromPosition = rb.position;
            t.toPosition = door.MapPosition(exit, rb.position, rb.position);

            soul.Warp(t.toPosition, t.turn * rb.linearVelocity);
            _lastWarp[rb] = Time.time;
            exit.NoteArrival(rb, t.toPosition);
            if (MagicDoorTraversed != null) MagicDoorTraversed(t);
            return true;
        }

        // ------------------------------------------------------------------ request: magnet power

public bool RequestSetMagnet(int magnetId, bool on)
        {
            MagnetZone magnet = GetMagnet(magnetId);
            if (magnet == null || magnet.IsOn == on) return false;
            return NetHostSwitch(Pesky.Protocol.KitKind.Magnet, magnetId, on);
        }

        // ------------------------------------------------------------------ request: lift power

        /// <summary>Switches a lift on (it leaves its top stop after one dwell) or off (parked at the top).</summary>
public bool RequestSetLift(int liftId, bool on)
        {
            Lift lift = GetLift(liftId);
            if (lift == null || lift.IsOn == on) return false;
            return NetHostSwitch(Pesky.Protocol.KitKind.Lift, liftId, on);
        }
    }
}
