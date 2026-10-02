using System.Collections.Generic;
using Pesky.Data;
using Pesky.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pesky.Editor
{
    /// <summary>
    /// The one-off Editor automation that turned the labyrinth into the simplified RUN (docs/RUN.md,
    /// 2026-09-29): the two-door room variant, the Run scene built from a copy of Labyrinth.unity, the
    /// tutorial's Mage room rebuilt as a practice hall, and a deterministic scene-id pass. Each step is a
    /// menu item so it can be read, re-run on a fresh copy, and checked with Pesky > Validate Open Scenes.
    /// Labyrinth.unity itself is only ever READ.
    /// </summary>
    public static class RunSceneBuilder
    {
        const string RoomPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom.prefab";
        const string BadEndPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_BadEnd.prefab";
        const string TwoDoorPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_TwoDoor.prefab";
        const string DoorLeverPrefab = "Assets/Prefabs/Kit/Doors/Door_Lever.prefab";
        const string DoorSealedPrefab = "Assets/Prefabs/Kit/Doors/Door_Sealed.prefab";
        const string LeverPrefab = "Assets/Prefabs/Kit/Movers/ImpactLever.prefab";
        const string SignPrefab = "Assets/Prefabs/Kit/Signs_And_Lights/Sign.prefab";
        const string LabyrinthScene = "Assets/Scenes/Labyrinth.unity";
        const string RunScene = "Assets/Scenes/Run.unity";
        const string TutorialScene = "Assets/Scenes/Tutorial.unity";
        const string RunDefAsset = "Assets/Data/Run.asset";
        const string RunTutorialDefAsset = "Assets/Data/Run_Tutorial.asset";
        const string RunUxml = "Assets/UI/Run.uxml";
        const string RunUss = "Assets/UI/Run.uss";
        const int LayerWorld = 8;

        // ------------------------------------------------------------------ 1. the two-door room

        /// <summary>
        /// LabyrinthRoom_TwoDoor: the square room with its east and west doorways removed and those openings
        /// sealed, a Door_Lever gate on the south (exit) doorway and an ImpactLever ("Seal") beside it. The
        /// north doorway is the ENTRY, the south the EXIT. sealedSides marks east and west, so the scene
        /// validator accepts the two empty doorway slots.
        /// </summary>
        [MenuItem("Pesky/Run/1 Create TwoDoor Room Variant")]
        public static void CreateTwoDoorVariant()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefab);
            if (source == null) { Debug.LogError("Missing " + RoomPrefab); return; }
            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(source);
            room.name = "LabyrinthRoom_TwoDoor";

            RemoveSideDoorways(room);
            SealOpening(room, "Wall_East", new Vector3(12.25f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 3f));
            SealOpening(room, "Wall_West", new Vector3(-12.25f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 3f));

            // The placeholder seal: a latching impact lever beside the exit door, and a lever gate on it.
            Transform gameplay = new GameObject("Gameplay").transform;
            gameplay.SetParent(room.transform, false);
            GameObject lever = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LeverPrefab), gameplay);
            lever.name = "Seal";
            lever.transform.localPosition = new Vector3(4f, 0f, -9f);
            lever.transform.localRotation = Quaternion.identity;

            Transform doorways = room.transform.Find("Doorways");
            GameObject gate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DoorLeverPrefab), doorways);
            gate.name = "Gate_South";
            gate.transform.localPosition = new Vector3(0f, 0f, -12f);
            gate.transform.localRotation = Quaternion.identity;
            SetRef(gate.GetComponent<DoorCondition>(), "lever", lever.GetComponent<ImpactLever>());
            SetRef(doorways.Find("Doorway_South").GetComponent<MagicDoor>(), "gate", gate.GetComponent<Door>());

            PrefabUtility.SaveAsPrefabAsset(room, TwoDoorPrefab);
            Object.DestroyImmediate(room);
            AssetDatabase.SaveAssets();
            Debug.Log("[RunSceneBuilder] wrote " + TwoDoorPrefab);
        }

        /// <summary>Removes the east and west doorway objects and marks those sides sealed on the LabyrinthRoom.</summary>
        static void RemoveSideDoorways(GameObject room)
        {
            Transform doorways = room.transform.Find("Doorways");
            DestroyChild(doorways, "Doorway_East");
            DestroyChild(doorways, "Doorway_West");
            LabyrinthRoom lr = room.GetComponent<LabyrinthRoom>();
            SerializedObject so = new SerializedObject(lr);
            SerializedProperty doors = so.FindProperty("doorways");
            SerializedProperty sealedSides = so.FindProperty("sealedSides");
            if (doors.arraySize < 4) doors.arraySize = 4;
            if (sealedSides.arraySize < 4) sealedSides.arraySize = 4;
            doors.GetArrayElementAtIndex(1).objectReferenceValue = null;
            doors.GetArrayElementAtIndex(3).objectReferenceValue = null;
            sealedSides.GetArrayElementAtIndex(1).boolValue = true;
            sealedSides.GetArrayElementAtIndex(3).boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        /// <summary>A wall block that fills a 3 x 3.5 opening: World layer, static, the wall's own material.</summary>
        static GameObject SealOpening(GameObject room, string wallName, Vector3 localPosition, Vector3 size)
        {
            Transform wall = room.transform.Find("Geometry/" + wallName);
            if (wall == null) { Debug.LogError("No Geometry/" + wallName + " on " + room.name); return null; }
            Transform reference = wall.Find("Left");
            GameObject seal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seal.name = "Seal";
            seal.transform.SetParent(wall, false);
            seal.transform.localPosition = localPosition;
            seal.transform.localRotation = Quaternion.identity;
            seal.transform.localScale = size;
            seal.layer = LayerWorld;
            MeshRenderer mr = seal.GetComponent<MeshRenderer>();
            MeshRenderer refMr = reference != null ? reference.GetComponent<MeshRenderer>() : null;
            if (mr != null && refMr != null) mr.sharedMaterial = refMr.sharedMaterial;
            GameObjectUtility.SetStaticEditorFlags(seal, reference != null ? GameObjectUtility.GetStaticEditorFlags(reference.gameObject) : (StaticEditorFlags)~0);
            return seal;
        }

        static void SetRef(Object target, string property, Object value)
        {
            if (target == null) { Debug.LogError("SetRef: no target for " + property); return; }
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError("SetRef: " + target.GetType().Name + " has no " + property); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string property, bool value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError("SetBool: " + target.GetType().Name + " has no " + property); return; }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string property, int value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError("SetInt: " + target.GetType().Name + " has no " + property); return; }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string property, string value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError("SetString: " + target.GetType().Name + " has no " + property); return; }
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray<T>(Object target, string property, List<T> values) where T : Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError("SetArray: " + target.GetType().Name + " has no " + property); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static int GetInt(Object target, string property)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            return p != null ? p.intValue : 0;
        }

        // ------------------------------------------------------------------ 2. the Run scene

        /// <summary>
        /// Copies Labyrinth.unity to Run.unity and reworks the copy: every blank room and the Resurrection
        /// Room become TwoDoor pool rooms; the start room keeps its rack and gets a sealed, signed north
        /// door; the Exit room keeps its south ExitZone; the map / pad / compass HUD goes and RunHud, the
        /// RunDirector, MageCurse and CurseEffects arrive; every registry is rebuilt; scene ids are
        /// reassigned by kind. Then the build settings become Boot, MainMenu, Tutorial, Run.
        /// </summary>
        [MenuItem("Pesky/Run/2 Build Run Scene From Labyrinth")]
        public static void BuildRunScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TwoDoorPrefab) == null) { Debug.LogError("Run step 1 first: no " + TwoDoorPrefab); return; }
            if (!AssetDatabase.CopyAsset(LabyrinthScene, RunScene)) { Debug.LogError("Could not copy " + LabyrinthScene + " to " + RunScene); return; }
            Scene scene = EditorSceneManager.OpenScene(RunScene, OpenSceneMode.Single);

            GameObject managers = FindRoot(scene, "_Managers");
            GameObject ui = FindRoot(scene, "_UI");
            GameObject environment = FindRoot(scene, "Environment");
            Transform roomsRoot = environment.transform.Find("Rooms");
            WorldAuthority authority = managers.GetComponentInChildren<WorldAuthority>();
            LabyrinthDirector director = managers.GetComponentInChildren<LabyrinthDirector>();
            SessionRunner runner = managers.GetComponentInChildren<SessionRunner>();
            PlayerSpawner spawner = managers.GetComponentInChildren<PlayerSpawner>();
            LevelClock clock = managers.GetComponentInChildren<LevelClock>();
            MageNudge nudge = managers.GetComponentInChildren<MageNudge>();
            OrbitCamera camera = Object.FindFirstObjectByType<OrbitCamera>();
            LabyrinthDef labDef = director != null ? director.Def : null;
            GameObject twoDoor = AssetDatabase.LoadAssetAtPath<GameObject>(TwoDoorPrefab);

            // ---- rooms
            LabyrinthRoom startRoom = null, exitRoom = null;
            List<LabyrinthRoom> pool = new List<LabyrinthRoom>();
            List<Transform> children = new List<Transform>();
            foreach (Transform t in roomsRoot) children.Add(t);
            foreach (Transform t in children)
            {
                LabyrinthRoom lr = t.GetComponent<LabyrinthRoom>();
                if (lr == null) continue;
                Object src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                string srcPath = src != null ? AssetDatabase.GetAssetPath(src) : "";
                int roomId = lr.RoomId;
                if (srcPath == RoomPrefab || srcPath == BadEndPrefab)
                {
                    int index = t.GetSiblingIndex();
                    Vector3 pos = t.position;
                    Quaternion rot = t.rotation;
                    Object.DestroyImmediate(t.gameObject);
                    GameObject fresh = (GameObject)PrefabUtility.InstantiatePrefab(twoDoor, roomsRoot);
                    fresh.name = "Room_" + roomId.ToString("00") + "_Pool";
                    fresh.transform.SetPositionAndRotation(pos, rot);
                    fresh.transform.SetSiblingIndex(index);
                    LabyrinthRoom room = fresh.GetComponent<LabyrinthRoom>();
                    SetInt(room, "roomId", roomId);
                    string label = labDef != null ? labDef.LabelOf(roomId) : "Room " + roomId;
                    WirePoolRoom(fresh, authority, director, label);
                    pool.Add(room);
                }
                else if (srcPath.EndsWith("LabyrinthRoom_Start.prefab"))
                {
                    startRoom = lr;
                    ReworkStartRoom(t.gameObject, authority, director);
                }
                else if (srcPath.EndsWith("LabyrinthRoom_Exit.prefab"))
                {
                    exitRoom = lr;
                    ReworkExitRoom(t.gameObject);
                }
                else Debug.LogWarning("Unknown room prefab on " + t.name + ": " + srcPath);
            }
            if (startRoom == null || exitRoom == null) { Debug.LogError("No start or exit room found."); return; }

            // ---- managers
            DestroyChild(managers.transform, "CompassModel");
            GameObject runGo = new GameObject("RunDirector");
            runGo.transform.SetParent(managers.transform, false);
            RunDirector run = runGo.AddComponent<RunDirector>();
            SetRef(run, "sessionRunner", runner);
            SetRef(run, "fallbackDef", AssetDatabase.LoadAssetAtPath<RunDef>(RunDefAsset));
            SetRef(run, "startRoom", startRoom);
            SetRef(run, "exitRoom", exitRoom);
            RoomVolume exitVolume = exitRoom.GetComponentInChildren<RoomVolume>();
            SetRef(run, "exitVolume", exitVolume != null ? exitVolume.GetComponent<BoxCollider>() : null);
            SetArray(run, "pool", pool);
            SetRef(director, "run", run);
            SetRef(authority, "run", run);

            // ---- UI
            DestroyChild(ui.transform, "LabyrinthHud");
            RunHud runHud = CreateRunHud(ui.transform, authority, runner, RunDefAsset, false);

            SetRef(nudge, "hud", null);
            SetRef(nudge, "runHud", runHud);
            InputActionAssetOf(nudge, out Object controls);
            CreateCurseObjects(managers.transform, authority, spawner, camera, clock, runHud, controls, RunDefAsset);

            // ---- registries, ids, save
            RebuildRegistries(scene, authority, director);
            AssignSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Reinstall the authored platform rooms after rebuilding Run.unity so the automation remains
            // safe to rerun instead of silently replacing them with disposable pool placeholders.
            RunPuzzleRoomsBuilder.CreateAllPrefabs();
            RunPuzzleRoomsBuilder.InstallIntoOpenRunScene(scene);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene(TutorialScene, true),
                new EditorBuildSettingsScene(RunScene, true),
            };
            Debug.Log("[RunSceneBuilder] built " + RunScene + ": pool of " + pool.Count + " rooms; build settings = Boot, MainMenu, Tutorial, Run");
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == name) return go;
            Debug.LogError("No root object " + name + " in " + scene.path);
            return null;
        }

        /// <summary>Per instance wiring of a TwoDoor room: the scene references a prefab cannot hold, and the room's own name.</summary>
        static void WirePoolRoom(GameObject room, WorldAuthority authority, LabyrinthDirector director, string label)
        {
            foreach (MagicDoor door in room.GetComponentsInChildren<MagicDoor>(true))
            {
                SetRef(door, "authority", authority);
                SetRef(door, "labyrinth", director);
            }
            foreach (Door door in room.GetComponentsInChildren<Door>(true)) SetRef(door, "authority", authority);
            foreach (ImpactLever lever in room.GetComponentsInChildren<ImpactLever>(true)) SetRef(lever, "authority", authority);
            RoomVolume volume = room.GetComponentInChildren<RoomVolume>(true);
            if (volume != null) SetString(volume, "roomName", label);
            Transform sign = room.transform.Find("Fixtures/RoomSign");
            if (sign != null) SetString(sign.GetComponent<Sign>(), "text", label);
        }

        /// <summary>The start room: east and west sealed, the north doorway gated shut for good with a RESERVED sign, the south doorway the run's first door.</summary>
        static void ReworkStartRoom(GameObject room, WorldAuthority authority, LabyrinthDirector director)
        {
            RemoveSideDoorways(room);
            SealOpening(room, "Wall_East", new Vector3(12.25f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 3f));
            SealOpening(room, "Wall_West", new Vector3(-12.25f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 3f));

            Transform doorways = room.transform.Find("Doorways");
            GameObject gate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DoorSealedPrefab), doorways);
            gate.name = "Gate_North_Reserved";
            gate.transform.localPosition = new Vector3(0f, 0f, 12f);
            gate.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SetRef(gate.GetComponent<Door>(), "authority", authority);
            SetRef(doorways.Find("Doorway_North").GetComponent<MagicDoor>(), "gate", gate.GetComponent<Door>());

            Transform fixtures = room.transform.Find("Fixtures");
            GameObject sign = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SignPrefab), fixtures);
            sign.name = "Sign_Reserved";
            // Beside the north doorway, its +Z at the north wall so the board reads from the room.
            sign.transform.localPosition = new Vector3(3.2f, 0f, 10.5f);
            sign.transform.localRotation = Quaternion.identity;
            SetString(sign.GetComponent<Sign>(), "text", "RESERVED");
        }

        /// <summary>The Exit room: east and west sealed, only the south ExitZone kept (its doorway is the Exit).</summary>
        static void ReworkExitRoom(GameObject room)
        {
            RemoveSideDoorways(room);
            SealOpening(room, "Wall_East", new Vector3(12.25f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 3f));
            SealOpening(room, "Wall_West", new Vector3(-12.25f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 3f));
            Transform zones = room.transform.Find("ExitZones");
            DestroyChild(zones, "ExitZone_North");
            DestroyChild(zones, "ExitZone_East");
            DestroyChild(zones, "ExitZone_West");
        }

        static RunHud CreateRunHud(Transform uiRoot, WorldAuthority authority, SessionRunner runner, string defPath, bool asleep)
        {
            UIDocument reference = null;
            foreach (UIDocument d in uiRoot.GetComponentsInChildren<UIDocument>(true)) { reference = d; break; }
            GameObject go = new GameObject("RunHud");
            go.transform.SetParent(uiRoot, false);
            UIDocument doc = go.AddComponent<UIDocument>();
            if (reference != null) doc.panelSettings = reference.panelSettings;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RunUxml);
            doc.sortingOrder = 1;
            RunHud hud = go.AddComponent<RunHud>();
            SetRef(hud, "document", doc);
            SetRef(hud, "style", AssetDatabase.LoadAssetAtPath<StyleSheet>(RunUss));
            SetRef(hud, "authority", authority);
            SetRef(hud, "sessionRunner", runner);
            SetRef(hud, "fallbackDef", AssetDatabase.LoadAssetAtPath<RunDef>(defPath));
            SetBool(hud, "startAsleep", asleep);
            return hud;
        }

        static void InputActionAssetOf(MageNudge nudge, out Object controls)
        {
            controls = null;
            if (nudge == null) return;
            SerializedObject so = new SerializedObject(nudge);
            SerializedProperty p = so.FindProperty("controls");
            controls = p != null ? p.objectReferenceValue : null;
        }

        static void CreateCurseObjects(Transform managers, WorldAuthority authority, PlayerSpawner spawner, OrbitCamera camera,
            LevelClock clock, RunHud runHud, Object controls, string defPath)
        {
            RunDef def = AssetDatabase.LoadAssetAtPath<RunDef>(defPath);
            GameObject curseGo = new GameObject("MageCurse");
            curseGo.transform.SetParent(managers, false);
            MageCurse curse = curseGo.AddComponent<MageCurse>();
            SetRef(curse, "authority", authority);
            SetRef(curse, "spawner", spawner);
            SetRef(curse, "orbitCamera", camera);
            SetRef(curse, "runHud", runHud);
            SetRef(curse, "fallbackDef", def);
            SetRef(curse, "controls", controls);

            GameObject fxGo = new GameObject("CurseEffects");
            fxGo.transform.SetParent(managers, false);
            CurseEffects fx = fxGo.AddComponent<CurseEffects>();
            SetRef(fx, "authority", authority);
            SetRef(fx, "spawner", spawner);
            SetRef(fx, "orbitCamera", camera);
            SetRef(fx, "clock", clock);
            SetRef(fx, "runHud", runHud);
            SetRef(fx, "fallbackDef", def);
        }

        /// <summary>Every MagicDoor, RoomVolume, Door and ImpactLever in the scene into the WorldAuthority, every LabyrinthRoom into the director. Hierarchy order.</summary>
        static void RebuildRegistries(Scene scene, WorldAuthority authority, LabyrinthDirector director)
        {
            List<MagicDoor> magicDoors = new List<MagicDoor>();
            List<RoomVolume> volumes = new List<RoomVolume>();
            List<Door> doors = new List<Door>();
            List<ImpactLever> levers = new List<ImpactLever>();
            List<ScalesLock> scales = new List<ScalesLock>();
            List<LabyrinthRoom> rooms = new List<LabyrinthRoom>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                magicDoors.AddRange(root.GetComponentsInChildren<MagicDoor>(true));
                volumes.AddRange(root.GetComponentsInChildren<RoomVolume>(true));
                doors.AddRange(root.GetComponentsInChildren<Door>(true));
                levers.AddRange(root.GetComponentsInChildren<ImpactLever>(true));
                scales.AddRange(root.GetComponentsInChildren<ScalesLock>(true));
                rooms.AddRange(root.GetComponentsInChildren<LabyrinthRoom>(true));
            }
            if (authority != null)
            {
                SetArray(authority, "magicDoors", magicDoors);
                SetArray(authority, "rooms", volumes);
                SetArray(authority, "doors", doors);
                SetArray(authority, "levers", levers);
                SetArray(authority, "scales", scales);
            }
            if (director != null) SetArray(director, "rooms", rooms);
            Debug.Log("[RunSceneBuilder] registries: " + magicDoors.Count + " magic doors, " + volumes.Count + " volumes, " + doors.Count + " doors, " + levers.Count + " levers, " + scales.Count + " scales, " + rooms.Count + " rooms");
        }

        // ------------------------------------------------------------------ scene ids

        /// <summary>
        /// Every ISceneId in the open scene gets an id from its kind's block (docs/KIT.md), in hierarchy
        /// order: weapons 101+, home slots 201+, room volumes 301+, doors 401+, enemies 501+, plates 601+,
        /// keys 701+, runes 801+, anvils 901+, clock movers 1001+, signs 1101+, spawn points 1201+, magic
        /// doors 1301+, magnets 1401+, lifts 1501+, ropes 1601+, pots 1701+, levers 1801+, counterweights
        /// 1901+, scales 2001+, lightning 2101+, cracked walls 2201+, porter gates 2301+, exit zones 2401+,
        /// anything else 3001+. Deterministic, so two loads of the same scene agree.
        /// </summary>
        [MenuItem("Pesky/Run/Assign Scene Ids (open scene)")]
        public static void AssignSceneIdsMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            AssignSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static int BlockOf(System.Type t)
        {
            string n = t.Name;
            switch (n)
            {
                case "WeaponBody": return 101;
                case "WeaponHomeSlot": return 201;
                case "RoomVolume": return 301;
                case "Door": return 401;
                case "GoblinBrain": return 501;
                case "PressurePlate": return 601;
                case "KeyPickup": return 701;
                case "RunePickup": return 801;
                case "AnvilStation": return 901;
                case "ClockMover": return 1001;
                case "Sign": return 1101;
                case "SpawnPoint": return 1201;
                case "MagicDoor": return 1301;
                case "MagnetZone": return 1401;
                case "Lift": return 1501;
                case "Rope": return 1601;
                case "Pot": return 1701;
                case "ImpactLever": return 1801;
                case "CounterweightPair": return 1901;
                case "ScalesLock": return 2001;
                case "LightningField": return 2101;
                case "CrackedWall": return 2201;
                case "PorterGate": return 2301;
                case "ExitZone": return 2401;
                default: return 3001;
            }
        }

        public static void AssignSceneIds(Scene scene)
        {
            Dictionary<int, int> next = new Dictionary<int, int>();
            List<GameObject> all = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects()) Collect(root.transform, all);
            int count = 0;
            foreach (GameObject go in all)
            {
                foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
                {
                    if (!(mb is ISceneId)) continue;
                    int block = BlockOf(mb.GetType());
                    int id;
                    if (!next.TryGetValue(block, out id)) id = block;
                    next[block] = id + 1;
                    SerializedObject so = new SerializedObject(mb);
                    SerializedProperty p = so.FindProperty("id");
                    if (p == null) { Debug.LogWarning("ISceneId without an 'id' field: " + mb.GetType().Name); continue; }
                    p.intValue = id;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
            }
            Debug.Log("[RunSceneBuilder] assigned " + count + " scene ids in " + scene.name);
        }

        static void Collect(Transform t, List<GameObject> into)
        {
            into.Add(t.gameObject);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), into);
        }

        // ------------------------------------------------------------------ 3. the tutorial's practice hall

        /// <summary>
        /// Room6_MageTutorial without the 3x3 practice grid: the Entry Hall alone, its four grid doorways
        /// gone and sealed, the ending ExitZone moved in and lit for good, new signs (the run, the curses),
        /// RunHud instead of LabyrinthHud, MageCurse and CurseEffects, the dummy's label wired for the curse
        /// lesson, registries rebuilt, NavMesh rebaked.
        /// </summary>
        [MenuItem("Pesky/Run/3 Rework Tutorial Mage Room")]
        public static void ReworkTutorial()
        {
            Scene scene = EditorSceneManager.OpenScene(TutorialScene, OpenSceneMode.Single);
            GameObject managers = FindRoot(scene, "_Managers");
            GameObject ui = FindRoot(scene, "_UI");
            GameObject room6 = FindRoot(scene, "Room6_MageTutorial");
            WorldAuthority authority = managers.GetComponentInChildren<WorldAuthority>();
            SessionRunner runner = managers.GetComponentInChildren<SessionRunner>();
            PlayerSpawner spawner = managers.GetComponentInChildren<PlayerSpawner>();
            LevelClock clock = managers.GetComponentInChildren<LevelClock>();
            MageNudge nudge = managers.GetComponentInChildren<MageNudge>();
            OrbitCamera camera = Object.FindFirstObjectByType<OrbitCamera>();
            PracticeDummy dummy = room6.GetComponentInChildren<PracticeDummy>(true);

            Transform hall = room6.transform.Find("PRoom_00_EntryHall");
            Transform gateHall = room6.transform.Find("PRoom_02_GateHall");
            if (hall == null || gateHall == null) { Debug.LogError("Tutorial practice rooms not found."); return; }

            // The ending ring: out of the Gate Hall (unpacked so it can leave), into the practice hall, lit for good.
            PrefabUtility.UnpackPrefabInstance(gateHall.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform ring = gateHall.Find("ExitZones/ExitZone_South");
            PrefabUtility.UnpackPrefabInstance(hall.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            ring.SetParent(hall, false);
            ring.name = "ExitRing";
            ring.localPosition = new Vector3(0f, 0f, -9f);
            ring.localRotation = Quaternion.identity;
            ExitZone zone = ring.GetComponent<ExitZone>();
            SetRef(zone, "doorway", null);
            SetBool(zone, "alwaysLive", true);

            // The eight other practice rooms go.
            List<Transform> gone = new List<Transform>();
            foreach (Transform t in room6.transform) if (t.name.StartsWith("PRoom_") && t != hall) gone.Add(t);
            foreach (Transform t in gone) Object.DestroyImmediate(t.gameObject);

            // The hall: no grid doorways, four sealed openings, no room component (there is no grid to belong to).
            Transform doorways = hall.Find("Doorways");
            DestroyChild(doorways, "Doorway_North");
            DestroyChild(doorways, "Doorway_East");
            DestroyChild(doorways, "Doorway_South");
            DestroyChild(doorways, "Doorway_West");
            SealOpening(hall.gameObject, "Wall_North", new Vector3(0f, 1.75f, 12.25f), new Vector3(3f, 3.5f, 0.5f));
            SealOpening(hall.gameObject, "Wall_South", new Vector3(0f, 1.75f, -12.25f), new Vector3(3f, 3.5f, 0.5f));
            SealOpening(hall.gameObject, "Wall_East", new Vector3(12.25f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 3f));
            SealOpening(hall.gameObject, "Wall_West", new Vector3(-12.25f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 3f));
            LabyrinthRoom hallRoom = hall.GetComponent<LabyrinthRoom>();
            if (hallRoom != null) Object.DestroyImmediate(hallRoom);
            DestroyChild(hall.Find("Fixtures"), "FloorNumber");
            hall.name = "PracticeHall";

            // Signs: the run, the curses, the nudge's last line, the ring.
            Transform fixtures = hall.Find("Fixtures");
            RenameSign(fixtures, "Sign_Compass", "Sign_Run",
                "THE RUN: THE START ROOM, THEN FIVE ROOMS IN A ROW, THEN THE EXIT. EVERY ROOM'S EXIT DOOR IS SEALED UNTIL YOU STRIKE THE LEVER BESIDE IT. LEAVING THE START ROOM STARTS A 5:00 TIMER EVERYBODY SEES. THE WEAPONS WIN WHEN ALL OF THEM STAND IN THE EXIT ROOM AT ONCE. THE ARCH MAGE WINS THE INSTANT THE TIMER RUNS OUT.");
            RenameSign(fixtures, "Sign_Map", "Sign_Curse",
                "1-5 CASTS A CURSE ON WHO YOU LOOK AT, WITHIN 15 M. 1 MAGNETIC: THEIR LAUNCHES BEND TOWARD THE NEAREST WEAPON. 2 NAUSEA: THEIR VIEW SWAYS AND THEIR AIM DRIFTS. 3 SLIPPERY: THEY CANNOT SETTLE. 4 BLINDNESS: THEIR SCREEN CLOSES TO A SMALL CIRCLE. 5 HEAVY: HALF LAUNCH SPEED. 20 SECONDS EACH, ONE COOLDOWN FOR ALL FIVE. THE VICTIM NEVER SEES WHO. TRY THEM ON THE DUMMY: IT SHOWS WHAT IT GOT.");
            RenameSign(fixtures, "Sign_Nudge", "Sign_Nudge",
                "THE DUMMY HOPS EVERY FEW SECONDS. AS THE ARCH MAGE, LOOK AT IT WHILE IT IS IN THE AIR: LEFT CLICK NUDGES IT AWAY FROM YOU, RIGHT CLICK PULLS IT TOWARD YOU. ONLY WHILE THEY ARE IN THE AIR, CLOSE BY AND IN SIGHT. ON A FRIEND IT LOOKS LIKE A JUMP THAT WENT WRONG - ONLY A SOUL SEES THE WISP IT LEAVES. THE SMALL RINGS BY THE MIDDLE OF YOUR SCREEN SHOW WHEN A NUDGE (N) OR A CURSE (C) IS READY AGAIN.");
            GameObject exitSign = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SignPrefab), fixtures);
            exitSign.name = "Sign_Exit";
            exitSign.transform.localPosition = new Vector3(-4f, 0f, -8.5f);
            exitSign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SetString(exitSign.GetComponent<Sign>(), "text", "THE LIT RING ENDS THE TUTORIAL.");

            // The dummy: no compass spike, a label that names the curse it received.
            if (dummy != null)
            {
                SetRef(dummy, "labyrinth", null);
                SetRef(dummy, "hud", null);
                SetRef(dummy, "spikePivot", null);
                DestroyChild(dummy.transform, "SpikePivot");
                DestroyChild(dummy.transform, "Ring");
                Transform label = dummy.transform.Find("Label");
                SetRef(dummy, "label", label != null ? ComponentNamed(label, "TextMeshPro") : null);
                SetRef(dummy, "curseClock", clock);
            }

            // Managers and UI: no grid, no compass, RunHud asleep until the lesson, the curse pair.
            DestroyChild(managers.transform, "LabyrinthDirector");
            DestroyChild(managers.transform, "CompassModel");
            SetRef(authority, "labyrinth", null);
            DestroyChild(ui.transform, "LabyrinthHud");
            RunHud runHud = CreateRunHud(ui.transform, authority, runner, RunTutorialDefAsset, true);
            SetRef(nudge, "labyrinth", null);
            SetRef(nudge, "hud", null);
            SetRef(nudge, "runHud", runHud);
            InputActionAssetOf(nudge, out Object controls);
            CreateCurseObjects(managers.transform, authority, spawner, camera, clock, runHud, controls, RunTutorialDefAsset);
            foreach (TutorialTrigger trigger in room6.GetComponentsInChildren<TutorialTrigger>(true))
            {
                SerializedObject so = new SerializedObject(trigger);
                SerializedProperty wake = so.FindProperty("wakeHud");
                bool wakes = wake != null && wake.objectReferenceValue != null;
                so.Dispose();
                if (!wakes && trigger.name != "TutorialGate_MageLesson") continue;
                SetRef(trigger, "wakeHud", null);
                SetRef(trigger, "wakeRunHud", runHud);
            }
            foreach (DebugOverlay overlay in ui.GetComponentsInChildren<DebugOverlay>(true))
            {
                SetRef(overlay, "labyrinth", null);
                SetRef(overlay, "compass", null);
            }

            RebuildTutorialRegistries(scene, authority);
            AssignFreshSignIds(scene);
            RebakeNavMesh(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RunSceneBuilder] reworked " + TutorialScene + ": practice hall, RunHud, curse lesson");
        }

        static void RenameSign(Transform fixtures, string oldName, string newName, string text)
        {
            Transform t = fixtures != null ? fixtures.Find(oldName) : null;
            if (t == null) { Debug.LogWarning("No sign " + oldName); return; }
            t.name = newName;
            SetString(t.GetComponent<Sign>(), "text", text);
        }

        /// <summary>A component by type name (Pesky.Editor does not reference the TextMeshPro assembly).</summary>
        static Component ComponentNamed(Transform t, string typeName)
        {
            foreach (Component c in t.GetComponents<Component>())
                if (c != null && c.GetType().Name == typeName) return c;
            return null;
        }


        /// <summary>The tutorial's WorldAuthority keeps its authored arrays; only the entries that pointed at deleted objects are dropped.</summary>
        static void RebuildTutorialRegistries(Scene scene, WorldAuthority authority)
        {
            if (authority == null) return;
            SerializedObject so = new SerializedObject(authority);
            SerializedProperty p = so.GetIterator();
            bool enter = true;
            while (p.NextVisible(enter))
            {
                // Never descend into a string or an array: strings are char arrays and an array's elements are handled here.
                enter = !p.isArray && p.propertyType != SerializedPropertyType.String;
                if (!p.isArray || p.propertyType == SerializedPropertyType.String) continue;
                if (p.arraySize == 0 || p.GetArrayElementAtIndex(0).propertyType != SerializedPropertyType.ObjectReference) continue;
                for (int i = p.arraySize - 1; i >= 0; i--)
                {
                    if (p.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
                    p.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A new Sign in a scene whose ids were authored by hand gets the first id past the largest one in use.</summary>
        static void AssignFreshSignIds(Scene scene)
        {
            int max = 0;
            List<Sign> unassigned = new List<Sign>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    ISceneId identified = mb as ISceneId;
                    if (identified == null) continue;
                    if (identified.SceneId > max) max = identified.SceneId;
                    Sign sign = mb as Sign;
                    if (sign != null && identified.SceneId == 0) unassigned.Add(sign);
                }
            }
            foreach (Sign sign in unassigned) SetInt(sign, "id", ++max);
        }

        [MenuItem("Pesky/Run/Rebake NavMesh (open scene)")]
        public static void RebakeNavMeshMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            RebakeNavMesh(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void RebakeNavMesh(Scene scene)
        {
            // The AI Navigation package's Editor baker (NavMeshAssetManager) writes the NavMeshData asset next to
            // the scene, exactly as the Bake button does. Reached by reflection: Pesky.Editor does not reference it.
            List<Object> surfaces = new List<Object>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name == "NavMeshSurface") surfaces.Add(mb);
            if (surfaces.Count == 0) { Debug.LogWarning("[RunSceneBuilder] no NavMeshSurface to rebake"); return; }

            System.Type manager = null;
            foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                manager = asm.GetType("Unity.AI.Navigation.Editor.NavMeshAssetManager");
                if (manager != null) break;
            }
            if (manager != null)
            {
                // 'instance' is ScriptableSingleton<T>'s static property: look up the hierarchy for it.
                System.Reflection.PropertyInfo instanceProperty = manager.GetProperty("instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                System.Reflection.MethodInfo bake = manager.GetMethod("StartBakingSurfaces", new[] { typeof(Object[]) });
                if (instanceProperty == null || bake == null)
                {
                    Debug.LogWarning("[RunSceneBuilder] NavMeshAssetManager found but not its instance / StartBakingSurfaces; falling back to BuildNavMesh");
                    manager = null;
                }
                else
                {
                    object instance = instanceProperty.GetValue(null, null);
                    bake.Invoke(instance, new object[] { surfaces.ToArray() });
                }
            }
            if (manager != null)
            {
                Debug.Log("[RunSceneBuilder] baking " + surfaces.Count + " NavMeshSurface(s) through NavMeshAssetManager");
                return;
            }
            foreach (Object s in surfaces)
            {
                System.Reflection.MethodInfo build = s.GetType().GetMethod("BuildNavMesh", System.Type.EmptyTypes);
                if (build != null) build.Invoke(s, null);
            }
            Debug.Log("[RunSceneBuilder] rebaked " + surfaces.Count + " NavMeshSurface(s) with BuildNavMesh (no asset manager found)");
        }
    }
}
