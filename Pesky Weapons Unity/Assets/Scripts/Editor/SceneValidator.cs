using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Pesky.Data;
using Pesky.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pesky.Editor
{
    /// <summary>
    /// Checks the things that silently rot in a hand-authored scene: duplicate scene ids, serialized
    /// references left empty, wrong layers, geometry that is not static (and moving things that are).
    /// Menu item plus a callable static method so a build step or a test can run it.
    /// </summary>
    public static class SceneValidator
    {
        public const int LayerWorld = 8;
        public const int LayerWeapon = 9;
        public const int LayerSoul = 10;
        public const int LayerEnemy = 11;
        public const int LayerTrigger = 12;
        public const int LayerPickup = 13;
        public const int LayerDebris = 14;

        [MenuItem("Pesky/Validate Open Scenes")]
        public static void ValidateMenu()
        {
            List<string> problems = Validate();
            if (problems.Count == 0)
            {
                Debug.Log("[SceneValidator] 0 problems.");
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("[SceneValidator] ").Append(problems.Count).Append(" problem(s):");
            for (int i = 0; i < problems.Count; i++) sb.Append('\n').Append(i + 1).Append(". ").Append(problems[i]);
            Debug.LogError(sb.ToString());
        }

        /// <summary>Runs every check over every loaded scene and returns the problems, newest first.</summary>
        public static List<string> Validate()
        {
            List<string> problems = new List<string>();
            List<GameObject> all = new List<GameObject>();

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++) Collect(roots[r].transform, all);
            }

            CheckIds(all, problems);
            CheckReferences(all, problems);
            CheckLayersAndStatic(all, problems);
            CheckPuzzleKit(all, problems);
            CheckEnemyRoles(all, problems);
            CheckLabyrinth(all, problems);            CheckDoorwayOpenings(all, problems);

            CheckRegistry(all, problems);
            CheckSceneSanity(all, problems);
            return problems;
        }

        static void Collect(Transform t, List<GameObject> into)
        {
            into.Add(t.gameObject);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), into);
        }

        static string Path(Component c)
        {
            return c == null ? "<null>" : Path(c.gameObject) + " (" + c.GetType().Name + ")";
        }

        static string Path(GameObject go)
        {
            if (go == null) return "<null>";
            string p = go.name;
            Transform t = go.transform.parent;
            while (t != null) { p = t.name + "/" + p; t = t.parent; }
            return go.scene.name + ":" + p;
        }

        // ------------------------------------------------------------------ ids

        static void CheckIds(List<GameObject> all, List<string> problems)
        {
            Dictionary<int, Component> seen = new Dictionary<int, Component>();
            for (int i = 0; i < all.Count; i++)
            {
                MonoBehaviour[] behaviours = all[i].GetComponents<MonoBehaviour>();
                for (int b = 0; b < behaviours.Length; b++)
                {
                    ISceneId identified = behaviours[b] as ISceneId;
                    if (identified == null) continue;
                    int id = identified.SceneId;
                    if (id == 0)
                    {
                        problems.Add("Scene id 0 (unassigned) on " + Path(behaviours[b]));
                        continue;
                    }
                    Component other;
                    if (seen.TryGetValue(id, out other))
                        problems.Add("Duplicate scene id " + id + ": " + Path(behaviours[b]) + " and " + Path(other));
                    else
                        seen.Add(id, behaviours[b]);
                }
            }
        }

        // ------------------------------------------------------------------ serialized references

        static void CheckReferences(List<GameObject> all, List<string> problems)
        {
            for (int i = 0; i < all.Count; i++)
            {
                MonoBehaviour[] behaviours = all[i].GetComponents<MonoBehaviour>();
                for (int b = 0; b < behaviours.Length; b++)
                {
                    MonoBehaviour mb = behaviours[b];
                    if (mb == null)
                    {
                        problems.Add("Missing script on " + Path(all[i]));
                        continue;
                    }
                    System.Type type = mb.GetType();
                    if (type.Namespace == null || !type.Namespace.StartsWith("Pesky")) continue;

                    SerializedObject so = new SerializedObject(mb);
                    SerializedProperty p = so.GetIterator();
                    bool enter = true;
                    while (p.NextVisible(enter))
                    {
                        enter = true;
                        if (p.propertyPath == "m_Script") continue;
                        if (p.propertyType != SerializedPropertyType.ObjectReference)
                        {
                            // Do not descend into anything that cannot hold a reference.
                            if (p.propertyType == SerializedPropertyType.String) enter = false;
                            continue;
                        }
                        if (p.objectReferenceValue != null) continue;
                        if (IsOptional(type, p.propertyPath)) continue;
                        problems.Add("Empty serialized reference '" + p.propertyPath + "' on " + Path(mb));
                    }
                    so.Dispose();
                }
            }

            // Door conditions that need a reference their mode uses.
            for (int i = 0; i < all.Count; i++)
            {
                DoorCondition dc = all[i].GetComponent<DoorCondition>();
                if (dc != null && dc.IsMisconfigured())
                    problems.Add("DoorCondition mode " + dc.ConditionMode + " has no target on " + Path(dc));
            }
        }

        static bool IsOptional(System.Type type, string propertyPath)
        {
            int dot = propertyPath.IndexOf('.');
            string fieldName = dot >= 0 ? propertyPath.Substring(0, dot) : propertyPath;
            System.Type t = type;
            while (t != null)
            {
                FieldInfo f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f.GetCustomAttributes(typeof(OptionalRefAttribute), true).Length > 0;
                t = t.BaseType;
            }
            return false;
        }

        // ------------------------------------------------------------------ layers and static flags

        static void CheckLayersAndStatic(List<GameObject> all, List<string> problems)
        {
            for (int i = 0; i < all.Count; i++)
            {
                GameObject go = all[i];
                string path = Path(go);
                bool isGeometry = path.Contains("/Geometry");

                if (isGeometry)
                {
                    if (go.GetComponent<Renderer>() != null || go.GetComponent<Collider>() != null)
                    {
                        if (go.layer != LayerWorld)
                            problems.Add("Geometry not on the World layer (is " + LayerMask.LayerToName(go.layer) + "): " + path);
                        if (!go.isStatic)
                            problems.Add("Geometry not marked static: " + path);
                    }
                }

                if (go.GetComponent<ClockMover>() != null && go.isStatic)
                    problems.Add("A ClockMover must not be static: " + path);

                RequireLayer<WeaponBody>(go, LayerWeapon, path, problems);
                RequireLayer<GoblinBrain>(go, LayerEnemy, path, problems);
                RequireLayer<KeyPickup>(go, LayerPickup, path, problems);
                RequireLayer<RunePickup>(go, LayerPickup, path, problems);
                RequireLayer<RoomVolume>(go, LayerTrigger, path, problems);
                RequireLayer<PressurePlate>(go, LayerTrigger, path, problems);
                RequireLayer<AnvilStation>(go, LayerTrigger, path, problems);
                RequireLayer<DoorPrompt>(go, LayerTrigger, path, problems);
                RequireLayer<MagnetZone>(go, LayerTrigger, path, problems);

                if (go.GetComponent<Lift>() != null && go.isStatic)
                    problems.Add("A Lift must not be static: " + path);

                MagnetZone magnet = go.GetComponent<MagnetZone>();
                if (magnet != null)
                {
                    Collider zone = go.GetComponent<Collider>();
                    if (zone == null || !zone.isTrigger) problems.Add("MagnetZone needs a trigger collider: " + path);
                }

                MagicDoor magic = go.GetComponent<MagicDoor>();
                if (magic != null)
                {
                    if (magic.IsGridDoor)
                    {
                        // A grid doorway has NO authored twin: its far side is whatever room the labyrinth
                        // table puts next door, resolved the moment it is asked - and at edit time there is
                        // no table at all. What it needs instead is its room and its director.
                        SerializedObject gso = new SerializedObject(magic);
                        SerializedProperty director = gso.FindProperty("labyrinth");
                        if (director == null || director.objectReferenceValue == null)
                            problems.Add("Grid MagicDoor has no LabyrinthDirector, so it can never resolve a twin: " + path);
                        gso.Dispose();

                        if (magic.Room == null)
                            problems.Add("Grid MagicDoor has no LabyrinthRoom: " + path);
                        else if (magic.Room.Doorway(magic.DoorwayDir) != magic)
                            problems.Add("Grid MagicDoor is not the one its room lists for direction " +
                                         magic.DoorwayDir + ": " + path);
                    }
                    else
                    {
                        MagicDoor other = magic.Twin;
                        if (magic.LinkId == 0) problems.Add("MagicDoor has link id 0 (unassigned): " + path);
                        if (other == null) problems.Add("MagicDoor has no twin (no reference, and no other door in the registry shares its link id): " + path);
                        else
                        {
                            if (other.Twin != magic) problems.Add("MagicDoor's twin does not link back: " + path);
                            if (other.LinkId != magic.LinkId) problems.Add("MagicDoor and its twin carry different link ids: " + path);
                        }
                    }
                    if (Mathf.Abs(Vector3.Dot(go.transform.up, Vector3.up) - 1f) > 0.001f)
                        problems.Add("MagicDoor must stand upright (only its yaw may be rotated): " + path);
                }

                RoomVolume room = go.GetComponent<RoomVolume>();
                if (room != null)
                {
                    Collider col = go.GetComponent<Collider>();
                    if (col == null || !col.isTrigger) problems.Add("RoomVolume needs a trigger collider: " + path);
                }

                Door door = go.GetComponent<Door>();
                if (door != null && door.Condition == null)
                    problems.Add("Door has no DoorCondition: " + path);
            }
        }

        static void RequireLayer<T>(GameObject go, int layer, string path, List<string> problems) where T : Component
        {
            if (go.GetComponent<T>() == null) return;
            if (go.layer != layer)
                problems.Add(typeof(T).Name + " should be on layer " + LayerMask.LayerToName(layer) +
                             " but is on " + LayerMask.LayerToName(go.layer) + ": " + path);
        }

        // ------------------------------------------------------------------ the labyrinth

        /// <summary>
        /// The labyrinth's own wiring, which nothing else can check: room ids are unique and in range, every
        /// authored room is listed in the director, and each room's four doorways point back at it.
        /// </summary>
        static void CheckLabyrinth(List<GameObject> all, List<string> problems)
        {
            List<LabyrinthRoom> rooms = new List<LabyrinthRoom>();
            LabyrinthDirector director = null;
            int directors = 0;
            for (int i = 0; i < all.Count; i++)
            {
                LabyrinthRoom room = all[i].GetComponent<LabyrinthRoom>();
                if (room != null) rooms.Add(room);
                LabyrinthDirector d = all[i].GetComponent<LabyrinthDirector>();
                if (d == null) continue;
                director = d;
                directors++;
            }

            if (rooms.Count == 0 && director == null) return;
            if (directors > 1) problems.Add("Expected at most 1 LabyrinthDirector, found " + directors);
            if (director == null)
            {
                problems.Add("There are " + rooms.Count + " LabyrinthRooms but no LabyrinthDirector to map them.");
                return;
            }

            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < rooms.Count; i++)
            {
                LabyrinthRoom room = rooms[i];
                if (!seen.Add(room.RoomId))
                    problems.Add("Two labyrinth rooms share room id " + room.RoomId + ": " + Path(room));

                for (int dir = 0; dir < 4; dir++)
                {
                    MagicDoor door = room.Doorway(dir);
                    if (door == null) { problems.Add("Labyrinth room " + room.RoomId + " has no doorway " + dir + ": " + Path(room)); continue; }
                    if (!door.IsGridDoor) problems.Add("Labyrinth doorway " + dir + " is not a grid door: " + Path(door));
                    if (door.Room != room) problems.Add("Labyrinth doorway " + dir + " belongs to another room: " + Path(door));
                    if (door.DoorwayDir != dir) problems.Add("Labyrinth doorway is listed as " + dir + " but says " + door.DoorwayDir + ": " + Path(door));
                }
            }

            IReadOnlyList<LabyrinthRoom> listed = director.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < listed.Count; j++) if (listed[j] == rooms[i]) found = true;
                if (!found) problems.Add("Labyrinth room is not listed in the LabyrinthDirector: " + Path(rooms[i]));
            }

            LabyrinthDef def = director.Def;
            if (def == null)
            {
                problems.Add("LabyrinthDirector has no LabyrinthDef to fall back on: " + Path(director));
                return;
            }
            string trouble = def.Problem();
            if (!string.IsNullOrEmpty(trouble)) problems.Add("LabyrinthDef: " + trouble);
            if (rooms.Count < def.CellCount)
                problems.Add("The labyrinth needs " + def.CellCount + " authored rooms but the scene has " + rooms.Count + ".");
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].RoomId < 0 || rooms[i].RoomId >= def.rooms.Length)
                    problems.Add("Labyrinth room id " + rooms[i].RoomId + " is outside LabyrinthDef.rooms: " + Path(rooms[i]));
        }
        // ------------------------------------------------------------------ doorway openings

        /// <summary>How near a Door or MagicDoor has to be to count as filling an opening.</summary>
        const float DoorwayNearDoor = 2.5f;

        /// <summary>How far past the wall the floor probe stands.</summary>
        const float DoorwayProbe = 3f;

        /// <summary>The probe's height above the opening's sill, and how far down it looks for floor.</summary>
        const float DoorwaySill = 0.75f;
        const float DoorwayDrop = 10f;

        /// <summary>
        /// A HOLE IN A WALL HAS TO LEAD SOMEWHERE. Every doorway wall segment (a DoorwayWallSegment, which
        /// the room builder names "..._Doorway") must either hold a working Door or MagicDoor, or have floor
        /// on BOTH sides of it. An opening with a doorway in it is fine however solid the far side is - a
        /// magic doorway's alcove is walled off on purpose - but an opening with neither a door nor a floor
        /// beyond it is a way out of the game, which is exactly the leftover this check was written for.
        ///
        /// Seal an opening by filling it with wall and renaming the segment ("..._Sealed"): it is no longer
        /// a doorway, so it is no longer checked.
        /// </summary>
        static void CheckDoorwayOpenings(List<GameObject> all, List<string> problems)
        {
            List<Transform> openings = new List<Transform>();
            List<Transform> doors = new List<Transform>();
            for (int i = 0; i < all.Count; i++)
            {
                GameObject go = all[i];
                if (go.GetComponent<MagicDoor>() != null || go.GetComponent<Door>() != null) doors.Add(go.transform);
                if (go.name.EndsWith("_Doorway")) openings.Add(go.transform);
            }
            if (openings.Count == 0) return;

            // Edit-mode physics queries need the colliders where the transforms say they are.
            Physics.SyncTransforms();

            for (int i = 0; i < openings.Count; i++)
            {
                Transform opening = openings[i];
                Vector3 middle = opening.position + Vector3.up * 1.75f;

                bool served = false;
                for (int d = 0; d < doors.Count && !served; d++)
                    served = Vector3.Distance(doors[d].position, middle) <= DoorwayNearDoor;
                if (served) continue;

                Vector3 sill = opening.position + Vector3.up * DoorwaySill;
                Vector3 across = opening.forward;
                bool front = HasFloorUnder(sill + across * DoorwayProbe);
                bool back = HasFloorUnder(sill - across * DoorwayProbe);
                if (front && back) continue;

                problems.Add("Doorway opening leads nowhere: no door or magic doorway within " +
                             DoorwayNearDoor + " m and no floor " + DoorwayProbe + " m " +
                             (front ? "behind" : "in front of") + " it. Seal it or give it a door: " +
                             Path(opening.gameObject));
            }
        }

        static bool HasFloorUnder(Vector3 point)
        {
            RaycastHit hit;
            return Physics.Raycast(point, Vector3.down, out hit, DoorwayDrop, ~0, QueryTriggerInteraction.Ignore);
        }


        // ------------------------------------------------------------------ scene sanity

        static void CheckSceneSanity(List<GameObject> all, List<string> problems)
        {
            int cameras = 0, listeners = 0, authorities = 0, clocks = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].GetComponent<Camera>() != null) cameras++;
                if (all[i].GetComponent<AudioListener>() != null) listeners++;
                if (all[i].GetComponent<WorldAuthority>() != null) authorities++;
                if (all[i].GetComponent<LevelClock>() != null) clocks++;
            }

            bool gameplayScene = authorities > 0 || clocks > 0;
            if (!gameplayScene) return;

            if (cameras != 1) problems.Add("Expected exactly 1 Camera, found " + cameras);
            if (listeners != 1) problems.Add("Expected exactly 1 AudioListener, found " + listeners);
            if (authorities != 1) problems.Add("Expected exactly 1 WorldAuthority, found " + authorities);
            if (clocks != 1) problems.Add("Expected exactly 1 LevelClock, found " + clocks);
        }

        // ------------------------------------------------------------------ puzzle kit

        static void RequireNotStatic<T>(GameObject go, string path, List<string> problems) where T : Component
        {
            if (go.GetComponent<T>() == null) return;
            if (go.isStatic)
                problems.Add(typeof(T).Name + " must not be marked static (it moves, or it disappears): " + path);
        }

        static void RequireSolidCollider<T>(GameObject go, string path, List<string> problems) where T : Component
        {
            if (go.GetComponent<T>() == null) return;
            Collider col = go.GetComponent<Collider>();
            if (col == null || col.isTrigger)
                problems.Add(typeof(T).Name + " needs a solid (non-trigger) collider on the SAME object, or it never" +
                             " hears the weapon that hits it: " + path);
        }

        static void RequireTriggerCollider<T>(GameObject go, string path, List<string> problems) where T : Component
        {
            if (go.GetComponent<T>() == null) return;
            Collider col = go.GetComponent<Collider>();
            if (col == null || !col.isTrigger)
                problems.Add(typeof(T).Name + " needs a trigger collider (it is the sensing volume): " + path);
        }

        static void CheckPuzzleKit(List<GameObject> all, List<string> problems)
        {
            for (int i = 0; i < all.Count; i++)
            {
                GameObject go = all[i];
                string path = Path(go);

                RequireLayer<LightningField>(go, LayerTrigger, path, problems);
                RequireLayer<MassPan>(go, LayerTrigger, path, problems);

                RequireNotStatic<DropRamp>(go, path, problems);
                RequireNotStatic<MassPan>(go, path, problems);
                RequireNotStatic<Rope>(go, path, problems);
                RequireNotStatic<Pot>(go, path, problems);
                RequireNotStatic<CrackedWall>(go, path, problems);
                RequireNotStatic<PorterGate>(go, path, problems);
                RequireNotStatic<ImpactLever>(go, path, problems);

                RequireSolidCollider<Rope>(go, path, problems);
                RequireSolidCollider<Pot>(go, path, problems);
                RequireSolidCollider<CrackedWall>(go, path, problems);
                RequireSolidCollider<ImpactLever>(go, path, problems);
                RequireTriggerCollider<LightningField>(go, path, problems);
                RequireTriggerCollider<MassPan>(go, path, problems);

                Rope rope = go.GetComponent<Rope>();
                if (rope != null && rope.Ramp != null && rope.Ramp.Rope != rope)
                    problems.Add("Rope's DropRamp does not point back at this rope: " + path);

                ImpactLever lever = go.GetComponent<ImpactLever>();
                if (lever != null && lever.Lift != null && !lever.IsLatching)
                    problems.Add("A winch (an ImpactLever with a Lift) must be latching, or the lift can be switched off again: " + path);

                CounterweightPair pair = go.GetComponent<CounterweightPair>();
                if (pair != null && pair.PanA != null && pair.PanA == pair.PanB)
                    problems.Add("CounterweightPair has the same pan on both sides: " + path);

                ScalesLock scales = go.GetComponent<ScalesLock>();
                if (scales != null)
                {
                    if (scales.PanCount != 3)
                        problems.Add("ScalesLock should have exactly 3 pans, has " + scales.PanCount + ": " + path);
                    for (int a = 0; a < scales.PanCount; a++)
                        for (int b = a + 1; b < scales.PanCount; b++)
                            if (scales.Pan(a) != null && scales.Pan(a) == scales.Pan(b))
                                problems.Add("ScalesLock uses the same pan in slots " + a + " and " + b + ": " + path);
                }

                PorterGate gate = go.GetComponent<PorterGate>();
                if (gate != null && gate.PorterCount == 0)
                    problems.Add("PorterGate has no porters wired, so nothing can ever open it: " + path);
            }
        }

        // ------------------------------------------------------------------ goblin roles

        static void CheckEnemyRoles(List<GameObject> all, List<string> problems)
        {
            for (int i = 0; i < all.Count; i++)
            {
                GoblinBrain goblin = all[i].GetComponent<GoblinBrain>();
                if (goblin == null) continue;
                string path = Path(goblin);

                if (goblin.IsPorter)
                {
                    if (goblin.CarrySocket == null)
                        problems.Add("Porter goblin has no carrySocket, so it has nowhere to hold a weapon: " + path);
                    if (goblin.DropPoint == null)
                        problems.Add("Porter goblin has no dropPoint (the stand beyond its gate): " + path);
                    if (goblin.Patrol == null)
                        problems.Add("Porter goblin has no PatrolRoute: it would walk straight at its stand and never use its gate: " + path);
                }

                if (goblin.GoblinRole == GoblinBrain.Role.ShieldBoss && goblin.MaxShieldHp <= 0f)
                    problems.Add("ShieldBoss goblin's EnemyDef has shieldMaxHp 0, so it has no shield at all: " + path);

                if (goblin.StartsAsleep && goblin.Room == null)
                    problems.Add("A sleeping goblin needs its RoomVolume wired, or it can never hear anything: " + path);
            }
        }

        // ------------------------------------------------------------------ authority registry

        /// <summary>
        /// Everything the WorldAuthority has to be able to find by id must appear in one of its serialized
        /// arrays. An unregistered piece looks fine in the Inspector and silently does nothing at runtime.
        /// </summary>
        static void CheckRegistry(List<GameObject> all, List<string> problems)
        {
            HashSet<UnityEngine.Object> registered = new HashSet<UnityEngine.Object>();
            bool anyAuthority = false;
            for (int i = 0; i < all.Count; i++)
            {
                WorldAuthority authority = all[i].GetComponent<WorldAuthority>();
                if (authority == null) continue;
                anyAuthority = true;
                SerializedObject so = new SerializedObject(authority);
                SerializedProperty p = so.GetIterator();
                bool enter = true;
                while (p.NextVisible(enter))
                {
                    enter = true;
                    if (p.propertyType == SerializedPropertyType.String) { enter = false; continue; }
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (p.objectReferenceValue != null) registered.Add(p.objectReferenceValue);
                }
                so.Dispose();
            }
            if (!anyAuthority) return;

            System.Type[] types =
            {
                typeof(WeaponBody), typeof(GoblinBrain), typeof(Door), typeof(PressurePlate),
                typeof(KeyPickup), typeof(RunePickup), typeof(AnvilStation), typeof(RoomVolume),
                typeof(MagicDoor), typeof(MagnetZone), typeof(Lift), typeof(Rope), typeof(Pot),
                typeof(ImpactLever), typeof(CounterweightPair), typeof(ScalesLock),
                typeof(LightningField), typeof(CrackedWall), typeof(PorterGate)
            };

            for (int i = 0; i < all.Count; i++)
            {
                for (int t = 0; t < types.Length; t++)
                {
                    Component c = all[i].GetComponent(types[t]);
                    if (c == null || registered.Contains(c)) continue;
                    problems.Add(types[t].Name + " is not registered in any WorldAuthority array, so the authority" +
                                 " cannot see it: " + Path(c));
                }
            }
        }

    }
}
