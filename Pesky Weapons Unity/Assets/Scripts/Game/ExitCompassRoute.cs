using UnityEngine;

namespace Pesky.Game
{
    public struct ExitCompassReading
    {
        public LabyrinthRoom room;
        public MagicDoor door;
        public Vector3 target;
        public Vector3 finalDestination;
        public int step;
        public int roomCount;
        public bool finalRoom;
        public bool arrived;
        public bool locked;
    }

    public static class ExitCompassRoute
    {
        public static bool TryRead(RunDirector run, Vector3 position, out ExitCompassReading reading)
        {
            reading = default;
            if (run == null || run.ExitRoom == null || run.Run == null || run.Run.Count == 0) return false;
            LabyrinthRoom room = run.RoomAt(position);
            if (room == null) return false;
            bool finalRoom = room == run.ExitRoom;
            int index = run.Run.IndexOf(room.RoomId);
            if (!finalRoom && room != run.StartRoom && index < 0) return false;

            MagicDoor door = finalRoom ? null : room.Doorway(RunDirector.Exit);
            if (!finalRoom && door == null) return false;
            reading.room = room;
            reading.door = door;
            reading.target = finalRoom ? run.ExitGoal : door.transform.position;
            reading.finalDestination = run.ExitGoal;
            reading.step = finalRoom ? run.Run.Count + 1 : (room == run.StartRoom ? 0 : index + 1);
            reading.roomCount = run.Run.Count;
            reading.finalRoom = finalRoom;
            reading.arrived = finalRoom && run.InExitVolume(position);
            reading.locked = door != null && !door.GateOpen;
            return true;
        }
    }

    public struct ExitCompassBearing
    {
        float _angle;
        float _velocity;
        bool _ready;

        public float Angle { get { return _angle; } }

        public void Reset() { _ready = false; _velocity = 0f; }

        public float Step(float target, float deltaTime, float smoothTime, bool reset = false)
        {
            if (!_ready || reset)
            {
                _angle = Mathf.DeltaAngle(0f, target);
                _velocity = 0f;
                _ready = true;
            }
            else if (deltaTime > 0f)
            {
                _angle = Mathf.SmoothDampAngle(_angle, target, ref _velocity,
                    Mathf.Max(0.03f, smoothTime), 1080f, Mathf.Min(deltaTime, 0.1f));
                _angle = Mathf.DeltaAngle(0f, _angle);
            }
            return _angle;
        }

        public static bool TryHeading(Vector3 from, Vector3 target, float cameraYaw, out float heading)
        {
            Vector3 flat = target - from;
            flat.y = 0f;
            heading = 0f;
            if (flat.sqrMagnitude < 0.49f) return false;
            heading = Mathf.DeltaAngle(cameraYaw, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
            return true;
        }
    }
}
