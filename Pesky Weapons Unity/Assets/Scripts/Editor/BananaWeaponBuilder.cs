using System.Collections.Generic;
using Pesky.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pesky.Editor
{
    /// <summary>Builds the readable cartoon Banana and keeps slot seven of every starting rack wired to it.</summary>
    public static class BananaWeaponBuilder
    {
        const string BananaPrefab = "Assets/Prefabs/Weapons/Banana.prefab";
        const string StartRoomPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_Start.prefab";
        const string BananaMaterial = "Assets/Materials/M_Banana.mat";
        const string WoodMaterial = "Assets/Materials/M_Wood.mat";
        const string HighlightMaterial = "Assets/Materials/M_Banana_Highlight.mat";
        const string SpotMaterial = "Assets/Materials/M_Banana_Spot.mat";
        const int WeaponLayer = 9;

        static readonly string[] RackScenes =
        {
            "Assets/Scenes/Run.unity",
            "Assets/Scenes/Labyrinth.unity"
        };

        [MenuItem("Pesky/Weapons/Polish Banana And Add To Rack")]
        public static void Build()
        {
            string returnScene = SceneManager.GetActiveScene().path;
            BuildAssetsAndRack();
            for (int i = 0; i < RackScenes.Length; i++) RefreshSceneWeaponRegistry(RackScenes[i]);
            if (!string.IsNullOrEmpty(returnScene) && SceneManager.GetActiveScene().path != returnScene)
                EditorSceneManager.OpenScene(returnScene, OpenSceneMode.Single);
            Debug.Log("[BananaWeapon] polished Banana prefab and installed it in rack slot 7.");
        }

        public static void BuildAssetsAndRack()
        {
            Material yellow = AssetDatabase.LoadAssetAtPath<Material>(BananaMaterial);
            Material wood = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterial);
            if (yellow == null || wood == null)
            {
                Debug.LogError("[BananaWeapon] missing Banana or Wood material.");
                return;
            }

            Material highlight = Tint(HighlightMaterial, yellow, new Color(1f, 0.96f, 0.42f, 1f), 0.42f);
            Material spot = Tint(SpotMaterial, wood, new Color(0.20f, 0.075f, 0.02f, 1f), 0.18f);
            PolishBanana(yellow, highlight, spot);
            InstallOnRack();
            AssetDatabase.SaveAssets();
        }

        static void PolishBanana(Material yellow, Material highlight, Material spot)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BananaPrefab);
            if (root == null) { Debug.LogError("[BananaWeapon] missing " + BananaPrefab); return; }

            List<GameObject> old = new List<GameObject>();
            foreach (Transform child in root.transform) old.Add(child.gameObject);
            for (int i = 0; i < old.Count; i++) Object.DestroyImmediate(old[i]);

            GameObject peel = new GameObject("Peel");
            peel.layer = WeaponLayer;
            peel.transform.SetParent(root.transform, false);

            Vector3[] points =
            {
                new Vector3(0.30f, 0f, -0.58f),
                new Vector3(0.13f, 0f, -0.38f),
                new Vector3(0.025f, 0f, -0.14f),
                new Vector3(0.025f, 0f, 0.14f),
                new Vector3(0.13f, 0f, 0.38f),
                new Vector3(0.30f, 0f, 0.58f)
            };

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 direction = points[i + 1] - points[i];
                Vector3 middle = (points[i] + points[i + 1]) * 0.5f;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                float halfLength = Mathf.Max(0.135f, direction.magnitude * 0.55f);

                GameObject segment = Primitive(PrimitiveType.Capsule, "Peel_" + (i + 1), peel.transform,
                    middle, rotation, new Vector3(0.25f, halfLength, 0.25f), yellow, true);
                CapsuleCollider collider = segment.GetComponent<CapsuleCollider>();
                if (collider != null) collider.radius = 0.48f;

                Primitive(PrimitiveType.Capsule, "Highlight_" + (i + 1), peel.transform,
                    middle + Vector3.up * 0.122f, rotation, new Vector3(0.055f, halfLength * 0.92f, 0.055f),
                    highlight, false);
            }

            Vector3 stemDirection = (points[points.Length - 1] - points[points.Length - 2]).normalized;
            Vector3 stemEnd = points[points.Length - 1] + stemDirection * 0.19f;
            Primitive(PrimitiveType.Cylinder, "Stem", peel.transform,
                (points[points.Length - 1] + stemEnd) * 0.5f,
                Quaternion.FromToRotation(Vector3.up, stemDirection), new Vector3(0.075f, 0.10f, 0.075f), spot, false);
            Primitive(PrimitiveType.Sphere, "DarkTip", peel.transform, points[0], Quaternion.identity,
                new Vector3(0.16f, 0.12f, 0.16f), spot, false);

            Vector3[] spots =
            {
                new Vector3(0.095f, 0.125f, -0.28f),
                new Vector3(0.025f, 0.125f, 0.02f),
                new Vector3(0.13f, 0.125f, 0.36f)
            };
            for (int i = 0; i < spots.Length; i++)
                Primitive(PrimitiveType.Sphere, "RipeSpot_" + (i + 1), peel.transform, spots[i], Quaternion.identity,
                    new Vector3(0.05f, 0.025f, 0.05f), spot, false);

            PrefabUtility.SaveAsPrefabAsset(root, BananaPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void InstallOnRack()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(StartRoomPrefab);
            if (root == null) { Debug.LogError("[BananaWeapon] missing " + StartRoomPrefab); return; }
            Transform weapons = root.transform.Find("Gameplay/Weapons");
            Transform slotTransform = root.transform.Find("Gameplay/WeaponRack/Slot_7");
            WeaponHomeSlot slot = slotTransform != null ? slotTransform.GetComponent<WeaponHomeSlot>() : null;
            GameObject bananaSource = AssetDatabase.LoadAssetAtPath<GameObject>(BananaPrefab);
            if (weapons == null || slot == null || bananaSource == null)
            {
                Debug.LogError("[BananaWeapon] the starting room is missing Weapons, Slot_7, or the Banana prefab.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }

            List<GameObject> replace = new List<GameObject>();
            foreach (Transform child in weapons)
            {
                WeaponBody body = child.GetComponent<WeaponBody>();
                if (body != null && (body.HomeSlot == slot || child.name == "Banana_7")) replace.Add(child.gameObject);
            }
            for (int i = 0; i < replace.Count; i++) Object.DestroyImmediate(replace[i]);

            GameObject banana = (GameObject)PrefabUtility.InstantiatePrefab(bananaSource, weapons);
            banana.name = "Banana_7";
            banana.transform.SetPositionAndRotation(slot.Position, slot.Rotation);
            WeaponBody weapon = banana.GetComponent<WeaponBody>();
            SetRef(weapon, "homeSlot", slot);
            SetInt(weapon, "id", 7);
            weapon.ApplyDef();

            PrefabUtility.SaveAsPrefabAsset(root, StartRoomPrefab);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void RefreshSceneWeaponRegistry(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            WorldAuthority authority = Object.FindFirstObjectByType<WorldAuthority>(FindObjectsInactive.Include);
            if (authority != null) SetArray(authority, "weapons", ComponentsInScene<WeaponBody>(scene));
            RunSceneBuilder.AssignSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position,
            Quaternion rotation, Vector3 scale, Material material, bool collision)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = WeaponLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Material Tint(string path, Material source, Color color, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.CopyPropertiesFromMaterial(source);
            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static List<T> ComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        static void SetRef(Object target, string property, Object value) { SetProperty(target, property, p => p.objectReferenceValue = value); }
        static void SetInt(Object target, string property, int value) { SetProperty(target, property, p => p.intValue = value); }
        static void SetArray<T>(Object target, string property, List<T> values) where T : Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError(target.GetType().Name + " has no " + property); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetProperty(Object target, string name, System.Action<SerializedProperty> write)
        {
            if (target == null) { Debug.LogError("Missing target for " + name); return; }
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(name);
            if (property == null) { Debug.LogError(target.GetType().Name + " has no property " + name); return; }
            write(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
