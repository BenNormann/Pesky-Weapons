using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
                    MagicDoor other = magic.Twin;
                    if (magic.LinkId == 0) problems.Add("MagicDoor has link id 0 (unassigned): " + path);
                    if (other == null) problems.Add("MagicDoor has no twin (no reference, and no other door in the registry shares its link id): " + path);
                    else
                    {
                        if (other.Twin != magic) problems.Add("MagicDoor's twin does not link back: " + path);
                        if (other.LinkId != magic.LinkId) problems.Add("MagicDoor and its twin carry different link ids: " + path);
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
