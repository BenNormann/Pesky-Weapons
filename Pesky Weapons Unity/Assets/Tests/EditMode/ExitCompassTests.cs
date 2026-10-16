using System.Reflection;
using NUnit.Framework;
using Pesky.Game;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEditor;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Tests
{
    public sealed class ExitCompassTests
    {
        GameObject _root;
        RunDirector _run;
        WorldSim _sim;
        LabyrinthRoom _start, _middle, _finish, _unused;
        BoxCollider _goal;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CompassTest");
            _run = Child("Run").AddComponent<RunDirector>();
            _start = Room(0, new Vector3(0, 0, 0));
            _middle = Room(3, new Vector3(200, 0, 0));
            _finish = Room(2, new Vector3(400, 0, 0));
            _unused = Room(8, new Vector3(600, 0, 0));
            _goal = Child("CrewFinish").AddComponent<BoxCollider>();
            _goal.transform.position = _finish.transform.position + new Vector3(0, 0, -5);
            _goal.center = new Vector3(0, 2, 0);
            _goal.size = new Vector3(4, 6, 4);
            _goal.isTrigger = true;
            Set(_run, "startRoom", _start);
            Set(_run, "exitRoom", _finish);
            Set(_run, "exitVolume", _goal);
            Set(_run, "pool", new[] { _middle, _unused });
            _sim = new WorldSim(null, 1);
            _sim.Apply(new RunLayoutMsg { roomIds = new ushort[] { 3 } });
            var session = new NetSession();
            typeof(NetSession).GetProperty("Sim").SetValue(session, _sim);
            Set(_run, "_session", session);
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_root); }

        GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            return go;
        }

        LabyrinthRoom Room(int id, Vector3 position)
        {
            var go = Child("Room_" + id);
            go.transform.position = position;
            var room = go.AddComponent<LabyrinthRoom>();
            var footprint = go.AddComponent<BoxCollider>();
            footprint.isTrigger = true;
            footprint.size = new Vector3(30, 30, 30);
            Set(room, "roomId", id);
            Set(room, "footprint", footprint);
            var doorGo = Child("Exit_" + id);
            doorGo.transform.SetParent(go.transform, false);
            doorGo.transform.localPosition = new Vector3(0, 0, -12);
            var door = doorGo.AddComponent<MagicDoor>();
            Set(room, "doorways", new MagicDoor[] { null, null, door, null });
            return room;
        }

        static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        }

        [Test]
        public void StartPointsToItsPortalNotAcrossTheEditorRoomGrid()
        {
            Assert.IsTrue(ExitCompassRoute.TryRead(_run, Vector3.zero, out var reading));
            Assert.AreSame(_start.Doorway(RunDirector.Exit), reading.door);
            Assert.AreEqual(new Vector3(0, 0, -12), reading.target);
            Assert.AreEqual(_run.ExitGoal, reading.finalDestination);
            Assert.AreEqual(0, reading.step);
        }

        [Test]
        public void ASelectedRoomAlwaysPointsForwardEvenAfterBacktracking()
        {
            Assert.IsTrue(ExitCompassRoute.TryRead(_run, _middle.transform.position, out var reading));
            Assert.AreSame(_middle.Doorway(RunDirector.Exit), reading.door);
            Assert.AreEqual(1, reading.step);
            Assert.AreEqual(_run.ExitGoal, reading.finalDestination);
        }

        [Test]
        public void ExitTargetsTheActualGatherVolumeAndStopsAtArrival()
        {
            Assert.IsTrue(ExitCompassRoute.TryRead(_run, _finish.transform.position, out var outside));
            Assert.IsTrue(outside.finalRoom);
            Assert.IsFalse(outside.arrived);
            Assert.AreEqual(_goal.transform.TransformPoint(_goal.center), outside.target);
            Assert.IsTrue(ExitCompassRoute.TryRead(_run, _run.ExitGoal, out var inside));
            Assert.IsTrue(inside.arrived);
            Assert.IsNull(inside.door);
        }

        [Test]
        public void WaitingForTheLayoutNeverMakesUpAHeading()
        {
            _sim.Apply(new RunLayoutMsg { roomIds = new ushort[0] });
            Assert.IsFalse(ExitCompassRoute.TryRead(_run, Vector3.zero, out _));
        }

        [Test]
        public void UnknownAndUnselectedRoomsNeverPointToAnUnlinkedDoor()
        {
            Assert.IsFalse(ExitCompassRoute.TryRead(_run, new Vector3(999, 0, 0), out _));
            Assert.IsFalse(ExitCompassRoute.TryRead(_run, _unused.transform.position, out _));
        }

        [Test]
        public void ClosedDoorsRemainTheTargetAndAreMarkedLocked()
        {
            var door = _middle.Doorway(RunDirector.Exit);
            var gate = Child("Gate").AddComponent<Door>();
            Set(door, "gate", gate);
            Assert.IsTrue(ExitCompassRoute.TryRead(_run, _middle.transform.position, out var reading));
            Assert.IsTrue(reading.locked);
            Assert.AreSame(door, reading.door);
        }

        [Test]
        public void HeadingIgnoresHeightAndIsRelativeToCameraYaw()
        {
            Assert.IsTrue(ExitCompassBearing.TryHeading(Vector3.zero, new Vector3(10, 500, 0), 90, out var angle));
            Assert.AreEqual(0, angle, 0.01f);
            Assert.IsFalse(ExitCompassBearing.TryHeading(Vector3.zero, new Vector3(0.01f, 5, 0.01f), 0, out _));
        }

        [Test]
        public void CrossingNorthTakesTheShortArcWithoutOvershoot()
        {
            var bearing = new ExitCompassBearing();
            bearing.Step(179, 0.02f, 0.14f);
            float first = bearing.Step(-179, 0.02f, 0.14f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(179, first)), 2f);
            for (int i = 0; i < 60; i++) bearing.Step(-179, 1f / 60f, 0.14f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(-179, bearing.Angle)), 0.1f);
        }

        [Test]
        public void TeleportsResetVelocityInsteadOfSweepingThroughOldTargets()
        {
            var bearing = new ExitCompassBearing();
            bearing.Step(90, 0.02f, 0.14f);
            bearing.Step(-90, 0.02f, 0.14f);
            Assert.AreEqual(0, bearing.Step(0, 0.02f, 0.14f, true), 0.001f);
            Assert.AreEqual(0, bearing.Step(0, 0.02f, 0.14f), 0.001f);
        }

        [Test]
        public void SmoothingSettlesAtBothThirtyAndOneHundredFortyFourFps()
        {
            var slow = new ExitCompassBearing();
            var fast = new ExitCompassBearing();
            slow.Step(0, 0, 0.14f);
            fast.Step(0, 0, 0.14f);
            for (int i = 0; i < 30; i++) slow.Step(90, 1f / 30f, 0.14f);
            for (int i = 0; i < 144; i++) fast.Step(90, 1f / 144f, 0.14f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(slow.Angle, fast.Angle)), 0.1f);
            Assert.AreEqual(90, slow.Angle, 0.1f);
        }

        [Test]
        public void PrefabLivesOutsideResourcesAndUsesTheSharedHudDocument()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ExitCompass.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<RunExitCompass>());
            Assert.IsNull(prefab.GetComponent<UIDocument>());
            Assert.IsFalse(AssetDatabase.IsValidFolder("Assets/Resources"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/ExitCompass.uss"));
            foreach (var method in typeof(RunExitCompass).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                Assert.IsFalse(method.IsDefined(typeof(RuntimeInitializeOnLoadMethodAttribute), false));
        }

        [Test]
        public void SharedHudOwnsTheCompassLayoutAndStyle()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Hud.uxml");
            var root = asset.CloneTree();
            var compass = root.Q<VisualElement>("exit-compass");
            var weapon = root.Q<VisualElement>("weapon-panel");
            Assert.IsNotNull(compass);
            Assert.AreSame(weapon.parent, compass.parent);
            Assert.AreEqual("bottom-row", compass.parent.name);
            Assert.Less(compass.parent.IndexOf(compass), weapon.parent.IndexOf(weapon));
            Assert.IsTrue(compass.ClassListContains("hud-panel"));
            Assert.IsTrue(weapon.ClassListContains("hud-panel"));
            Assert.IsNotNull(compass.Q<VisualElement>("exit-compass-dial"));
            Assert.AreEqual(StyleKeyword.Null, weapon.style.marginLeft.keyword);
        }

        [Test]
        public void RunSceneContainsOneWiredCompassPrefabUnderUi()
        {
            string yaml = System.IO.File.ReadAllText("Assets/Scenes/Run.unity");
            string guid = AssetDatabase.AssetPathToGUID("Assets/Prefabs/UI/ExitCompass.prefab");
            string instance = null;
            int count = 0;
            var blocks = Regex.Matches(yaml, @"(?m)^--- !u![\s\S]*?(?=^--- !u!|\z)");
            foreach (Match block in blocks)
            {
                if (!block.Value.Contains("PrefabInstance:") || !block.Value.Contains("m_SourcePrefab: {fileID: 100100000, guid: " + guid)) continue;
                instance = block.Value;
                count++;
            }
            Assert.AreEqual(1, count);
            string parentId = Regex.Match(instance, @"m_TransformParent: \{fileID: (\d+)\}").Groups[1].Value;
            string parent = SceneBlock(blocks, parentId);
            string parentGoId = Regex.Match(parent, @"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;
            StringAssert.Contains("m_Name: _UI", SceneBlock(blocks, parentGoId));
            foreach (string field in new[] { "document", "run", "spawner", "orbitCamera", "sessionRunner" })
            {
                var reference = Regex.Match(instance, @"propertyPath: " + field + @"\r?\n\s+value:.*\r?\n\s+objectReference: \{fileID: (\d+)\}");
                Assert.IsTrue(reference.Success, field);
                string id = reference.Groups[1].Value;
                Assert.AreNotEqual("0", id, field);
                Assert.IsNotEmpty(SceneBlock(blocks, id), field);
            }
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/HUD.prefab").GetComponent<UIDocument>();
            Assert.AreEqual("Assets/UI/Hud.uxml", AssetDatabase.GetAssetPath(hud.visualTreeAsset));
            Assert.Less(hud.sortingOrder, 1f);
        }

        static string SceneBlock(MatchCollection blocks, string id)
        {
            foreach (Match block in blocks)
                if (Regex.IsMatch(block.Value, @"\A--- !u!\d+ &" + id + @"(?:\s|$)")) return block.Value;
            return "";
        }
    }
}
