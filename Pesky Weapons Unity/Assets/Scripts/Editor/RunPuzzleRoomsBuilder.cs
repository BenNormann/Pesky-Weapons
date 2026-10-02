using System.Collections.Generic;
using Pesky.Data;
using Pesky.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pesky.Editor
{
    /// <summary>Builds and installs the four readable, movement-first rooms used by the current run.</summary>
    public static class RunPuzzleRoomsBuilder
    {
        const string TwoDoorPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_TwoDoor.prefab";
        const string ClockMoverPrefab = "Assets/Prefabs/Kit/Movers/ClockMover.prefab";
        const string MagnetPrefab = "Assets/Prefabs/Kit/Hazards/MagnetZone.prefab";
        const string CounterweightPrefab = "Assets/Prefabs/Kit/Plates_And_Pans/CounterweightPair.prefab";
        const string LeverPrefab = "Assets/Prefabs/Kit/Movers/ImpactLever.prefab";
        const string LeverDoorPrefab = "Assets/Prefabs/Kit/Doors/Door_Lever.prefab";
        const string SignPrefab = "Assets/Prefabs/Kit/Signs_And_Lights/Sign.prefab";
        const string RunScene = "Assets/Scenes/Run.unity";
        const string RunDefAsset = "Assets/Data/Run.asset";
        const string LabyrinthDefAsset = "Assets/Data/Labyrinth.asset";
        const string MetalMaterial = "Assets/Materials/M_Metal.mat";
        const string GlowMaterial = "Assets/Materials/M_Glow.mat";
        const string HazardMaterial = "Assets/Materials/M_Hazard.mat";
        const int LayerWorld = 8;

        static readonly string[] RoomPrefabs =
        {
            "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_ResonanceForge.prefab",
            "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_ClockworkCanteen.prefab",
            "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_MagnetMayhem.prefab",
            "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_CounterweightComedy.prefab"
        };

        static readonly string[] RoomNames =
        {
            "Resonance Forge", "Clockwork Canteen", "Magnet Mayhem", "Counterweight Comedy"
        };

        [MenuItem("Pesky/Rooms/Build Four-Room Puzzle Run")]
        public static void BuildAll()
        {
            BananaWeaponBuilder.BuildAssetsAndRack();
            CreateAllPrefabs();
            ConfigureData();
            Scene scene = EditorSceneManager.OpenScene(RunScene, OpenSceneMode.Single);
            InstallIntoOpenRunScene(scene);
            Debug.Log("[RunPuzzleRooms] built four platform rooms and installed the four-room run.");
        }

        public static void CreateAllPrefabs()
        {
            ResonanceForgeRoomBuilder.CreateRoomPrefab();
            CreateClockworkCanteen();
            CreateMagnetMayhem();
            CreateCounterweightComedy();
            AssetDatabase.SaveAssets();
        }

        static void CreateClockworkCanteen()
        {
            GameObject room = NewRoom("LabyrinthRoom_ClockworkCanteen", "clockwork-canteen",
                "Clockwork Canteen", "Ride the moving serving trays and smack the service bell on the south balcony.", 2);
            if (room == null) return;
            Transform gameplay = room.transform.Find("Gameplay");
            Transform decor = NewGroup(room.transform, "PlatformCourse");
            Material metal = LoadMaterial(MetalMaterial);
            Material glow = LoadMaterial(GlowMaterial);

            CreateBlock(decor, "StarterStep", new Vector3(-4.8f, 0.55f, 6f), new Vector3(3.2f, 1.1f, 2.4f), metal, true);
            CreateBlock(decor, "MiddleStep", new Vector3(4.2f, 1.45f, 0.5f), new Vector3(3.2f, 2.9f, 2.4f), metal, true);
            CreateBlock(decor, "GoalBalcony", new Vector3(0f, 3.7f, -7.3f), new Vector3(7.5f, 0.6f, 3f), metal, true);

            CreateClockMover(gameplay, "Tray_A", new Vector3(-4.7f, 1.25f, 4f), new Vector3(-0.8f, 2.1f, 1.4f), 6f, 0f);
            CreateClockMover(gameplay, "Tray_B", new Vector3(3.8f, 2.2f, 1.4f), new Vector3(-0.5f, 3.15f, -2.6f), 7f, 0.33f);
            CreateClockMover(gameplay, "Tray_C", new Vector3(-1.8f, 3.2f, -3.2f), new Vector3(1.8f, 4.1f, -6.1f), 5f, 0.66f);

            ImpactLever lever = CreateLeverGate(room, new Vector3(0f, 4.35f, -7.1f), "ServiceBell");
            SetFloat(lever, "minSpeed", 2f);
            CreateBlock(decor, "BellGlow", new Vector3(0f, 4.05f, -7.1f), new Vector3(2.4f, 0.08f, 2.4f), glow, false);
            AddSigns(room, "CLOCKWORK CANTEEN",
                "RIDE THE SERVING TRAYS. HIT THE GREEN SERVICE BELL UP HIGH. FALLING DOWN IS FREE; DIGNITY COSTS EXTRA.",
                "TODAY'S SPECIAL: VELOCITY SOUP");
            SaveRoom(room, RoomPrefabs[1]);
        }

        static void CreateMagnetMayhem()
        {
            GameObject room = NewRoom("LabyrinthRoom_MagnetMayhem", "magnet-mayhem",
                "Magnet Mayhem", "Metal weapons ride ceiling magnets; wooden weapons take the chunky side platforms.", 2);
            if (room == null) return;
            Transform gameplay = room.transform.Find("Gameplay");
            Transform decor = NewGroup(room.transform, "MagnetCourse");
            Material metal = LoadMaterial(MetalMaterial);
            Material glow = LoadMaterial(GlowMaterial);

            CreateMagnet(gameplay, "Magnet_A", new Vector3(-4.5f, 6f, 4f));
            CreateMagnet(gameplay, "Magnet_B", new Vector3(0f, 6f, 0f));
            CreateMagnet(gameplay, "Magnet_C", new Vector3(4.5f, 6f, -4f));
            CreateBlock(decor, "WoodRoute_A", new Vector3(5.8f, 0.8f, 5.2f), new Vector3(3f, 1.6f, 2.6f), metal, true);
            CreateBlock(decor, "WoodRoute_B", new Vector3(6.2f, 2f, 0.8f), new Vector3(3f, 0.6f, 2.6f), metal, true);
            CreateBlock(decor, "WoodRoute_C", new Vector3(5.8f, 3.2f, -3.6f), new Vector3(3f, 0.6f, 2.6f), metal, true);
            CreateBlock(decor, "GoalBalcony", new Vector3(2f, 4.15f, -7.2f), new Vector3(8f, 0.6f, 3f), metal, true);
            CreateBlock(decor, "MagnetLanding_A", new Vector3(-4.5f, 4.05f, 4f), new Vector3(3f, 0.35f, 3f), metal, true);
            CreateBlock(decor, "MagnetLanding_B", new Vector3(0f, 4.05f, 0f), new Vector3(3f, 0.35f, 3f), metal, true);
            CreateBlock(decor, "MagnetLanding_C", new Vector3(4.5f, 4.05f, -4f), new Vector3(3f, 0.35f, 3f), metal, true);

            ImpactLever lever = CreateLeverGate(room, new Vector3(2f, 4.8f, -7f), "DefinitelyNotAFridgeMagnet");
            SetFloat(lever, "minSpeed", 2f);
            CreateBlock(decor, "GoalGlow", new Vector3(2f, 4.48f, -7f), new Vector3(2.4f, 0.08f, 2.4f), glow, false);
            AddSigns(room, "MAGNET MAYHEM",
                "METAL? LET THE CEILING MAGNET CATCH YOU. WOOD OR BANANA? USE THE BIG SIDE STEPS. HIT THE LEVER ON THE SOUTH BALCONY.",
                "WARNING: MAGNETIC PERSONALITIES MAY CLING");
            SaveRoom(room, RoomPrefabs[2]);
        }

        static void CreateCounterweightComedy()
        {
            GameObject room = NewRoom("LabyrinthRoom_CounterweightComedy", "counterweight-comedy",
                "Counterweight Comedy", "Park a heavy body on one pan so a friend rides the other pan up to the exit lever.", 2);
            if (room == null) return;
            Transform gameplay = room.transform.Find("Gameplay");
            Transform decor = NewGroup(room.transform, "CounterweightCourse");
            Material metal = LoadMaterial(MetalMaterial);
            Material hazard = LoadMaterial(HazardMaterial);

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CounterweightPrefab);
            GameObject pairGo = (GameObject)PrefabUtility.InstantiatePrefab(source, gameplay);
            pairGo.name = "FriendshipElevator";
            pairGo.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            pairGo.transform.localRotation = Quaternion.identity;

            CreateBlock(decor, "LoadingStep_West", new Vector3(-5.4f, 0.65f, 3.5f), new Vector3(3f, 1.3f, 2.8f), metal, true);
            CreateBlock(decor, "LoadingStep_East", new Vector3(5.4f, 0.65f, 3.5f), new Vector3(3f, 1.3f, 2.8f), metal, true);
            CreateBlock(decor, "GoalBalcony", new Vector3(3f, 5.1f, -7.2f), new Vector3(7f, 0.6f, 3f), metal, true);
            CreateBlock(decor, "ComedyStripe", new Vector3(0f, 0.04f, 5f), new Vector3(10f, 0.08f, 0.25f), hazard, false);

            ImpactLever lever = CreateLeverGate(room, new Vector3(3f, 5.75f, -7f), "UpperFloorButton");
            SetFloat(lever, "minSpeed", 2f);
            AddSigns(room, "COUNTERWEIGHT COMEDY",
                "PARK A HAMMER OR MACE ON ONE PAN. A FRIEND RIDES THE OTHER PAN UP, THEN HITS THE UPPER-FLOOR BUTTON. Q MAKES A LOVELY PAPERWEIGHT.",
                "THE BANANA IS NOT HEAVY. IT IS TRYING ITS BEST.");
            SaveRoom(room, RoomPrefabs[3]);
        }

        static GameObject NewRoom(string objectName, string puzzleId, string displayName, string summary, int tier)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TwoDoorPrefab);
            if (source == null) { Debug.LogError("Missing " + TwoDoorPrefab); return null; }
            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(source);
            room.name = objectName;
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            DestroyChild(room.transform.Find("Gameplay"), "Seal");
            DestroyChild(room.transform.Find("Doorways"), "Gate_South");
            PuzzleRoomProfile profile = room.GetComponent<PuzzleRoomProfile>();
            if (profile == null) profile = room.AddComponent<PuzzleRoomProfile>();
            SetString(profile, "puzzleId", puzzleId);
            SetString(profile, "displayName", displayName);
            SetString(profile, "summary", summary);
            SetInt(profile, "minimumPlayers", 3);
            SetInt(profile, "optimalPlayers", 4);
            SetInt(profile, "maximumPlayers", 8);
            SetFloat(profile, "expectedSolveSeconds", 45f);
            SetInt(profile, "difficultyTier", tier);
            return room;
        }

        static ImpactLever CreateLeverGate(GameObject room, Vector3 leverPosition, string leverName)
        {
            Transform gameplay = room.transform.Find("Gameplay");
            Transform doorways = room.transform.Find("Doorways");
            GameObject leverGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LeverPrefab), gameplay);
            leverGo.name = leverName;
            leverGo.transform.localPosition = leverPosition;
            leverGo.transform.localRotation = Quaternion.identity;
            ImpactLever lever = leverGo.GetComponent<ImpactLever>();
            SetBool(lever, "latching", true);

            GameObject gateGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LeverDoorPrefab), doorways);
            gateGo.name = "Gate_South_" + leverName;
            gateGo.transform.localPosition = new Vector3(0f, 0f, -12f);
            gateGo.transform.localRotation = Quaternion.identity;
            Door gate = gateGo.GetComponent<Door>();
            SetRef(gate.Condition, "lever", lever);
            Transform south = doorways.Find("Doorway_South");
            if (south != null) SetRef(south.GetComponent<MagicDoor>(), "gate", gate);
            return lever;
        }

        static void CreateClockMover(Transform parent, string name, Vector3 a, Vector3 b, float period, float phase)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ClockMoverPrefab);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            go.name = name;
            go.transform.localPosition = a;
            go.transform.localRotation = Quaternion.identity;
            Transform pointA = Marker(parent, name + "_A", a);
            Transform pointB = Marker(parent, name + "_B", b);
            ClockMover mover = go.GetComponent<ClockMover>();
            SetRef(mover, "pointA", pointA);
            SetRef(mover, "pointB", pointB);
            SetFloat(mover, "periodSeconds", period);
            SetFloat(mover, "phase", phase);
            SetVector3(mover, "riderBoxCenter", new Vector3(0f, 1.05f, 0f));
            SetVector3(mover, "riderBoxSize", new Vector3(3.6f, 2.1f, 3.6f));
            SetFloat(mover, "riderBrake", 18f);
            SetFloat(mover, "brakeLaunchGrace", 0.75f);
        }

        static void CreateMagnet(Transform parent, string name, Vector3 position)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(MagnetPrefab);
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            SetFloat(go.GetComponent<MagnetZone>(), "pullAcceleration", 34f);
        }

        static void AddSigns(GameObject room, string roomTitle, string instructions, string joke)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SignPrefab);
            Transform fixtures = room.transform.Find("Fixtures");
            Transform existing = fixtures != null ? fixtures.Find("RoomSign") : null;
            if (existing != null) SetString(existing.GetComponent<Sign>(), "text", roomTitle);
            CreateSign(source, fixtures, "Instructions", new Vector3(0f, 0f, 9f), Quaternion.identity, instructions);
            CreateSign(source, fixtures, "JokeSign", new Vector3(-7f, 0f, 6f), Quaternion.identity, joke);
        }

        static void ConfigureData()
        {
            LabyrinthDef labyrinth = AssetDatabase.LoadAssetAtPath<LabyrinthDef>(LabyrinthDefAsset);
            if (labyrinth != null)
            {
                SerializedObject so = new SerializedObject(labyrinth);
                SerializedProperty rooms = so.FindProperty("rooms");
                string[] glyphs = { "F", "C", "M", "W" };
                for (int i = 0; i < RoomNames.Length; i++)
                {
                    int id = i + 3;
                    if (rooms == null || rooms.arraySize <= id) continue;
                    SerializedProperty entry = rooms.GetArrayElementAtIndex(id);
                    entry.FindPropertyRelative("label").stringValue = RoomNames[i];
                    entry.FindPropertyRelative("glyph").stringValue = glyphs[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(labyrinth);
            }
            RunDef run = AssetDatabase.LoadAssetAtPath<RunDef>(RunDefAsset);
            if (run != null)
            {
                SerializedObject so = new SerializedObject(run);
                so.FindProperty("roomsPerRun").intValue = 4;
                SerializedProperty ids = so.FindProperty("guaranteedRoomIds");
                ids.arraySize = 4;
                for (int i = 0; i < 4; i++) ids.GetArrayElementAtIndex(i).intValue = i + 3;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(run);
            }
            AssetDatabase.SaveAssets();
        }

        public static void InstallIntoOpenRunScene(Scene scene)
        {
            if (!scene.IsValid() || scene.path != RunScene) { Debug.LogError("[RunPuzzleRooms] expected Run scene, got " + scene.path); return; }
            for (int i = 0; i < RoomPrefabs.Length; i++) if (AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabs[i]) == null) CreateAllPrefabs();
            GameObject environment = FindRoot(scene, "Environment");
            GameObject managers = FindRoot(scene, "_Managers");
            Transform roomsRoot = environment != null ? environment.transform.Find("Rooms") : null;
            if (roomsRoot == null || managers == null) { Debug.LogError("Run scene is missing rooms or managers."); return; }
            for (int i = 0; i < RoomPrefabs.Length; i++) ReplaceRoom(roomsRoot, i + 3, RoomPrefabs[i], RoomNames[i]);

            WorldAuthority authority = managers.GetComponentInChildren<WorldAuthority>(true);
            LabyrinthDirector director = managers.GetComponentInChildren<LabyrinthDirector>(true);
            RunDirector run = managers.GetComponentInChildren<RunDirector>(true);
            LevelClock clock = managers.GetComponentInChildren<LevelClock>(true);
            for (int i = 0; i < RoomPrefabs.Length; i++)
            {
                Transform room = roomsRoot.Find("Room_" + (i + 3).ToString("00") + "_" + SafeName(RoomNames[i]));
                if (room != null) WireSceneReferences(room.gameObject, authority, director, clock, RoomNames[i]);
            }
            RunHud hud = Object.FindFirstObjectByType<RunHud>(FindObjectsInactive.Include);
            if (hud != null)
            {
                SetString(hud, "weaponSub", "four rooms, one exit, five minutes. one of you is not helping. V calls a vote.");
                SetString(hud, "escapedTitle", "THE WEAPONS ESCAPED (SOMEHOW)");
            }
            RebuildSceneRegistries(scene, roomsRoot, authority, director, run);
            RunSceneBuilder.AssignSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[RunPuzzleRooms] installed rooms 3-6; the Exit room follows room four.");
        }

        static void ReplaceRoom(Transform roomsRoot, int roomId, string prefabPath, string displayName)
        {
            Transform old = null;
            foreach (Transform child in roomsRoot)
            {
                LabyrinthRoom candidate = child.GetComponent<LabyrinthRoom>();
                if (candidate != null && candidate.RoomId == roomId) { old = child; break; }
            }
            if (old == null) { Debug.LogError("No room id " + roomId + " in Run.unity"); return; }
            int sibling = old.GetSiblingIndex();
            Vector3 position = old.position;
            Quaternion rotation = old.rotation;
            Object.DestroyImmediate(old.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, roomsRoot);
            room.name = "Room_" + roomId.ToString("00") + "_" + SafeName(displayName);
            room.transform.SetPositionAndRotation(position, rotation);
            room.transform.SetSiblingIndex(sibling);
            SetInt(room.GetComponent<LabyrinthRoom>(), "roomId", roomId);
        }

        static void WireSceneReferences(GameObject room, WorldAuthority authority, LabyrinthDirector director, LevelClock clock, string roomName)
        {
            foreach (MagicDoor x in room.GetComponentsInChildren<MagicDoor>(true)) { SetRef(x, "authority", authority); SetRef(x, "labyrinth", director); }
            foreach (Door x in room.GetComponentsInChildren<Door>(true)) SetRef(x, "authority", authority);
            foreach (ImpactLever x in room.GetComponentsInChildren<ImpactLever>(true)) SetRef(x, "authority", authority);
            foreach (PressurePlate x in room.GetComponentsInChildren<PressurePlate>(true)) SetRef(x, "authority", authority);
            foreach (ScalesLock x in room.GetComponentsInChildren<ScalesLock>(true)) SetRef(x, "authority", authority);
            foreach (ClockMover x in room.GetComponentsInChildren<ClockMover>(true)) SetRef(x, "clock", clock);
            foreach (CounterweightPair x in room.GetComponentsInChildren<CounterweightPair>(true)) { SetRef(x, "authority", authority); SetRef(x, "clock", clock); }
            foreach (LightningField x in room.GetComponentsInChildren<LightningField>(true)) { SetRef(x, "authority", authority); SetRef(x, "clock", clock); }
            foreach (Lift x in room.GetComponentsInChildren<Lift>(true)) SetRef(x, "clock", clock);
            RoomVolume volume = room.GetComponentInChildren<RoomVolume>(true);
            if (volume != null) SetString(volume, "roomName", roomName);
        }

        static void RebuildSceneRegistries(Scene scene, Transform roomsRoot, WorldAuthority authority, LabyrinthDirector director, RunDirector run)
        {
            List<LabyrinthRoom> allRooms = ComponentsInScene<LabyrinthRoom>(scene);
            if (director != null) SetArray(director, "rooms", allRooms);
            if (run != null)
            {
                List<LabyrinthRoom> pool = new List<LabyrinthRoom>();
                foreach (LabyrinthRoom room in allRooms)
                    if (room != null && room != run.StartRoom && room != run.ExitRoom && room.transform.parent == roomsRoot) pool.Add(room);
                SetArray(run, "pool", pool);
            }
            if (authority == null) return;
            SetArray(authority, "weapons", ComponentsInScene<WeaponBody>(scene));
            SetArray(authority, "magicDoors", ComponentsInScene<MagicDoor>(scene));
            SetArray(authority, "rooms", ComponentsInScene<RoomVolume>(scene));
            SetArray(authority, "doors", ComponentsInScene<Door>(scene));
            SetArray(authority, "plates", ComponentsInScene<PressurePlate>(scene));
            SetArray(authority, "levers", ComponentsInScene<ImpactLever>(scene));
            SetArray(authority, "counterweights", ComponentsInScene<CounterweightPair>(scene));
            SetArray(authority, "scales", ComponentsInScene<ScalesLock>(scene));
            SetArray(authority, "lightningFields", ComponentsInScene<LightningField>(scene));
            SetArray(authority, "magnets", ComponentsInScene<MagnetZone>(scene));
            SetArray(authority, "lifts", ComponentsInScene<Lift>(scene));
        }

        static List<T> ComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        static Transform Marker(Transform parent, string name, Vector3 position) { GameObject go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform; }
        static Transform NewGroup(Transform parent, string name) { GameObject go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static void CreateSign(GameObject source, Transform parent, string name, Vector3 position, Quaternion rotation, string text)
        {
            if (source == null || parent == null) return;
            GameObject sign = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            sign.name = name;
            sign.transform.localPosition = position;
            sign.transform.localRotation = rotation;
            SetString(sign.GetComponent<Sign>(), "text", text);
        }
        static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.layer = LayerWorld;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
            return go;
        }

        static Material LoadMaterial(string path) { return AssetDatabase.LoadAssetAtPath<Material>(path); }
        static string SafeName(string value) { return value.Replace(" ", ""); }
        static void SaveRoom(GameObject room, string path) { PrefabUtility.SaveAsPrefabAsset(room, path); Object.DestroyImmediate(room); }
        static void DestroyChild(Transform parent, string name) { Transform child = parent != null ? parent.Find(name) : null; if (child != null) Object.DestroyImmediate(child.gameObject); }
        static GameObject FindRoot(Scene scene, string name) { foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == name) return go; return null; }
        static void SetRef(Object target, string property, Object value) { SetProperty(target, property, p => p.objectReferenceValue = value); }
        static void SetString(Object target, string property, string value) { SetProperty(target, property, p => p.stringValue = value); }
        static void SetInt(Object target, string property, int value) { SetProperty(target, property, p => p.intValue = value); }
        static void SetFloat(Object target, string property, float value) { SetProperty(target, property, p => p.floatValue = value); }
        static void SetVector3(Object target, string property, Vector3 value) { SetProperty(target, property, p => p.vector3Value = value); }
        static void SetBool(Object target, string property, bool value) { SetProperty(target, property, p => p.boolValue = value); }
        static void SetProperty(Object target, string name, System.Action<SerializedProperty> write)
        {
            if (target == null) { Debug.LogError("Missing target for " + name); return; }
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(name);
            if (property == null) { Debug.LogError(target.GetType().Name + " has no property " + name); return; }
            write(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void SetArray<T>(Object target, string property, List<T> values) where T : Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError(target.GetType().Name + " has no " + property); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
