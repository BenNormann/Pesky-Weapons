using TMPro;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The truthful sign above a labyrinth doorway. It reads <see cref="MagicDoor.DestinationGlyph"/> and
    /// <see cref="MagicDoor.DestinationLabel"/>, which resolve off the grid table AT THE MOMENT THEY ARE
    /// ASKED, and writes them into two world-space labels. Glyphs never lie; compasses do.
    ///
    /// A pure view: it changes nothing shared, and it is the only thing in the room that knows the table
    /// moved. The table only changes when a Mage drags a room, so a quarter-second refresh is plenty.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorwayGlyph : MonoBehaviour
    {
        [Tooltip("The grid doorway this sign belongs to.")]
        [SerializeField] MagicDoor door;

        [Tooltip("The big glyph of the room through this doorway.")]
        [SerializeField] TextMeshPro glyphLabel;

        [Tooltip("The full room name under the glyph.")]
        [OptionalRef][SerializeField] TextMeshPro nameLabel;

        [Tooltip("Seconds between refreshes.")]
        [SerializeField] float refreshInterval = 0.25f;

        [Tooltip("Shown while the labyrinth has no table yet (before a round starts).")]
        [SerializeField] string unknownGlyph = "?";

        float _next;

        void OnEnable()
        {
            _next = 0f;
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            Refresh();
        }

        /// <summary>Read the doorway and write the labels. Safe to call at any time.</summary>
        /// <summary>
        /// Read the doorway and write the labels. Safe to call at any time.
        ///
        /// The labels are OFF by default: <see cref="Pesky.Data.LabyrinthDef.showDoorLabels"/> is false, so
        /// a doorway says nothing about where it goes and the crew has to work the maze out for itself.
        /// Turning that toggle on brings them back with no other change; the logic below never went away.
        /// </summary>
        public void Refresh()
        {
            if (door == null) return;
            bool show = ShowLabels;
            SetShown(glyphLabel, show);
            SetShown(nameLabel, show);
            if (!show) return;

            string glyph = door.DestinationGlyph;
            if (string.IsNullOrEmpty(glyph)) glyph = unknownGlyph;
            string label = door.DestinationLabel;

            if (glyphLabel != null && glyphLabel.text != glyph) glyphLabel.text = glyph;
            if (nameLabel != null && nameLabel.text != label) nameLabel.text = label;
        }

        /// <summary>Does the data say a doorway may name its destination at all? No labyrinth, no labels.</summary>
        bool ShowLabels
        {
            get
            {
                LabyrinthDirector director = door != null ? door.Labyrinth : null;
                Pesky.Data.LabyrinthDef def = director != null ? director.Def : null;
                return def != null && def.showDoorLabels;
            }
        }

        static void SetShown(TextMeshPro label, bool on)
        {
            if (label != null && label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
        }
    }
}
