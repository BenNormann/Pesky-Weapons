using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The game chat in the top right (round 29c, owner): a NARRATOR who watches how long this player has been in a
    /// room and, when it is too long, says something. One hint at <see cref="firstHintSeconds"/>, a detailed one at
    /// <see cref="secondHintSeconds"/>, both sarcastic and a little mean (the lines live in <see cref="NarratorLines"/>,
    /// keyed by the room's PuzzleRoomProfile id). The clock restarts whenever the player is in a different room. The
    /// same chat carries what goblins say (the porter's refusal), because a line floating over a goblin's head behind a
    /// grate was never readable. Lives on the RunHud's UIDocument like RunEndScreen; purely local, nothing shared.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class NarratorChat : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [Tooltip("Where the local soul comes from.")]
        [OptionalRef][SerializeField] PlayerSpawner spawner;
        [Tooltip("The rooms (each with a RoomVolume child) the player can be in.")]
        [OptionalRef][SerializeField] LabyrinthDirector labyrinth;
        [Tooltip("Seconds in one room before the narrator drops the first hint.")]
        [SerializeField] float firstHintSeconds = 60f;
        [Tooltip("Seconds in one room before the detailed hint.")]
        [SerializeField] float secondHintSeconds = 120f;
        [Tooltip("A line fades out this long after it was said.")]
        [SerializeField] float lineSeconds = 30f;
        [SerializeField] int maxLines = 6;
        [Tooltip("A goblin's line reaches the chat only from this close to the camera.")]
        [SerializeField] float goblinHearing = 40f;

        public const string Narrator = "NARRATOR";
        public const string Goblin = "GOBLIN";

        static NarratorChat _instance;

        struct Line { public Label label; public float bornAt; }
        readonly List<Line> _lines = new List<Line>();
        VisualElement _list;

        struct RoomBox { public LabyrinthRoom room; public Bounds bounds; public string key; }
        readonly List<RoomBox> _rooms = new List<RoomBox>();
        LabyrinthRoom _room;
        float _enteredAt;
        int _hintsGiven;
        float _nextScan;

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            _list = root.Q<VisualElement>("chat-lines");
            if (_list == null)
            {
                // The UXML has no chat yet (an older page): build the panel here so the narrator still speaks.
                VisualElement panel = new VisualElement();
                panel.name = "chat-root";
                panel.AddToClassList("chat-root");
                panel.pickingMode = PickingMode.Ignore;
                _list = new VisualElement();
                _list.name = "chat-lines";
                _list.AddToClassList("chat-lines");
                _list.pickingMode = PickingMode.Ignore;
                panel.Add(_list);
                root.Add(panel);
            }
            _rooms.Clear();
            if (labyrinth != null)
            {
                IReadOnlyList<LabyrinthRoom> rooms = labyrinth.Rooms;
                for (int i = 0; i < rooms.Count; i++)
                {
                    LabyrinthRoom r = rooms[i];
                    if (r == null) continue;
                    Transform vt = r.transform.Find("RoomVolume");
                    Collider vc = vt != null ? vt.GetComponent<Collider>() : null;
                    if (vc == null) continue;
                    RoomBox box = new RoomBox();
                    box.room = r;
                    box.bounds = vc.bounds;
                    PuzzleRoomProfile profile = r.GetComponent<PuzzleRoomProfile>();
                    if (profile != null && !string.IsNullOrEmpty(profile.PuzzleId)) box.key = profile.PuzzleId;
                    else if (r.name.Contains("WeaponRack")) box.key = NarratorLines.StartRoom;
                    else if (r.name.Contains("GateHall")) box.key = NarratorLines.GateHall;
                    else box.key = NarratorLines.PoolRoom;
                    _rooms.Add(box);
                }
            }
        }

        /// <summary>Anything may speak into the chat: the narrator, a goblin. No chat on this machine: silence.</summary>
        public static void Say(string who, string text)
        {
            if (_instance != null) _instance.Post(who, text);
        }

        /// <summary>A goblin spoke at a place: it reaches the chat only if the camera is near enough to have heard it.</summary>
        public static void SayNear(string who, string text, Vector3 at)
        {
            if (_instance == null) return;
            Camera cam = Camera.main;
            if (cam != null && (cam.transform.position - at).sqrMagnitude > _instance.goblinHearing * _instance.goblinHearing) return;
            _instance.Post(who, text);
        }

        void Post(string who, string text)
        {
            if (_list == null || string.IsNullOrEmpty(text)) return;
            Label label = new Label();
            label.enableRichText = true;
            label.text = "<b>" + who + "</b>  " + text;
            label.AddToClassList("chat-line");
            label.AddToClassList(who == Goblin ? "chat-line--goblin" : "chat-line--narrator");
            label.pickingMode = PickingMode.Ignore;
            _list.Add(label);
            Line line = new Line();
            line.label = label;
            line.bornAt = Time.unscaledTime;
            _lines.Add(line);
            while (_lines.Count > maxLines) Drop(0);
        }

        void Drop(int i)
        {
            if (_lines[i].label != null && _lines[i].label.parent != null) _lines[i].label.RemoveFromHierarchy();
            _lines.RemoveAt(i);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                float age = now - _lines[i].bornAt;
                if (age >= lineSeconds) { Drop(i); continue; }
                float fade = Mathf.Clamp01((lineSeconds - age) / 4f);
                _lines[i].label.style.opacity = fade;
            }

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 0.5f;
            Vector3 pos;
            if (!LocalPosition(out pos)) { _room = null; return; }
            LabyrinthRoom here = null;
            string key = null;
            for (int i = 0; i < _rooms.Count; i++)
                if (_rooms[i].bounds.Contains(pos)) { here = _rooms[i].room; key = _rooms[i].key; break; }
            if (here != _room)
            {
                _room = here;
                _enteredAt = Time.time;
                _hintsGiven = 0;
                return;
            }
            if (_room == null) return;
            float stuck = Time.time - _enteredAt;
            if (_hintsGiven == 0 && stuck >= firstHintSeconds)
            {
                _hintsGiven = 1;
                Post(Narrator, NarratorLines.Hint(key, 1));
            }
            else if (_hintsGiven == 1 && stuck >= secondHintSeconds)
            {
                _hintsGiven = 2;
                Post(Narrator, NarratorLines.Hint(key, 2));
            }
        }

        bool LocalPosition(out Vector3 pos)
        {
            pos = Vector3.zero;
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            if (soul == null || soul.IsGhost) return false;
            if (soul.IsPossessing && soul.Weapon != null) pos = soul.Weapon.transform.position;
            else pos = soul.transform.position;
            return true;
        }
    }
}
