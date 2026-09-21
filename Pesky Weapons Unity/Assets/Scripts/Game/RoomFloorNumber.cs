using Pesky.Data;
using TMPro;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The one thing a labyrinth room tells you about itself: its OWN number, lying flat and face up on the
    /// middle of its floor with the top of the digits pointing at the room's north doorway, so it reads the
    /// right way round from the camera above.
    ///
    /// The doorways say nothing (see <see cref="DoorwayGlyph"/> and
    /// <see cref="LabyrinthDef.showDoorLabels"/>). A room says where you ARE, never where a door goes, so
    /// the crew has to remember the maze or draw it on the shared scratch pad.
    ///
    /// The number is the room's <see cref="LabyrinthRoom.RoomId"/>, read at runtime, so all twenty-five
    /// rooms and the tutorial's practice rooms get theirs from one prefab with nothing authored per
    /// instance. Set <see cref="overrideText"/> on an instance to say something else.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomFloorNumber : MonoBehaviour
    {
        [Tooltip("The room whose id is shown. Empty: the nearest LabyrinthRoom up the hierarchy is used.")]
        [OptionalRef][SerializeField] LabyrinthRoom room;

        [Tooltip("The flat text on the floor.")]
        [SerializeField] TextMeshPro label;

        [Tooltip("The labyrinth, for the showFloorNumbers toggle. Empty: the number is always shown.")]
        [OptionalRef][SerializeField] LabyrinthDirector labyrinth;

        [Tooltip("Shown instead of the room id when it is not empty.")]
        [SerializeField] string overrideText = "";

        [Tooltip("Seconds between refreshes. The number never changes; this only has to outlast the session starting.")]
        [SerializeField] float refreshInterval = 1f;

        float _next;

        void OnEnable()
        {
            if (room == null) room = GetComponentInParent<LabyrinthRoom>();
            _next = 0f;
            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Mathf.Max(0.25f, refreshInterval);
            Refresh();
        }
        /// <summary>
        /// The labyrinth, from the serialized field or, when that is empty, from the room's own north
        /// doorway - which already carries the director on every authored room. No Find, no singleton.
        /// </summary>
        LabyrinthDirector Director
        {
            get
            {
                if (labyrinth != null) return labyrinth;
                MagicDoor north = room != null ? room.Doorway(0) : null;
                return north != null ? north.Labyrinth : null;
            }
        }


        /// <summary>Write the room's number into the floor label. Safe to call at any time.</summary>
        public void Refresh()
        {
            if (label == null) return;
            LabyrinthDirector director = Director;
            LabyrinthDef def = director != null ? director.Def : null;
            bool show = def == null || def.showFloorNumbers;
            if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            if (!show) return;

            string text = !string.IsNullOrEmpty(overrideText)
                ? overrideText
                : (room != null ? room.RoomId.ToString() : "");
            if (label.text != text) label.text = text;
        }
    }
}
