using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// What a door waits for. One component with a mode, so the kit ships one prefab per variant
    /// (Door_AlwaysOpen, Door_RoomCleared, Door_Plate, Door_Key, Door_Sealed) instead of one class each.
    /// It is a pure predicate: it never changes state, the WorldAuthority asks it and opens the door.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorCondition : MonoBehaviour
    {
        public enum Mode
        {
            AlwaysOpen = 0,
            RoomCleared = 1,
            PlateLatched = 2,
            PartyHasKey = 3,
            Sealed = 4,
            LeverOn = 5,
            ScalesSatisfied = 6,
            /// <summary>A HOLD plate (PressurePlate.latching off): satisfied only while enough mass rests on it.</summary>
            PlateHeld = 7,
            /// <summary>Every plate in `plates` is Held at once (a circuit whose gaps are all bridged).</summary>
            PlatesHeld = 8,
            /// <summary>Power reaches the door: every break in its Circuit is closed (round 22).</summary>
            CircuitLive = 9,
            /// <summary>Every lever in `levers` is on at the same time (a lever bank: split up and flip them all).</summary>
            LeversOn = 10
        }

        [SerializeField] Mode mode = Mode.AlwaysOpen;
        [Tooltip("RoomCleared: every enemy that belongs to this room must be dead.")]
        [OptionalRef][SerializeField] RoomVolume room;
        [Tooltip("PlateLatched: this plate must have latched.")]
        [OptionalRef][SerializeField] PressurePlate plate;
        [Tooltip("PartyHasKey: the id of the key the party must hold.")]
        [SerializeField] int keyId = 1;
        [Tooltip("LeverOn: this ImpactLever must be on.")]
        [OptionalRef][SerializeField] ImpactLever lever;
        [Tooltip("ScalesSatisfied: this ScalesLock must have latched.")]
        [OptionalRef][SerializeField] ScalesLock scales;
        [Tooltip("CircuitLive: the circuit that must be whole.")]
        [OptionalRef][SerializeField] Circuit circuit;
        [Tooltip("PlatesHeld: every one of these must be Held at the same time.")]
        [OptionalRef][SerializeField] PressurePlate[] plates = new PressurePlate[0];
        [Tooltip("LeversOn: every one of these must be on at the same time.")]
        [OptionalRef][SerializeField] ImpactLever[] levers = new ImpactLever[0];
        [Tooltip("Optional second condition OR-ed with this one (a portcullis held by a plate OR pinned by a lever).")]
        [OptionalRef][SerializeField] DoorCondition orCondition;
        public Mode ConditionMode { get { return mode; } }
        public RoomVolume Room { get { return room; } }
        public PressurePlate Plate { get { return plate; } }
        public int KeyId { get { return keyId; } }
        public ImpactLever Lever { get { return lever; } }
        public ScalesLock Scales { get { return scales; } }
public bool IsSatisfied(WorldAuthority authority)
        {
            if (Own(authority)) return true;
            return orCondition != null && orCondition != this && orCondition.IsSatisfied(authority);
        }

        bool Own(WorldAuthority authority)
        {
            switch (mode)
            {
                case Mode.AlwaysOpen: return true;
                case Mode.RoomCleared: return room != null && room.Cleared;
                case Mode.PlateLatched: return plate != null && plate.Latched;
                case Mode.PlateHeld: return plate != null && plate.Held;
                case Mode.PlatesHeld:
                    if (plates == null || plates.Length == 0) return false;
                    for (int i = 0; i < plates.Length; i++) if (plates[i] == null || !plates[i].Held) return false;
                    return true;
                case Mode.PartyHasKey: return authority != null && authority.HasKey(keyId);
                case Mode.LeverOn: return lever != null && lever.IsOn;
                case Mode.LeversOn:
                    if (levers == null || levers.Length == 0) return false;
                    for (int i = 0; i < levers.Length; i++) if (levers[i] == null || !levers[i].IsOn) return false;
                    return true;
                case Mode.CircuitLive: return circuit != null && circuit.IsLive;
                case Mode.ScalesSatisfied: return scales != null && scales.Latched;
                default: return false; // Sealed
            }
        }

        /// <summary>Short text for the HUD / the sign next to the door.</summary>
        public string Describe()
        {
            switch (mode)
            {
                case Mode.AlwaysOpen: return "OPEN";
                case Mode.RoomCleared: return room != null ? "CLEAR " + room.RoomName : "CLEAR ROOM";
                case Mode.PlateLatched: return "PLATE";
                case Mode.PlateHeld: return "HOLD THE PLATE";
                case Mode.CircuitLive: return "POWER";
                case Mode.PlatesHeld: return "CLOSE THE CIRCUIT";
                case Mode.PartyHasKey: return "KEY " + keyId;
                case Mode.LeverOn: return "LEVER";
                case Mode.LeversOn: return "EVERY LEVER";
                case Mode.ScalesSatisfied: return "SCALES";
                default: return "SEALED";
            }
        }

        /// <summary>True when this mode needs a serialized reference that is missing (scene validator).</summary>
        public bool IsMisconfigured()
        {
            if (mode == Mode.RoomCleared && room == null) return true;
            if ((mode == Mode.PlateLatched || mode == Mode.PlateHeld) && plate == null) return true;
            if (mode == Mode.PlatesHeld && (plates == null || plates.Length == 0)) return true;
            if (mode == Mode.LeverOn && lever == null) return true;
            if (mode == Mode.LeversOn && (levers == null || levers.Length == 0)) return true;
            if (mode == Mode.ScalesSatisfied && scales == null) return true;
            return false;
        }
    }
}
