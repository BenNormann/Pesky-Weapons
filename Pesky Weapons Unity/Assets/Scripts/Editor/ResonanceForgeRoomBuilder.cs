using System.Collections.Generic;
using Pesky.Data;
using Pesky.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pesky.Editor
{
    /// <summary>
    /// Reproducible authoring for the first real run puzzle. It creates a prefab variant from the shared
    /// two-door shell, installs it as room id 3 in Run.unity, wires scene-only authority references, and
    /// marks the room as guaranteed while the authored puzzle pool is small.
    /// </summary>
    public static class ResonanceForgeRoomBuilder
    {
        const string TwoDoorPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_TwoDoor.prefab";
        const string ScalesPrefab = "Assets/Prefabs/Kit/Plates_And_Pans/ScalesLock.prefab";
        const string DoorScalesPrefab = "Assets/Prefabs/Kit/Doors/Door_Scales.prefab";
        const string SignPrefab = "Assets/Prefabs/Kit/Signs_And_Lights/Sign.prefab";
        const string RoomPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_ResonanceForge.prefab";
        const string RunScene = "Assets/Scenes/Run.unity";
        const string LabyrinthDefAsset = "Assets/Data/Labyrinth.asset";
        const string RunDefAsset = "Assets/Data/Run.asset";
        const string GlowMaterial = "Assets/Materials/M_Glow.mat";
        const string MetalMaterial = "Assets/Materials/M_Metal.mat";
        const string HazardMaterial = "Assets/Materials/M_Hazard.mat";
        const int RoomId = 3;
        const int LayerWorld = 8;

        [MenuItem("Pesky/Rooms/Build Resonance Forge")]
        public static void BuildAll()
        {
            CreateRoomPrefab();
            ConfigureData();
            Scene scene = EditorSceneManager.OpenScene(RunScene, OpenSceneMode.Single);
            InstallIntoOpenRunScene(scene);
            Debug.Log("[ResonanceForge] built prefab, installed room 3, and guaranteed it in five-room runs.");
        }

        public static GameObject CreateRoomPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TwoDoorPrefab);
            GameObject scalesSource = AssetDatabase.LoadAssetAtPath<GameObject>(ScalesPrefab);
            GameObject gateSource = AssetDatabase.LoadAssetAtPath<GameObject>(DoorScalesPrefab);
            GameObject signSource = AssetDatabase.LoadAssetAtPath<GameObject>(SignPrefab);
            if (source == null || scalesSource == null || gateSource == null || signSource == null)
            {
                Debug.LogError("[ResonanceForge] one or more source prefabs are missing.");
                return null;
            }

            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(source);
            room.name = "LabyrinthRoom_ResonanceForge";
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Transform gameplay = room.transform.Find("Gameplay");
            Transform doorways = room.transform.Find("Doorways");
            DestroyChild(gameplay, "Seal");
            DestroyChild(doorways, "Gate_South");

            GameObject scalesGo = (GameObject)PrefabUtility.InstantiatePrefab(scalesSource, gameplay);
            scalesGo.name = "ResonanceScales";
            scalesGo.transform.localPosition = new Vector3(0f, 0f, -1.5f);
            scalesGo.transform.localRotation = Quaternion.identity;
            ScalesLock scales = scalesGo.GetComponent<ScalesLock>();

            GameObject gate = (GameObject)PrefabUtility.InstantiatePrefab(gateSource, doorways);
            gate.name = "Gate_South_Resonance";
            gate.transform.localPosition = new Vector3(0f, 0f, -12f);
            gate.transform.localRotation = Quaternion.identity;
            DoorCondition condition = gate.GetComponent<DoorCondition>();
            SetRef(condition, "scales", scales);
            Transform south = doorways.Find("Doorway_South");
            if (south != null) SetRef(south.GetComponent<MagicDoor>(), "gate", gate.GetComponent<Door>());

            PuzzleRoomProfile profile = room.AddComponent<PuzzleRoomProfile>();
            SetString(profile, "puzzleId", "resonance-forge");
            SetString(profile, "displayName", "Resonance Forge");
            SetString(profile, "summary", "Park light, medium and heavy weapon bodies on the three resonance scales at the same time.");
            SetInt(profile, "minimumPlayers", 3);
            SetInt(profile, "optimalPlayers", 4);
            SetInt(profile, "maximumPlayers", 8);
            SetFloat(profile, "expectedSolveSeconds", 55f);
            SetInt(profile, "difficultyTier", 2);

            Transform decor = new GameObject("ForgeDecor").transform;
            decor.SetParent(room.transform, false);
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterial);
            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterial);
            Material hazard = AssetDatabase.LoadAssetAtPath<Material>(HazardMaterial);

            // A waist-high forge splits the entry lane and makes the team fan out toward the three pans.
            CreateBlock(decor, "ForgeCore", new Vector3(0f, 0.75f, 3f), new Vector3(4f, 1.5f, 2f), metal, true);
            CreateBlock(decor, "Buttress_West", new Vector3(-8.5f, 1.25f, -1f), new Vector3(1f, 2.5f, 7f), metal, true);
            CreateBlock(decor, "Buttress_East", new Vector3(8.5f, 1.25f, -1f), new Vector3(1f, 2.5f, 7f), metal, true);
            // Broad launch islands turn the three pans into a forgiving platform approach instead of a
            // flat-floor sorting exercise. Their different heights teach short, medium and assisted arcs.
            CreateBlock(decor, "LaunchIsland_Light", new Vector3(-6f, 0.35f, 2.4f), new Vector3(3.2f, 0.7f, 3f), metal, true);
            CreateBlock(decor, "LaunchIsland_Medium", new Vector3(0f, 0.65f, 0.9f), new Vector3(3.2f, 1.3f, 3f), metal, true);
            CreateBlock(decor, "LaunchIsland_Heavy", new Vector3(6f, 0.95f, 2.4f), new Vector3(3.2f, 1.9f, 3f), metal, true);

            // Thin emissive traces make each scale's relationship to the south seal readable at a glance.
            for (int i = 0; i < 3; i++)
            {
                float x = -3f + i * 3f;
                CreateBlock(decor, "ResonanceTrace_" + i, new Vector3(x, 0.025f, -6.1f),
                    new Vector3(0.18f, 0.05f, 4.5f), glow, false);
                CreateCylinder(decor, "ResonanceBeacon_" + i, new Vector3(x, 1.35f, 0.4f),
                    new Vector3(0.22f, 1.35f, 0.22f), glow);
            }
            CreateBlock(decor, "DangerStripe_West", new Vector3(-5.3f, 0.035f, 4.15f), new Vector3(4f, 0.07f, 0.22f), hazard, false);
            CreateBlock(decor, "DangerStripe_East", new Vector3(5.3f, 0.035f, 4.15f), new Vector3(4f, 0.07f, 0.22f), hazard, false);

            Transform fixtures = room.transform.Find("Fixtures");
            CreateSign(signSource, fixtures, "Instructions", new Vector3(0f, 0f, 9f), Quaternion.identity,
                "RESONANCE FORGE: PARK LIGHT, MEDIUM AND HEAVY WEAPONS ON ALL THREE SCALES AT ONCE. Q LEAVES A BODY IN PLACE. WHEN ALL THREE GLOW, THE SOUTH SEAL LATCHES.");
            CreateSign(signSource, fixtures, "LightGuide", new Vector3(-7f, 0f, 5.6f), Quaternion.identity,
                "LIGHT: BANANA OR DAGGER");
            CreateSign(signSource, fixtures, "MediumGuide", new Vector3(0f, 0f, 5.6f), Quaternion.Euler(0f, 180f, 0f),
                "MEDIUM: STAFF, SWORD OR ORB");
            CreateSign(signSource, fixtures, "HeavyGuide", new Vector3(7f, 0f, 5.6f), Quaternion.Euler(0f, 180f, 0f),
                "HEAVY: MACE OR HAMMER");
            CreateSign(signSource, fixtures, "JokeSign", new Vector3(-7f, 0f, 8f), Quaternion.identity,
                "FORGE POLICY: NO REFUNDS AFTER SOUL REMOVAL");
            Transform roomSign = fixtures != null ? fixtures.Find("RoomSign") : null;
            if (roomSign != null) SetString(roomSign.GetComponent<Sign>(), "text", "RESONANCE FORGE");

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(room, RoomPrefab);
            Object.DestroyImmediate(room);
            AssetDatabase.SaveAssets();
            Debug.Log("[ResonanceForge] wrote " + RoomPrefab);
            return saved;
        }

        public static void InstallIntoOpenRunScene(Scene scene)
        {
            if (!scene.IsValid() || scene.path != RunScene)
            {
                Debug.LogError("[ResonanceForge] expected the open Run scene, got " + scene.path);
                return;
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefab);
            if (prefab == null) prefab = CreateRoomPrefab();
            if (prefab == null) return;

            GameObject environment = FindRoot(scene, "Environment");
            GameObject managers = FindRoot(scene, "_Managers");
            Transform roomsRoot = environment != null ? environment.transform.Find("Rooms") : null;
            if (roomsRoot == null || managers == null)
            {
                Debug.LogError("[ResonanceForge] Run scene is missing Environment/Rooms or _Managers.");
                return;
            }

            Transform old = roomsRoot.Find("Room_03_Pool");
            if (old == null) old = roomsRoot.Find("Room_03_ResonanceForge");
            if (old == null)
            {
                foreach (Transform child in roomsRoot)
                {
                    LabyrinthRoom candidate = child.GetComponent<LabyrinthRoom>();
                    if (candidate != null && candidate.RoomId == RoomId) { old = child; break; }
                }
            }
            if (old == null)
            {
                Debug.LogError("[ResonanceForge] could not find room id 3 in Run.unity.");
                return;
            }

            int sibling = old.GetSiblingIndex();
            Vector3 position = old.position;
            Quaternion rotation = old.rotation;
            Object.DestroyImmediate(old.gameObject);

            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, roomsRoot);
            room.name = "Room_03_ResonanceForge";
            room.transform.SetPositionAndRotation(position, rotation);
            room.transform.SetSiblingIndex(sibling);
            LabyrinthRoom labyrinthRoom = room.GetComponent<LabyrinthRoom>();
            SetInt(labyrinthRoom, "roomId", RoomId);

            WorldAuthority authority = managers.GetComponentInChildren<WorldAuthority>(true);
            LabyrinthDirector director = managers.GetComponentInChildren<LabyrinthDirector>(true);
            RunDirector run = managers.GetComponentInChildren<RunDirector>(true);
            WireSceneReferences(room, authority, director);
            RebuildSceneRegistries(scene, roomsRoot, authority, director, run);
            RunSceneBuilder.AssignSceneIds(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ResonanceForge] installed room id 3 in " + RunScene);
        }

        static void ConfigureData()
        {
            LabyrinthDef labyrinth = AssetDatabase.LoadAssetAtPath<LabyrinthDef>(LabyrinthDefAsset);
            if (labyrinth != null)
            {
                SerializedObject so = new SerializedObject(labyrinth);
                SerializedProperty rooms = so.FindProperty("rooms");
                if (rooms != null && rooms.arraySize > RoomId)
                {
                    SerializedProperty room = rooms.GetArrayElementAtIndex(RoomId);
                    room.FindPropertyRelative("label").stringValue = "Resonance Forge";
                    room.FindPropertyRelative("glyph").stringValue = "F";
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(labyrinth);
                }
            }

            RunDef run = AssetDatabase.LoadAssetAtPath<RunDef>(RunDefAsset);
            if (run != null)
            {
                SerializedObject so = new SerializedObject(run);
                SerializedProperty ids = so.FindProperty("guaranteedRoomIds");
                ids.arraySize = 1;
                ids.GetArrayElementAtIndex(0).intValue = RoomId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(run);
            }
            AssetDatabase.SaveAssets();
        }

        static void WireSceneReferences(GameObject room, WorldAuthority authority, LabyrinthDirector director)
        {
            foreach (MagicDoor door in room.GetComponentsInChildren<MagicDoor>(true))
            {
                SetRef(door, "authority", authority);
                SetRef(door, "labyrinth", director);
            }
            foreach (Door door in room.GetComponentsInChildren<Door>(true)) SetRef(door, "authority", authority);
            foreach (ScalesLock scales in room.GetComponentsInChildren<ScalesLock>(true)) SetRef(scales, "authority", authority);
            RoomVolume volume = room.GetComponentInChildren<RoomVolume>(true);
            if (volume != null) SetString(volume, "roomName", "Resonance Forge");
        }

        static void RebuildSceneRegistries(Scene scene, Transform roomsRoot, WorldAuthority authority,
            LabyrinthDirector director, RunDirector run)
        {
            List<LabyrinthRoom> allRooms = ComponentsInScene<LabyrinthRoom>(scene);
            if (director != null) SetArray(director, "rooms", allRooms);
            if (run != null)
            {
                List<LabyrinthRoom> pool = new List<LabyrinthRoom>();
                for (int i = 0; i < allRooms.Count; i++)
                {
                    LabyrinthRoom room = allRooms[i];
                    if (room == null || room == run.StartRoom || room == run.ExitRoom) continue;
                    if (room.transform.parent == roomsRoot) pool.Add(room);
                }
                SetArray(run, "pool", pool);
            }
            if (authority == null) return;
            SetArray(authority, "magicDoors", ComponentsInScene<MagicDoor>(scene));
            SetArray(authority, "rooms", ComponentsInScene<RoomVolume>(scene));
            SetArray(authority, "doors", ComponentsInScene<Door>(scene));
            SetArray(authority, "levers", ComponentsInScene<ImpactLever>(scene));
            SetArray(authority, "scales", ComponentsInScene<ScalesLock>(scene));
        }

        static List<T> ComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        static void CreateSign(GameObject source, Transform parent, string name, Vector3 position,
            Quaternion rotation, string text)
        {
            GameObject sign = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            sign.name = name;
            sign.transform.localPosition = position;
            sign.transform.localRotation = rotation;
            SetString(sign.GetComponent<Sign>(), "text", text);
        }

        static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, bool collision)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.layer = LayerWorld;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            if (!collision)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
            return go;
        }

        static GameObject CreateCylinder(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.layer = LayerWorld;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
            return go;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
            return null;
        }

        static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        static void SetRef(Object target, string property, Object value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string property, string value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string property, int value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string property, float value)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray<T>(Object target, string property, List<T> values) where T : Object
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) return;
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
