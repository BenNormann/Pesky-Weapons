using NUnit.Framework;
using Pesky.Data;
using Pesky.Game;
using UnityEditor;
using UnityEngine;

namespace Pesky.Tests
{
    public sealed class ResonanceForgeTests
    {
        const string Folder = "Assets/Prefabs/Rooms/Labyrinth/";
        const string PrefabPath = Folder + "LabyrinthRoom_ResonanceForge.prefab";

        [Test]
        public void PrefabIsWiredAsFourPlayerThreeClassPuzzle()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "The authored Resonance Forge prefab must exist.");

            PuzzleRoomProfile profile = prefab.GetComponent<PuzzleRoomProfile>();
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.MinimumPlayers, Is.EqualTo(3));
            Assert.That(profile.OptimalPlayers, Is.EqualTo(4));
            Assert.That(profile.MaximumPlayers, Is.EqualTo(8));

            ScalesLock scales = prefab.GetComponentInChildren<ScalesLock>(true);
            Assert.That(scales, Is.Not.Null);
            Assert.That(scales.PanCount, Is.EqualTo(3));
            Assert.That(scales.Want(0), Is.EqualTo(MassClass.Light));
            Assert.That(scales.Want(1), Is.EqualTo(MassClass.Medium));
            Assert.That(scales.Want(2), Is.EqualTo(MassClass.Heavy));
            Assert.That(scales.Fits(1f, MassClass.Light), Is.True);
            Assert.That(scales.Fits(3f, MassClass.Medium), Is.True);
            Assert.That(scales.Fits(14f, MassClass.Heavy), Is.True);

            Transform southDoor = prefab.transform.Find("Doorways/Doorway_South");
            Assert.That(southDoor, Is.Not.Null);
            MagicDoor portal = southDoor.GetComponent<MagicDoor>();
            Assert.That(portal, Is.Not.Null);
            Assert.That(portal.Gate, Is.Not.Null);
            Assert.That(portal.Gate.Condition, Is.Not.Null);
            Assert.That(portal.Gate.Condition.ConditionMode, Is.EqualTo(DoorCondition.Mode.ScalesSatisfied));
            Assert.That(portal.Gate.Condition.Scales, Is.SameAs(scales));

            Assert.That(prefab.GetComponentInChildren<ImpactLever>(true), Is.Null,
                "The generic lever must not remain as an easy bypass.");
        }

        [TestCase("LabyrinthRoom_ClockworkCanteen.prefab", typeof(ClockMover), 3)]
        [TestCase("LabyrinthRoom_MagnetMayhem.prefab", typeof(MagnetZone), 3)]
        [TestCase("LabyrinthRoom_CounterweightComedy.prefab", typeof(CounterweightPair), 1)]
        public void PlatformRoomHasProfileMechanicAndLatchingExit(string file, System.Type mechanic, int minimumCount)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + file);
            Assert.That(prefab, Is.Not.Null);
            PuzzleRoomProfile profile = prefab.GetComponent<PuzzleRoomProfile>();
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.MinimumPlayers, Is.EqualTo(3));
            Assert.That(profile.OptimalPlayers, Is.EqualTo(4));
            Assert.That(profile.MaximumPlayers, Is.EqualTo(8));
            Assert.That(prefab.GetComponentsInChildren(mechanic, true).Length, Is.GreaterThanOrEqualTo(minimumCount));

            MagicDoor portal = prefab.transform.Find("Doorways/Doorway_South").GetComponent<MagicDoor>();
            Assert.That(portal.Gate, Is.Not.Null);
            Assert.That(portal.Gate.Condition.ConditionMode, Is.EqualTo(DoorCondition.Mode.LeverOn));
            Assert.That(portal.Gate.Condition.Lever, Is.Not.Null);
            Assert.That(portal.Gate.Condition.Lever.IsLatching, Is.True);
        }

        [Test]
        public void RunAlwaysSelectsExactlyTheFourAuthoredRooms()
        {
            RunDef run = AssetDatabase.LoadAssetAtPath<RunDef>("Assets/Data/Run.asset");
            Assert.That(run, Is.Not.Null);
            Assert.That(run.roomsPerRun, Is.EqualTo(4));
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6 }, run.guaranteedRoomIds);
        }

        [Test]
        public void ClockworkTraysHoldGroundedWeaponsWithoutBlockingLaunches()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LabyrinthRoom_ClockworkCanteen.prefab");
            Assert.That(prefab, Is.Not.Null);

            ClockMover[] trays = prefab.GetComponentsInChildren<ClockMover>(true);
            Assert.That(trays, Has.Length.EqualTo(3));
            foreach (ClockMover tray in trays)
            {
                Assert.That(tray.RiderBrake, Is.GreaterThanOrEqualTo(14f), tray.name + " should stop grounded drift.");
                Assert.That(tray.BrakeLaunchGrace, Is.GreaterThanOrEqualTo(0.5f), tray.name + " should preserve launch momentum.");
                Assert.That(tray.RiderBoxSize.y, Is.GreaterThanOrEqualTo(2f), tray.name + " should keep tall weapons inside its rider zone.");
            }
        }

        [Test]
        public void BananaIsReadableAndLivesOnRackSlotSeven()
        {
            GameObject bananaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Banana.prefab");
            Assert.That(bananaPrefab, Is.Not.Null);
            Assert.That(bananaPrefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThanOrEqualTo(10),
                "The Banana should have a curved peel, highlight, stem and ripe details.");
            Assert.That(bananaPrefab.GetComponentsInChildren<Collider>(true).Length, Is.GreaterThanOrEqualTo(5),
                "The curved Banana needs a compound collider that follows its silhouette.");

            GameObject start = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LabyrinthRoom_Start.prefab");
            WeaponBody banana = null;
            foreach (WeaponBody weapon in start.GetComponentsInChildren<WeaponBody>(true))
                if (weapon.Def != null && weapon.Def.displayName == "Banana") banana = weapon;

            Assert.That(banana, Is.Not.Null, "The starting weapon room must include the Banana.");
            Assert.That(banana.HomeSlot, Is.Not.Null);
            Assert.That(banana.HomeSlot.SceneId, Is.EqualTo(7));
            Assert.That(banana.name, Is.EqualTo("Banana_7"));
        }
    }
}
