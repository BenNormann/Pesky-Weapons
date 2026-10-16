using System.Collections.Generic;
using Pesky.Data;
using Pesky.Game;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pesky.Editor
{
    /// <summary>
    /// Builds the five v3 puzzle rooms (docs/level-design/rooms-v3.md) from primitives and the kit, saves them
    /// as prefabs and installs them as rooms 3-7 of the run. Numbers live at the top of each room method:
    /// change a number, rerun "Pesky/Rooms/Build Five Rooms v3". Local axes: +Z = north = entry, -Z = exit.
    /// </summary>
    public static class RoomsV3Builder
    {
        const string TwoDoorPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_TwoDoor.prefab";
        const string RunScene = "Assets/Scenes/Run.unity";
        const string RunDefAsset = "Assets/Data/Run.asset";
        const string K = "Assets/Prefabs/Kit/";
        const string PlatePrefab = K + "Plates_And_Pans/Plate.prefab";
        const string LeverPrefab = K + "Movers/ImpactLever.prefab";
        const string RopePrefab = K + "Movers/Rope.prefab";
        const string RampPrefab = K + "Movers/DropRamp.prefab";
        const string WoodPrefab = K + "Breakables/WoodBlock.prefab";
        const string DoorPrefab = K + "Doors/Door.prefab";
        const string PorterGatePrefab = K + "Doors/PorterGate.prefab";
        const string StandPrefab = K + "Stations/HammerStand.prefab";
        const string RoutePrefab = K + "Markers/PatrolRoute.prefab";
        const string RoomVolumePrefab = K + "Markers/RoomVolume.prefab";
        const string GoblinPrefab = "Assets/Prefabs/Enemies/Goblin.prefab";
        const string PorterPrefab = "Assets/Prefabs/Enemies/GoblinPorter.prefab";
        const string SleeperPrefab = "Assets/Prefabs/Enemies/GoblinSleeper.prefab";
        const string BossPrefab = "Assets/Prefabs/Enemies/GoblinBoss.prefab";
        const string MFloor = "Assets/Materials/M_Floor.mat";
        const string MWall = "Assets/Materials/M_Wall.mat";
        const string MLedge = "Assets/Materials/M_Ledge.mat";
        const string MWood = "Assets/Materials/M_Wood.mat";
        const string MMetal = "Assets/Materials/M_Metal.mat";
        const string MGlow = "Assets/Materials/M_Glow.mat";
        const string MHazard = "Assets/Materials/M_Hazard.mat";
        const string MRope = "Assets/Materials/M_Rope.mat";
        const string MMagicFlow = "Assets/Materials/M_MagicFlow.mat";
        const string MAnvil = "Assets/Materials/M_Anvil.mat";
        const string CircuitBridgePrefab = K + "Circuit/CircuitBridge.prefab";
        const string MagicBridgePrefab = K + "Circuit/MagicBridge.prefab";
        const string MagnetPrefab = K + "Hazards/MagnetZone.prefab";
        const string DecoCrate = K + "Deco/Deco_Crate.prefab";
        const string DecoBarrel = K + "Deco/Deco_Barrel.prefab";
        const string DecoShelf = K + "Deco/Deco_Shelf.prefab";
        const string DecoPillar = K + "Deco/Deco_Pillar.prefab";
        const string DecoRubble = K + "Deco/Deco_Rubble.prefab";
        const string DecoCart = K + "Deco/Deco_Cart.prefab";
        const int LayerWorld = 8;
        const int FirstRoomId = 3;
        /// <summary>The renderers of a MagicDoor prefab that carry the magic: the portal plane and the wire that feeds it
        /// (two strips up the jambs, the bar over the opening, the supply up the wall and its turn into the wall; round 29).</summary>
        static readonly string[] DoorMagicPieces = { "Plane", "Feed_L", "Feed_R", "Feed_Top", "Supply", "Supply_In" };
        /// <summary>Height up the wall (above the doorway's floor) where the prefab's Supply strip tops out and turns into the
        /// wall: a room's own line into a door ends there.</summary>
        const float SupplyY = 5.6f;

        static readonly string[] Names = { "Gatehouse", "Well Room", "Porter Room", "Circuit Room", "Bat Room" };
        static string PrefabPath(int i) { return "Assets/Prefabs/Rooms/Labyrinth/RoomV3_" + SafeName(Names[i]) + ".prefab"; }

        /// <summary>
        /// The builder REPLACES the five room prefabs and their instances in Run.unity, so any hand edit made to
        /// them is lost. Before it touches anything it copies the scene and the prefabs to
        /// &lt;repo&gt;/Backups/rooms-v3/&lt;timestamp&gt;/ (git-ignored), and it refuses to run while the open scene has
        /// unsaved changes (OpenScene would discard them). Returns false when nothing may be built.
        /// </summary>
        static bool BackupBeforeBuild()
        {
            Scene open = SceneManager.GetActiveScene();
            if (open.isDirty)
            {
                Debug.LogError("[RoomsV3] the open scene '" + open.name + "' has UNSAVED changes. Save or discard them first; nothing was built.");
                return false;
            }
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string repoRoot = System.IO.Directory.GetParent(projectRoot).FullName;
            string folder = System.IO.Path.Combine(repoRoot, "Backups", "rooms-v3", System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            System.IO.Directory.CreateDirectory(folder);
            int copied = 0;
            string[] sources = { RunScene, PrefabPath(0), PrefabPath(1), PrefabPath(2), PrefabPath(3), PrefabPath(4) };
            foreach (string asset in sources)
            {
                string full = System.IO.Path.Combine(projectRoot, asset.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(full)) continue;
                System.IO.File.Copy(full, System.IO.Path.Combine(folder, System.IO.Path.GetFileName(full)), true);
                copied++;
            }
            Debug.Log("[RoomsV3] backed up " + copied + " file(s) to " + folder + " before rebuilding. Hand edits to the five rooms are REPLACED by this build.");
            return true;
        }

        [MenuItem("Pesky/Rooms/Build Five Rooms v3")]
        public static void BuildAll()
        {
            if (!BackupBeforeBuild()) return;
            CreateAllPrefabs();
            ConfigureData();
            Scene scene = EditorSceneManager.OpenScene(RunScene, OpenSceneMode.Single);
            InstallIntoOpenRunScene(scene);
            Debug.Log("[RoomsV3] built the five v3 rooms and installed them as rooms 3-7.");
        }

        /// <summary>Only the two v4 rooms (the Circuit Room and the Chasm, indices 3 and 4): the other three prefabs and their
        /// instances, hand edits included, are not touched. The scene still gets its registries, ids and NavMesh redone.</summary>
        [MenuItem("Pesky/Rooms/Rebuild Circuit Room + Chasm (v4)")]
        public static void RebuildV4()
        {
            if (!BackupBeforeBuild()) return;
            CircuitRoom();
            BatRoom();
            AssetDatabase.SaveAssets();
            Scene scene = EditorSceneManager.OpenScene(RunScene, OpenSceneMode.Single);
            InstallIntoOpenRunScene(scene, new[] { 3, 4 });
            Debug.Log("[RoomsV3] rebuilt the Circuit Room and the Chasm (v4) and reinstalled them as rooms " + (FirstRoomId + 3) + " and " + (FirstRoomId + 4) + ".");
        }

        [MenuItem("Pesky/Rooms/Rebuild Porter Room")]
        public static void RebuildPorter()
        {
            if (!BackupBeforeBuild()) return;
            PorterRoom();
            AssetDatabase.SaveAssets();
            Scene scene = EditorSceneManager.OpenScene(RunScene, OpenSceneMode.Single);
            InstallIntoOpenRunScene(scene, new[] { 2 });
            Debug.Log("[RoomsV3] rebuilt the Porter Room and reinstalled it as room " + (FirstRoomId + 2) + ".");
        }

        static bool Wanted(int[] only, int i) { return only == null || System.Array.IndexOf(only, i) >= 0; }

        public static void CreateAllPrefabs()
        {
            Gatehouse();
            WellRoom();
            PorterRoom();
            CircuitRoom();
            BatRoom();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Room 1: THE GATEHOUSE
        static void Gatehouse()
        {
            // docs/level-design/rooms-v5-gatehouse.md. The inner gate of the labyrinth: a stone gate wall 8 m thick across the
            // hall with the only passage through it shut by a PORTCULLIS at the passage's FRONT end, a winch chamber inside the
            // wall above the passage that opens onto the BACK yard (owner, round 29b: the pin lever is only reachable by someone
            // who has passed under the raised gate, which is the interlock), the petitioners' yard in front and the guards'
            // yard behind. A SWITCH IS A WIRE: the hold plate in the front yard runs its conduit along the floor and into the
            // passage; the pin lever in the chamber runs its conduit down through the wall into the passage; they meet in
            // the JUNCTION on the passage's west wall, in plain view from both yards, and the junction's line runs along the
            // passage ceiling to the gate and (inside the wall) up to the winch drum. The gate is up while that line is live:
            // plate held OR lever on. Behind the gate the weigh plate's line runs up the south wall into the exit door's own
            // wire. Two goblins behind, a sleeper in front. Decorations only where they block sight or movement (owner):
            // crates, barrels, a cart, a shelf.
            const float W = 24f, L = 60f, H = 12f;
            const float wallS = 0f, wallN = 8f;              // the gate wall spans z 0..8; its north face is the front
            const float passW = 6f, passH = 7f;              // the passage through it
            const float chamberY = 8f;                        // the winch chamber's floor; its ceiling is the roof
            const float off = 0.3f, onFloor = 0.13f;
            float wallMid = (wallS + wallN) / 2f, wallD = wallN - wallS;
            float faceN = wallN + off;                        // a rail on the gate wall's north (front) face
            float sz = -L / 2f + off;                         // a rail on the south wall
            Transform geo, play;
            GameObject room = Shell("RoomV3_Gatehouse", W, L, H, 0f, out geo, out play);

            // THE GATE WALL: two solid blocks either side of the passage, the slab over it (the chamber's floor), the chamber's
            // solid north wall (the gate rises into it), and its south wall with the WINDOW onto the back yard (x -2.5..2.5,
            // from the chamber floor to 11 m).
            float sideW = W / 2f - passW / 2f;
            Block(geo, "GateWall_West", new Vector3(-passW / 2f - sideW / 2f, H / 2f, wallMid), new Vector3(sideW, H, wallD), MWall);
            Block(geo, "GateWall_East", new Vector3(passW / 2f + sideW / 2f, H / 2f, wallMid), new Vector3(sideW, H, wallD), MWall);
            Block(geo, "GateWall_Slab", new Vector3(0f, (passH + chamberY) / 2f, wallMid), new Vector3(passW, chamberY - passH, wallD), MWall);
            Block(geo, "Chamber_North", new Vector3(0f, (chamberY + H) / 2f, wallN - 1f), new Vector3(passW, H - chamberY, 2f), MWall);
            Block(geo, "Chamber_PierW", new Vector3(-passW / 2f + 0.25f, (chamberY + H) / 2f, wallS + 0.25f), new Vector3(0.5f, H - chamberY, 0.5f), MWall);
            Block(geo, "Chamber_PierE", new Vector3(passW / 2f - 0.25f, (chamberY + H) / 2f, wallS + 0.25f), new Vector3(0.5f, H - chamberY, 0.5f), MWall);
            Block(geo, "Chamber_Lintel", new Vector3(0f, H - 0.5f, wallS + 0.25f), new Vector3(passW, 1f, 0.5f), MWall);

            // Up to the chamber, from the BACK yard: a goblin stair along the wall's south face east of the passage, flush to the
            // face and to the east wall, its top step beside the window (any weapon hops it); and a TIMBER POST west of the
            // passage, 1.5 m clear of the wall, that a blade sticks in and climbs (the mechanic the Well Room needs next) to
            // launch into the window.
            int steps = Mathf.RoundToInt(chamberY / 0.5f);
            float tread = (W / 2f - passW / 2f) / steps;
            Stair(geo, "ChamberStair", new Vector3(W / 2f, 0f, wallS - 1f), Vector3.left, steps, 0.5f, tread, 2f);
            Wood(play, "ClimbPost", new Vector3(-passW / 2f - 1.5f, (chamberY + 0.5f) / 2f, wallS - 1.5f - 0.45f), new Vector3(0.9f, chamberY + 0.5f, 0.9f));

            // THE WINCH: the drum across the chamber (it lights with the gate's line) and the counterweight beside it, a stone
            // block on a Door that DROPS as the gate rises (same condition), so the plate visibly does work. The weight hangs
            // FLUSH to the chamber's east wall (no slot beside it) and stops 0.5 m above the floor when down.
            GameObject drum = Cylinder(geo, "WinchDrum", new Vector3(0f, chamberY + 2.5f, wallS + 4f), new Vector3(1.2f, 1.4f, 1.2f), MMagicFlow, Quaternion.Euler(0f, 0f, 90f));
            Door weight = DoorAt(play, DoorPrefab, "Counterweight", new Vector3(passW / 2f - 0.5f, chamberY + 3.2f, wallS + 2.2f), Quaternion.identity);
            Transform weightPanel = weight.transform.Find("Panel");
            if (weightPanel != null) { weightPanel.localPosition = Vector3.zero; weightPanel.localScale = Vector3.one; Paint(weightPanel.gameObject, MAnvil); }
            SetVector3(weight, "openOffset", new Vector3(0f, -2.2f, 0f));
            SetBool(weight, "closesAgain", true);

            // THE PORTCULLIS at the passage's FRONT end: the Door prefab's panel grown to the passage (6 x 7 x 1), bars on both
            // faces, the slab itself the collider and a carving NavMesh obstacle (shut, no goblin paths through; open, it rises
            // clear). It slides UP into the solid part of the wall.
            // Inset 0.2 m behind the wall's face and 0.1 m INTO the walls, the floor and the ceiling, so none of its faces, shut
            // or risen, ever lies in a face of the wall (the raised gate flickered against the wall's face: round 29c).
            Door gate = DoorAt(play, DoorPrefab, "Portcullis", new Vector3(0f, 0f, wallN - 0.7f), Quaternion.identity);
            Transform gatePanel = gate.transform.Find("Panel");
            if (gatePanel != null)
            {
                gatePanel.localPosition = new Vector3(0f, passH / 2f, 0f);
                gatePanel.localScale = new Vector3(passW + 0.2f, passH + 0.2f, 1f);
                Lattice(gatePanel, 0.35f);
                Lattice(gatePanel, -0.35f);
                NavMeshModifier mod = gatePanel.gameObject.AddComponent<NavMeshModifier>();
                mod.ignoreFromBuild = true;
                UnityEngine.AI.NavMeshObstacle obstacle = gatePanel.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.center = Vector3.zero;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
            }
            SetVector3(gate, "openOffset", new Vector3(0f, passH + 0.2f, 0f));
            SetBool(gate, "closesAgain", true);

            // THE SWITCHES: the hold plate in the front yard (8 kg: a Mace or a Hammer, held, not latched) and the pin lever on
            // the chamber's west wall (latching). Gate and counterweight are up while the plate is held OR the lever is on.
            // The lever's handle is its +Z: on the chamber's west wall it faces +X, out into the room (owner, round 29c: it faced
            // the wall), and sits south of the drum so the handle's swing is clear of it.
            const float leverZ = 2.5f;
            PressurePlate hold = Plate(play, "Plate_Hold", new Vector3(-8f, 0f, 20f), 8f, false, false);
            ImpactLever pin = Lever(play, "Lever_Pin", new Vector3(-passW / 2f + 0.6f, chamberY + 1.5f, wallS + leverZ), Quaternion.Euler(0f, 90f, 0f));
            HeldOrPinned(gate, hold, pin);
            HeldOrPinned(weight, hold, pin);
            SetRef(hold, "spentWhenLeverOn", pin);   // once the winch is pinned the plate is spent: pressed for good, glow off (owner, round 29b)

            // THE WIRING. The junction sits on the passage's west wall, halfway through, at hand height. The hold plate's line:
            // along the front yard's floor to the gate wall, along the foot of its north face to the passage, along the foot of
            // the passage's west wall to the junction and up into it.
            float px = -passW / 2f + off;                     // a rail on the passage's / the chamber's west wall
            const float jy = 2.5f;
            Vector3 junctionAt = new Vector3(px, jy, wallS + leverZ);
            List<Renderer> holdRails = new List<Renderer>();
            holdRails.Add(Conduit(geo, "Hold_0", new Vector3(-8f, onFloor, 18.8f), new Vector3(-8f, onFloor, faceN)));
            holdRails.Add(Conduit(geo, "Hold_1", new Vector3(-8f, onFloor, faceN), new Vector3(px, onFloor, faceN)));
            holdRails.Add(Conduit(geo, "Hold_2", new Vector3(px, onFloor, faceN), new Vector3(px, onFloor, wallS + leverZ)));
            holdRails.Add(Conduit(geo, "Hold_3", new Vector3(px, onFloor, wallS + leverZ), new Vector3(px, jy - 0.3f, wallS + leverZ)));
            PowerLine holdLine = Line(play, "Line_Hold", hold, holdRails);
            // The pin lever's line: down the chamber's west wall to its floor, into that wall (buried), down inside it to just
            // under the passage ceiling, out into the passage's top corner and down its west wall into the junction.
            float bx = -passW / 2f - 0.6f;
            List<Renderer> pinRails = new List<Renderer>();
            pinRails.Add(Conduit(geo, "Pin_0", new Vector3(px, chamberY + 1.2f, wallS + leverZ), new Vector3(px, chamberY + onFloor, wallS + leverZ)));
            pinRails.Add(Conduit(geo, "Pin_1", new Vector3(px, chamberY + onFloor, wallS + leverZ), new Vector3(bx, chamberY + onFloor, wallS + leverZ)));
            pinRails.Add(Conduit(geo, "Pin_2", new Vector3(bx, chamberY + onFloor, wallS + leverZ), new Vector3(bx, passH - off, wallS + leverZ)));
            pinRails.Add(Conduit(geo, "Pin_3", new Vector3(bx, passH - off, wallS + leverZ), new Vector3(px, passH - off, wallS + leverZ)));
            pinRails.Add(Conduit(geo, "Pin_4", new Vector3(px, passH - off, wallS + leverZ), new Vector3(px, jy + 0.3f, wallS + leverZ)));
            PowerLine pinLine = Line(play, "Line_Pin", pin, pinRails);
            // The junction, and its line: along the passage's west wall at the ceiling toward the gate, across the ceiling to
            // the gate's line, up to the gate (just clear of the rising bars), then (inside the wall) up to the chamber and
            // into the drum.
            PowerJunction junction = Junction(play, geo, "Junction", junctionAt, new List<PowerLine> { holdLine, pinLine });
            float gx = -1.5f, gy = passH - off, gateZ = wallN - 1.2f - 0.5f;   // the gate line stops short of the bars
            List<Renderer> gateRails = new List<Renderer>();
            gateRails.Add(Conduit(geo, "Gate_0", new Vector3(px, gy, wallS + leverZ + 0.3f), new Vector3(px, gy, gateZ)));
            gateRails.Add(Conduit(geo, "Gate_1", new Vector3(px, gy, gateZ), new Vector3(gx, gy, gateZ)));
            gateRails.Add(Conduit(geo, "Gate_2", new Vector3(gx, gy, gateZ), new Vector3(gx, chamberY + 2.5f, gateZ)));
            gateRails.Add(Conduit(geo, "Gate_3", new Vector3(gx, chamberY + 2.5f, gateZ), new Vector3(gx, chamberY + 2.5f, wallS + 4.6f)));
            gateRails.Add(drum.GetComponent<Renderer>());
            Line(play, "Line_Gate", junction, gateRails);

            // THE FRONT YARD: crates and a cart that hide the hold plate from the entry, barrels against the gate wall's foot, and
            // a sleeping goblin by the east wall that a loud landing wakes.
            Deco(geo, DecoCrate, "Crate_F1", new Vector3(-11.5f, 0f, 24f), 0f);
            Deco(geo, DecoCrate, "Crate_F2", new Vector3(-10.5f, 0f, 24f), 0f);
            Deco(geo, DecoCrate, "Crate_F3", new Vector3(-11.5f, 1f, 24f), 0f);
            Deco(geo, DecoCart, "Cart", new Vector3(-5f, 0f, 24f), 30f);
            Deco(geo, DecoBarrel, "Barrel_F1", new Vector3(6f, 0f, 12.5f), 0f);
            Deco(geo, DecoBarrel, "Barrel_F2", new Vector3(6.8f, 0f, 12.5f), 0f);
            GameObject frontGo = Kit(RoomVolumePrefab, play, "FrontYardVolume", new Vector3(0f, H / 2f, (wallN + L / 2f) / 2f), Quaternion.identity);
            BoxCollider frontBox = frontGo.GetComponent<BoxCollider>();
            if (frontBox != null) { frontBox.center = Vector3.zero; frontBox.size = new Vector3(W, H, L / 2f - wallN); }
            RoomVolume front = frontGo.GetComponent<RoomVolume>();
            SetString(front, "roomName", "Front Yard");
            GameObject sleeper = Kit(SleeperPrefab, play, "Goblin_Sleeper", new Vector3(9.5f, 0f, 20f), Quaternion.Euler(0f, -90f, 0f));
            SetArray(front, "enemies", new List<GoblinBrain> { sleeper.GetComponent<GoblinBrain>() });
            SetRef(sleeper.GetComponent<GoblinBrain>(), "room", front);

            // THE BACK YARD: the guards' side. A watch platform along the east wall with its stair at the far (south) end, flush
            // to the south wall and the east wall; a shelf flush to the west wall; crate stacks and barrels that hide the weigh
            // plate in the south-west corner from the passage. The watcher on the platform, the patroller on the floor.
            const float platY = 4f;
            Stair(geo, "WatchStair", new Vector3(10f, 0f, -L / 2f), Vector3.forward, 8, 0.5f, 0.6f, 4f);
            Block(geo, "WatchPlatform", new Vector3(10f, platY - 0.25f, -L / 2f + 4.8f + 10f), new Vector3(4f, 0.5f, 20f), MLedge);
            Deco(geo, DecoShelf, "Shelf_W", new Vector3(-W / 2f + 0.3f, 0f, -14f), -90f);   // 0.3 = half its depth: flush to the wall
            Deco(geo, DecoCrate, "Crate_B1", new Vector3(-5.5f, 0f, -24.5f), 0f);
            Deco(geo, DecoCrate, "Crate_B2", new Vector3(-4.5f, 0f, -24.5f), 0f);
            Deco(geo, DecoCrate, "Crate_B3", new Vector3(-5.5f, 1f, -24.5f), 0f);
            Deco(geo, DecoBarrel, "Barrel_B1", new Vector3(-8f, 0f, -20f), 0f);
            Deco(geo, DecoBarrel, "Barrel_B2", new Vector3(-8.8f, 0f, -20f), 0f);
            GameObject backGo = Kit(RoomVolumePrefab, play, "BackYardVolume", new Vector3(0f, H / 2f, (wallS - L / 2f) / 2f), Quaternion.identity);
            BoxCollider backBox = backGo.GetComponent<BoxCollider>();
            if (backBox != null) { backBox.center = Vector3.zero; backBox.size = new Vector3(W, H, L / 2f + wallS); }
            RoomVolume back = backGo.GetComponent<RoomVolume>();
            SetString(back, "roomName", "Back Yard");
            GameObject watcher = Kit(GoblinPrefab, play, "Goblin_Watch", new Vector3(10f, platY, -14f), Quaternion.identity);
            GameObject patroller = Kit(GoblinPrefab, play, "Goblin_Patrol", new Vector3(0f, 0f, -12f), Quaternion.identity);
            GameObject routeGo = Kit(RoutePrefab, play, "PatrolRoute_BackYard", Vector3.zero, Quaternion.identity);
            RoutePoints(routeGo.transform, new Vector3(-5f, 0f, -8f), new Vector3(5f, 0f, -8f), new Vector3(5f, 0f, -22f), new Vector3(-3f, 0f, -22f));
            SetRef(patroller.GetComponent<GoblinBrain>(), "patrol", routeGo.GetComponent<PatrolRoute>());
            SetArray(back, "enemies", new List<GoblinBrain> { watcher.GetComponent<GoblinBrain>(), patroller.GetComponent<GoblinBrain>() });
            SetRef(watcher.GetComponent<GoblinBrain>(), "room", back);
            SetRef(patroller.GetComponent<GoblinBrain>(), "room", back);

            // THE EXIT: the weigh plate (10 kg, latching: a Hammer, or a Mace with a Sword or an Orb on it) and its line along
            // the floor, up the south wall and into the exit door's own supply wire; the door is a dead portal until then.
            PressurePlate weigh = Plate(play, "Plate_Weigh", new Vector3(-9f, 0f, -26f), 10f, true, false);
            List<Renderer> exitRails = new List<Renderer>();
            exitRails.Add(Conduit(geo, "Exit_0", new Vector3(-9f, onFloor, -27.2f), new Vector3(-9f, onFloor, sz)));
            exitRails.Add(Conduit(geo, "Exit_1", new Vector3(-9f, onFloor, sz), new Vector3(-9f, SupplyY, sz)));
            exitRails.Add(Conduit(geo, "Exit_2", new Vector3(-9f, SupplyY, sz), new Vector3(0f, SupplyY, sz)));
            GateSouth(room, DoorCondition.Mode.PlateLatched, weigh, null, null, null, 0f);
            exitRails.AddRange(DoorMagic(room));
            Line(play, "Line_Exit", weigh, exitRails);

            Profile(room, "gatehouse", Names[0], "A heavy holds the plate to raise the portcullis, another passes under it and pins the winch in the chamber behind, everyone weighs in behind the gate.", 1);
            SaveRoom(room, PrefabPath(0));
        }

        // ------------------------------------------------------------------ Room 2: WELL ROOM
// ------------------------------------------------------------------ Room 2: WELL ROOM
// ------------------------------------------------------------------ Room 2: WELL ROOM
        static void WellRoom()
        {
            // Octagon flat to flat W. The platform hangs level at balconyY; cutting the rope swings it about its
            // south hinge until its north end is on the floor, so the swing angle follows from the two numbers.
            const float W = 40f, L = 40f, H = 14f, balconyY = 7f;
            const float platform = 19.5f;
            float swing = Mathf.Asin(balconyY / platform) * Mathf.Rad2Deg;   // 21.0 deg
            Transform geo, play;
            GameObject room = Shell("RoomV3_WellRoom", W, L, H, balconyY, out geo, out play);
            Octagonise(geo, W, H);

            // Balconies: south (exit) 6 m deep, north (entry side, the rope) 8 m deep. The corner walls are their ends.
            const float northDepth = 12f, southDepth = 6f;   // the rope ledge by the entry is deep: room to land, aim and cut
            Block(geo, "Balcony_South", new Vector3(0f, balconyY - 0.25f, -L / 2f + southDepth / 2f), new Vector3(W, 0.5f, southDepth), MLedge);
            Block(geo, "Balcony_North", new Vector3(0f, balconyY - 0.25f, L / 2f - northDepth / 2f), new Vector3(W, 0.5f, northDepth), MLedge);

            // The platform: a DropRamp hinged at its south edge, deck along +Z (north), level at rest.
            float hingeZ = -L / 2f + southDepth + 0.8f;
            GameObject rampGo = Kit(RampPrefab, play, "Platform", new Vector3(0f, balconyY, hingeZ), Quaternion.identity);
            DropRamp ramp = rampGo.GetComponent<DropRamp>();
            SetInt(ramp, "mode", (int)DropRamp.Mode.Swing);
            SetFloat(ramp, "swingDegrees", swing);
            SetFloat(ramp, "fallSeconds", 1.2f);
            Transform deck = rampGo.transform.Find("Deck");
            if (deck != null) { deck.localPosition = new Vector3(0f, -0.2f, platform / 2f); deck.localScale = new Vector3(platform, 0.4f, platform); Paint(deck.gameObject, MWood); }
            Block(rampGo.transform, "Yoke_North", new Vector3(0f, 0.25f, platform - 0.25f), new Vector3(platform, 0.5f, 0.5f), MWood, Quaternion.identity, false);
            Block(rampGo.transform, "Yoke_South", new Vector3(0f, 0.25f, 0.25f), new Vector3(platform, 0.5f, 0.5f), MWood, Quaternion.identity, false);
            // CARGO (round 29b): the lift is the goblins' freight elevator and it is loaded. Crates and barrels stand along the
            // deck's two side edges and ride with it (children of the ramp, NOT static); the middle lane stays clear for the
            // heavy's ride up the fallen ramp.
            // Along the deck (local z, hinge at 0) the guide frames' uprights pass the deck's side edges at z 7.5..8.4 and
            // 11.1..12.0 and the yokes lip its ends at 0..0.5 and 19..19.5: every piece keeps 1.5 m from those (no pocket
            // between a crate and an upright at deck height).
            float side = platform / 2f - 1.25f;
            DecoMoving(rampGo.transform, DecoCrate, "Cargo_Crate_1", new Vector3(side, 0f, 4f), 0f);
            DecoMoving(rampGo.transform, DecoCrate, "Cargo_Crate_2", new Vector3(side, 0f, 5f), 0f);
            DecoMoving(rampGo.transform, DecoCrate, "Cargo_Crate_3", new Vector3(side, 1f, 4f), 0f);
            DecoMoving(rampGo.transform, DecoBarrel, "Cargo_Barrel_1", new Vector3(side, 0f, 14f), 0f);
            DecoMoving(rampGo.transform, DecoBarrel, "Cargo_Barrel_2", new Vector3(side, 0f, 14.8f), 0f);
            DecoMoving(rampGo.transform, DecoCrate, "Cargo_Crate_4", new Vector3(-side, 0f, 15f), 0f);
            DecoMoving(rampGo.transform, DecoCrate, "Cargo_Crate_5", new Vector3(-side, 0f, 16f), 0f);
            DecoMoving(rampGo.transform, DecoBarrel, "Cargo_Barrel_3", new Vector3(-side, 0f, 2.5f), 0f);
            DecoMoving(rampGo.transform, DecoBarrel, "Cargo_Barrel_4", new Vector3(-side, 0f, 3.3f), 0f);

            // Guide frames on the platform's four edge midpoints (blades stick in the uprights and climb them).
            float frameH = balconyY + 4f;   // tall posts: a blade climbs well past the platform and the ledge
            float edgeN = hingeZ + platform + 0.7f, edgeS = hingeZ - 0.7f;
            Frame(play, "Frame_North", new Vector3(0f, 0f, edgeN), 0f, frameH);
            Frame(play, "Frame_South", new Vector3(0f, 0f, edgeS), 0f, frameH);
            Frame(play, "Frame_East", new Vector3(platform / 2f + 0.7f, 0f, hingeZ + platform / 2f), 90f, frameH);
            Frame(play, "Frame_West", new Vector3(-platform / 2f - 0.7f, 0f, hingeZ + platform / 2f), 90f, frameH);

            // Ceiling beam, two wheels; the north cord is the rope, the south one a chain.
            float wheelY = H - 1.2f;
            Vector3 yokeTop = new Vector3(0f, balconyY + 0.5f, hingeZ + platform);
            Vector3 wheelTop = new Vector3(0f, wheelY, hingeZ + platform);
            Vector3 cleatEnd = new Vector3(3f, balconyY + 1f, L / 2f - 0.6f);
            Block(geo, "CeilingBeam", new Vector3(0f, H - 0.5f, 0f), new Vector3(0.5f, 0.5f, L), MWood);
            Cylinder(geo, "Wheel_North", wheelTop, new Vector3(1.2f, 0.15f, 1.2f), MMetal, Quaternion.Euler(0f, 0f, 90f));
            Cylinder(geo, "Wheel_South", new Vector3(0f, wheelY, hingeZ), new Vector3(1.2f, 0.15f, 1.2f), MMetal, Quaternion.Euler(0f, 0f, 90f));
            ChainRun(geo, "ChainUp", new Vector3(0f, balconyY + 0.5f, hingeZ), new Vector3(0f, wheelY, hingeZ));
            ChainRun(geo, "ChainSlant", new Vector3(0f, wheelY, hingeZ), new Vector3(-3f, balconyY + 1f, -L / 2f + 0.6f));
            Block(geo, "Cleat_North", new Vector3(3f, balconyY + 1f, L / 2f - 0.4f), new Vector3(0.6f, 0.2f, 0.3f), MMetal);

            // The WHOLE rope is cuttable. The kit Rope is the vertical run (its root is the bottom of its 3 m cord: it stands
            // on the yoke, scaled up to the wheel) and the slant from the wheel down to the cleat is a RopeSegment of the
            // same rope: a blade anywhere along either run is the one cut, and both runs vanish with it.
            const float ropeRadius = 0.35f;   // fat enough for a thrown blade to meet a 0.12 m rope; nothing rests against it
            GameObject ropeGo = Kit(RopePrefab, play, "Rope", yokeTop, Quaternion.identity);
            ropeGo.transform.localScale = new Vector3(1f, (wheelY - yokeTop.y) / 3f, 1f);   // a 5.3 m run from the 3 m kit cord
            CapsuleCollider ropeCapsule = ropeGo.GetComponent<CapsuleCollider>();
            if (ropeCapsule != null) { ropeCapsule.radius = ropeRadius; ropeCapsule.excludeLayers = 1 << SceneValidator.LayerSoul; }
            Transform endBottom = ropeGo.transform.Find("CutEnds/End_Bottom");
            if (endBottom != null) endBottom.gameObject.SetActive(false);   // the yoke swings away with the platform: no stub in mid-air
            Rope rope = ropeGo.GetComponent<Rope>();
            RopeSegment slant = RopeCord(play, rope, "Rope_Slant", wheelTop, cleatEnd, 0.12f, ropeRadius, MRope);
            SetArray(rope, "segments", new List<RopeSegment> { slant });
            SetRef(rope, "ramp", ramp);
            SetRef(ramp, "rope", rope);

            // THE LOADING AREA (round 29b): on the floor under the entry balcony, goods waiting to go up stand in the two corners
            // either side of the way in (clear of the lane where the fallen ramp's end lands, z 5..8); a cart by the west wall.
            // Up on the south balcony, goods that already went up, well clear of the plate.
            Deco(geo, DecoCrate, "Load_Crate_1", new Vector3(-8f, 0f, 17f), 0f);
            Deco(geo, DecoCrate, "Load_Crate_2", new Vector3(-7f, 0f, 17f), 0f);
            Deco(geo, DecoCrate, "Load_Crate_3", new Vector3(-8f, 1f, 17f), 0f);
            Deco(geo, DecoBarrel, "Load_Barrel_1", new Vector3(7f, 0f, 18f), 0f);
            Deco(geo, DecoBarrel, "Load_Barrel_2", new Vector3(7.8f, 0f, 18f), 0f);
            Deco(geo, DecoCart, "Load_Cart", new Vector3(-12f, 0f, 12f), 90f);
            Deco(geo, DecoCrate, "Up_Crate_1", new Vector3(-6f, balconyY, -18f), 0f);
            Deco(geo, DecoCrate, "Up_Crate_2", new Vector3(-5f, balconyY, -18f), 0f);
            Deco(geo, DecoBarrel, "Up_Barrel_1", new Vector3(-8f, balconyY, -17f), 0f);

            // Plate on the south balcony latches the exit (one heavy body). Its line runs along the balcony, up the south wall
            // and into the exit door's own wire: the portal is dead until the plate latches (round 29). No sign.
            PressurePlate p = Plate(play, "Plate_Exit", new Vector3(5f, balconyY, -L / 2f + 3f), 8f, true, false);
            float sz = -L / 2f + 0.3f, rail = balconyY + 0.13f;
            List<Renderer> exitRails = new List<Renderer>();
            exitRails.Add(Conduit(geo, "Exit_0", new Vector3(5f, rail, -L / 2f + 1.8f), new Vector3(5f, rail, sz)));
            exitRails.Add(Conduit(geo, "Exit_1", new Vector3(5f, rail, sz), new Vector3(5f, balconyY + SupplyY, sz)));
            exitRails.Add(Conduit(geo, "Exit_2", new Vector3(5f, balconyY + SupplyY, sz), new Vector3(0f, balconyY + SupplyY, sz)));
            GateSouth(room, DoorCondition.Mode.PlateLatched, p, null, null, null, balconyY);
            exitRails.AddRange(DoorMagic(room));
            Line(play, "Line_Exit", p, exitRails);
            Profile(room, "well_room", Names[1], "A blade climbs the guide posts and cuts the rope; the platform swings into a ramp; the heavy weighs the exit plate.", 2);
            SaveRoom(room, PrefabPath(1));
        }

        // ------------------------------------------------------------------ Room 3: PORTER ROOM
// ------------------------------------------------------------------ Room 3: PORTER ROOM
        static void PorterRoom()
        {
            // An open hall with three goblins to kill. Behind it a wall with a big grate door into the guard room.
            // Bang the bars and the porter comes over from the OTHER side: a Dagger it pulls through the bars
            // and carries to its stand; anything heavier it refuses ("a shame I can only fit a dagger...").
            // The grate opens for good from the lever inside; the exit opens once the hall is cleared.
            const float W = 16f, L = 50f, H = 11f;
            const float partitionZ = -15f, partitionH = 9.5f;
            Transform geo, play;
            GameObject room = Shell("RoomV3_PorterRoom", W, L, H, 0f, out geo, out play);

            // The partition is THICK (owner, round 29c): 1.2 m, with the grate in the middle of its 3 x 3.5 passage, so the
            // raised grate is wholly inside the wall with only its bottom edge poking out under the lintel. The wall's faces are
            // at partitionZ +- 0.6; what used to sit 0.6 off a 0.5 m wall now sits 0.6 off the back face.
            const float partD = 1.2f;
            float backZ = partitionZ - partD / 2f;
            Block(geo, "Partition_Left", new Vector3(-W / 4f - 0.75f, partitionH / 2f, partitionZ), new Vector3(W / 2f - 1.5f, partitionH, partD), MWall);
            Block(geo, "Partition_Right", new Vector3(W / 4f + 0.75f, partitionH / 2f, partitionZ), new Vector3(W / 2f - 1.5f, partitionH, partD), MWall);
            Block(geo, "Partition_Lintel", new Vector3(0f, (3.5f + partitionH) / 2f, partitionZ), new Vector3(3f, partitionH - 3.5f, partD), MWall);
            GameObject gateGo = Kit(PorterGatePrefab, play, "GrateDoor", new Vector3(0f, 0f, partitionZ), Quaternion.identity);
            SetVector3(gateGo.GetComponent<PorterGate>(), "openOffset", new Vector3(0f, 3.3f, 0f));   // up 3.3 of its 3.5: the bottom just shows
            PorterGate gate = gateGo.GetComponent<PorterGate>();
            SetArray(gate, "porters", new List<GoblinBrain>());   // nobody opens it but the lever inside
            ImpactLever bell = null;
            Transform gatePanel = gateGo.transform.Find("Panel");
            if (gatePanel != null)
            {
                Lattice(gatePanel, 0.6f);   // bars on the hall side
                NavMeshModifier mod = gatePanel.gameObject.AddComponent<NavMeshModifier>();
                mod.ignoreFromBuild = true;
                // Shut, the panel carves the navmesh so no goblin walks through the bars; open, it rises clear of the floor.
                UnityEngine.AI.NavMeshObstacle obstacle = gatePanel.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.center = Vector3.zero;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                // The bars ARE the bell: any weapon hitting the panel is a bang that summons the porter.
                bell = gatePanel.gameObject.AddComponent<ImpactLever>();
                SetBool(bell, "latching", false);
                SetFloat(bell, "minSpeed", 3f);
                SetFloat(bell, "rearmSeconds", 0.3f);
            }
            GameObject wheel = Cylinder(geo, "Wheel_GrateDoor", new Vector3(0f, 4f, backZ - 0.3f), new Vector3(0.8f, 0.15f, 0.8f), MMagicFlow, Quaternion.Euler(0f, 0f, 90f));
            ChainRun(geo, "Chain_GrateDoor", new Vector3(0f, 4.2f, backZ - 0.3f), new Vector3(0f, partitionH + 0.5f, backZ - 0.3f));

            // Inside: the lever that lifts the grate for good, the porter's stand, and where it stands at the bars.
            ImpactLever lDoor = Lever(play, "Lever_Door", new Vector3(-3f, 1.5f, backZ - 0.6f), Quaternion.Euler(0f, 180f, 0f));
            SetRef(gate, "latchLever", lDoor);
            GameObject stand = Kit(StandPrefab, play, "Stand_Inside", new Vector3(-5.7f, 0f, -23f), Quaternion.identity);   // the far corner of the store, round the maze; its plinth 1.5 m off the west wall
            Transform slot = stand.transform.Find("Slot");
            GameObject bars = new GameObject("BarsPoint");
            bars.transform.SetParent(play, false);
            bars.transform.localPosition = new Vector3(0f, 0f, backZ - 1.3f);
            GameObject porter = Kit(PorterPrefab, play, "Goblin_Porter", new Vector3(3f, 0f, -17.5f), Quaternion.identity);   // beside the bars, clear of the maze
            GoblinBrain porterBrain = porter.GetComponent<GoblinBrain>();
            if (slot != null) SetRef(porterBrain, "dropPoint", slot);
            SetRef(porterBrain, "bell", bell);
            SetRef(porterBrain, "barsPoint", bars.transform);

            // THE RECEIVING HALL (docs/level-design/porter-room-v5.md, round 29d). THE COUNTER: waist high (1 m, 1 m thick), 8 m in
            // front of the grate, from the west wall to 3 m short of the east wall: the inspectors' side and the petitioners'. A
            // weapon hops it; goblins go round its open east end, so the fight has a shape. Two crate stacks on its top break the
            // sight line from the entry to the grate.
            float counterZ = partitionZ + partD / 2f + 8f;
            Block(geo, "Counter", new Vector3(-1.5f, 0.5f, counterZ), new Vector3(13f, 1f, 1f), MLedge);
            Deco(geo, DecoCrate, "Counter_Crate_1", new Vector3(-5f, 1f, counterZ), 0f);
            Deco(geo, DecoCrate, "Counter_Crate_2", new Vector3(-4f, 1f, counterZ), 0f);
            Deco(geo, DecoCrate, "Counter_Crate_3", new Vector3(-5f, 2f, counterZ), 0f);
            Deco(geo, DecoCrate, "Counter_Crate_4", new Vector3(2f, 1f, counterZ), 0f);
            Deco(geo, DecoCrate, "Counter_Crate_5", new Vector3(2f, 2f, counterZ), 0f);
            // THE RECEIVING DOCK: the hall's west side from the counter to the north wall, raised 0.5 m (a goblin riser: no step
            // needed, a weapon hops it), clear of the entry doorway. The petitioners' queue: barrels along its edge, the cart at
            // its head, a crate stack on it: cover for a Dagger to creep along while the inspectors hunt whatever moved last.
            const float dockY = 0.5f, dockE = -2f;
            float dockN = L / 2f, dockS = counterZ + 0.5f;
            Block(geo, "Dock", new Vector3((-W / 2f + dockE) / 2f, dockY / 2f, (dockS + dockN) / 2f), new Vector3(dockE + W / 2f, dockY, dockN - dockS), MLedge);
            Deco(geo, DecoBarrel, "Dock_Barrel_1", new Vector3(dockE - 0.45f, dockY, 14f), 0f);
            Deco(geo, DecoBarrel, "Dock_Barrel_2", new Vector3(dockE - 0.45f, dockY, 14.8f), 0f);
            Deco(geo, DecoBarrel, "Dock_Barrel_3", new Vector3(dockE - 0.45f, dockY, 4f), 0f);
            Deco(geo, DecoBarrel, "Dock_Barrel_4", new Vector3(dockE - 0.45f, dockY, 4.8f), 0f);
            Deco(geo, DecoCart, "Dock_Cart", new Vector3(-5.5f, dockY, 20.5f), 0f);
            Deco(geo, DecoCrate, "Dock_Crate_1", new Vector3(-5.5f, dockY, 9f), 0f);
            Deco(geo, DecoCrate, "Dock_Crate_2", new Vector3(-4.5f, dockY, 9f), 0f);
            Deco(geo, DecoCrate, "Dock_Crate_3", new Vector3(-5.5f, dockY + 1f, 9f), 0f);
            Deco(geo, DecoBarrel, "Hall_Barrel_1", new Vector3(5f, 0f, 2f), 0f);
            Deco(geo, DecoBarrel, "Hall_Barrel_2", new Vector3(5.8f, 0f, 2f), 0f);
            Deco(geo, DecoShelf, "Hall_Shelf", new Vector3(W / 2f - 0.3f, 0f, -1f), 90f);
            // THE STORE behind the grate: shelves flush along both walls and a crate maze (stacks 1.5 m apart and from the walls)
            // between the bars and the porter's stand in the far west corner, so the Dagger's carry goes round a corner through
            // goods. The lever and the grate's wire stay on the partition's back face.
            Deco(geo, DecoShelf, "Store_Shelf_W", new Vector3(-W / 2f + 0.3f, 0f, -19f), -90f);
            Deco(geo, DecoShelf, "Store_Shelf_E", new Vector3(W / 2f - 0.3f, 0f, -22f), 90f);
            Deco(geo, DecoCrate, "Store_Crate_1", new Vector3(-5f, 0f, -19.5f), 0f);
            Deco(geo, DecoCrate, "Store_Crate_2", new Vector3(-5f, 1f, -19.5f), 0f);
            Deco(geo, DecoCrate, "Store_Crate_3", new Vector3(-0.5f, 0f, -19.5f), 0f);
            Deco(geo, DecoCrate, "Store_Crate_4", new Vector3(0.5f, 0f, -19.5f), 0f);
            Deco(geo, DecoCrate, "Store_Crate_5", new Vector3(-0.5f, 1f, -19.5f), 0f);
            Deco(geo, DecoCrate, "Store_Crate_6", new Vector3(4f, 0f, -19.5f), 0f);
            Deco(geo, DecoCrate, "Store_Crate_7", new Vector3(2f, 0f, -22.5f), 0f);
            Deco(geo, DecoCrate, "Store_Crate_8", new Vector3(2f, 1f, -22.5f), 0f);

            // Three goblins in the hall; their volume clears the exit. The guard room is the porter's own volume.
            GameObject hallGo = Kit(RoomVolumePrefab, play, "HallVolume", new Vector3(0f, H / 2f, (partitionZ + L / 2f) / 2f), Quaternion.identity);
            BoxCollider hallBox = hallGo.GetComponent<BoxCollider>();
            if (hallBox != null) { hallBox.center = Vector3.zero; hallBox.size = new Vector3(W, H, L / 2f - partitionZ); }
            RoomVolume hall = hallGo.GetComponent<RoomVolume>();
            SetString(hall, "roomName", "Hall");
            GameObject gobA = Kit(GoblinPrefab, play, "Goblin_A", new Vector3(-4f, 0f, -10f), Quaternion.identity);   // the inspectors start behind the counter
            GameObject gobB = Kit(GoblinPrefab, play, "Goblin_B", new Vector3(3f, 0f, -12f), Quaternion.identity);
            GameObject gobC = Kit(GoblinPrefab, play, "Goblin_C", new Vector3(5f, 0f, -8f), Quaternion.identity);   // east gap around the counter
            SetArray(hall, "enemies", new List<GoblinBrain> {
                gobA.GetComponent<GoblinBrain>(), gobB.GetComponent<GoblinBrain>(), gobC.GetComponent<GoblinBrain>()
            });
            SetRef(gobA.GetComponent<GoblinBrain>(), "room", hall);
            SetRef(gobB.GetComponent<GoblinBrain>(), "room", hall);
            SetRef(gobC.GetComponent<GoblinBrain>(), "room", hall);
            GameObject guardGo = Kit(RoomVolumePrefab, play, "GuardRoomVolume", new Vector3(0f, H / 2f, (partitionZ - L / 2f) / 2f), Quaternion.identity);
            BoxCollider guardBox = guardGo.GetComponent<BoxCollider>();
            if (guardBox != null) { guardBox.center = Vector3.zero; guardBox.size = new Vector3(W, H, L / 2f + partitionZ); }
            RoomVolume guardRoom = guardGo.GetComponent<RoomVolume>();
            SetString(guardRoom, "roomName", "Guard Room");
            SetArray(guardRoom, "enemies", new List<GoblinBrain> { porterBrain });
            SetRef(porterBrain, "room", guardRoom);
            Door exit = GateSouth(room, DoorCondition.Mode.RoomCleared, null, null, hall, null, 0f);
            Line(play, "Line_Exit", exit, DoorMagic(room));
            // The grate's lever is a switch on a wire (round 29): its line runs up the partition's south face to the grate's
            // wheel, and the wheel lights when the lever is on. No sign.
            float gz = backZ - 0.3f;
            List<Renderer> grateRails = new List<Renderer>();
            grateRails.Add(Conduit(geo, "Grate_0", new Vector3(-3.6f, 1.2f, gz), new Vector3(-3.6f, 4f, gz)));
            grateRails.Add(Conduit(geo, "Grate_1", new Vector3(-3.6f, 4f, gz), new Vector3(-0.5f, 4f, gz)));
            grateRails.Add(wheel.GetComponent<Renderer>());
            Line(play, "Line_Grate", lDoor, grateRails);
            Profile(room, "porter_room", Names[2], "Kill the three goblins, bang the grate, play dead as the Dagger so the porter pulls you through the bars, then pull the lever inside.", 3);
            SaveRoom(room, PrefabPath(2));
        }

        // ------------------------------------------------------------------ Room 4: CIRCUIT ROOM
        static void CircuitRoom()
        {
            // docs/level-design/rooms-v4-revamp.md: the goblins' power room. The generator powers every teleport door on
            // the map; conduits leave it along the ceiling and down the walls, glowing. The one that feeds THIS room's
            // exit is broken twice, in the maintenance pit and on the gallery: two Sword-sized metal bodies lying still
            // close the breaks, stretch by stretch the conduit lights, and the door's dead portal comes alive (no slab).
            // Wiring rule: every rail ends exactly where the next starts, in a break's attach point, in a door's feed strip
            // or inside a wall (Conduit() runs each end half a rail past its point, so corners close and ends bury). The
            // cradles sit flush against a wall with their stubs on that wall's rail line. Rails have no colliders. No sign.
            const float W = 24f, L = 30f, H = 8f;
            const float exitX = 4f;
            const float off = 0.3f;                  // a rail's centre line off a wall face; a cradle flush to that wall has its stubs here
            const float rail = 0.6f;                 // wall conduits at hip height
            const float onFloor = 0.13f;             // a rail along a wall's base, at the stubs' own height
            float top = H - off;                     // ceiling conduits
            float wx = -W / 2f + off, ex = W / 2f - off, sz = -L / 2f + off;
            Transform geo, play;
            GameObject room = Shell("RoomV3_CircuitRoom", W, L, H, 0f, out geo, out play, exitX);

            // The floor with the maintenance pit cut out of its south-west corner.
            const float pitX0 = -11.5f, pitX1 = -5.5f, pitZ0 = -14.5f, pitZ1 = -6.5f, pitDepth = 2.5f;
            DestroyChild(geo, "Floor");
            Block(geo, "Floor_North", new Vector3(0f, -0.25f, (pitZ1 + L / 2f) / 2f), new Vector3(W, 0.5f, L / 2f - pitZ1), MFloor);
            // East and west of the pit the floor runs all the way to the south wall (round 28: it stopped at the pit's south
            // edge, leaving a half-metre slot along the wall in front of the exit door that weapons fell through).
            Block(geo, "Floor_SouthEast", new Vector3((pitX1 + W / 2f) / 2f, -0.25f, (pitZ1 - L / 2f) / 2f), new Vector3(W / 2f - pitX1, 0.5f, pitZ1 + L / 2f), MFloor);
            Block(geo, "Floor_SouthWest", new Vector3((pitX0 - W / 2f) / 2f, -0.25f, (pitZ1 - L / 2f) / 2f), new Vector3(pitX0 + W / 2f, 0.5f, pitZ1 + L / 2f), MFloor);
            Block(geo, "Floor_SouthStrip", new Vector3((pitX0 + pitX1) / 2f, -0.25f, (pitZ0 - L / 2f) / 2f), new Vector3(pitX1 - pitX0, 0.5f, pitZ0 + L / 2f), MFloor);
            Block(geo, "Pit_Floor", new Vector3((pitX0 + pitX1) / 2f, -pitDepth - 0.25f, (pitZ0 + pitZ1) / 2f), new Vector3(pitX1 - pitX0 + 1f, 0.5f, pitZ1 - pitZ0 + 1f), MFloor);
            // The pit's walls sit under the floor slabs: their tops stop 2 cm under the floor's top and their inner faces stand 2 cm
            // proud into the pit, so no face of theirs lies in a face of a slab (round 29d, the coplanar check).
            float pitWallY = -(pitDepth + 0.02f) / 2f, pitWallH = pitDepth - 0.02f;
            Block(geo, "Pit_North", new Vector3((pitX0 + pitX1) / 2f, pitWallY, pitZ1 + 0.24f), new Vector3(pitX1 - pitX0 + 1f, pitWallH, 0.52f), MWall);
            Block(geo, "Pit_South", new Vector3((pitX0 + pitX1) / 2f, pitWallY, pitZ0 - 0.24f), new Vector3(pitX1 - pitX0 + 1f, pitWallH, 0.52f), MWall);
            Block(geo, "Pit_East", new Vector3(pitX1 + 0.24f, pitWallY, (pitZ0 + pitZ1) / 2f), new Vector3(0.52f, pitWallH, pitZ1 - pitZ0 + 1f), MWall);
            Block(geo, "Pit_West", new Vector3(pitX0 - 0.24f, pitWallY, (pitZ0 + pitZ1) / 2f), new Vector3(0.52f, pitWallH, pitZ1 - pitZ0 + 1f), MWall);
            // Goblin steps down into the pit from its north edge (0.5 m risers), the top step against the pit's face: no pocket.
            Stair(geo, "PitSteps", new Vector3(-8f, -pitDepth, pitZ1 - 4 * 0.6f), Vector3.forward, 4, 0.5f, 0.6f, 2f);

            // The gallery: a mezzanine over the exit door along the south-east, with its own goblin stair up the east wall.
            const float galleryY = 4.05f;   // 5 cm above the exit door frame's top (4.0) under it: no shared face
            Block(geo, "Gallery", new Vector3(6f, galleryY - 0.25f, -12f), new Vector3(12f, 0.5f, 6f), MLedge);
            Stair(geo, "GalleryStair", new Vector3(11f, 0f, -2f), Vector3.back, 8, 0.5f, 0.6f, 2f);
            Block(geo, "GalleryLanding", new Vector3(11f, galleryY / 2f, -7.9f), new Vector3(2f, galleryY, 2.2f), MLedge);

            // The generator: floor to ceiling, a crystal core in a cage with two halos. Every conduit starts at its top.
            Vector3 gen = new Vector3(0f, 0f, 2f);
            Generator(geo, gen, H);
            Vector3 genTop = gen + new Vector3(0f, top, 0f);

            // Intact conduits, live for good. East: along the ceiling, down the east wall, north along it and out through the
            // north wall. North: along the ceiling to the entry and down the north wall into the entry door's own supply wire
            // (SupplyY, where the prefab's Supply_In turns into the wall). South: south along the ceiling, a jog west, on to the
            // south wall west of the gallery and down it through the floor (it used to share the east run's line). The rest: along the
            // ceiling, down the south wall through the gallery's slab and out through the floor.
            Conduit(geo, "Live_E_1", genTop, new Vector3(ex, top, gen.z));
            Conduit(geo, "Live_E_2", new Vector3(ex, top, gen.z), new Vector3(ex, rail, gen.z));
            Conduit(geo, "Live_E_3", new Vector3(ex, rail, gen.z), new Vector3(ex, rail, L / 2f + 0.2f));
            Conduit(geo, "Live_N_1", genTop, new Vector3(0f, top, L / 2f - off));
            Conduit(geo, "Live_N_2", new Vector3(0f, top, L / 2f - off), new Vector3(0f, SupplyY, L / 2f - off));
            Conduit(geo, "Live_S_1", genTop, new Vector3(0f, top, gen.z - 3f));
            Conduit(geo, "Live_S_2", new Vector3(0f, top, gen.z - 3f), new Vector3(-1.5f, top, gen.z - 3f));
            Conduit(geo, "Live_S_3", new Vector3(-1.5f, top, gen.z - 3f), new Vector3(-1.5f, top, sz));
            Conduit(geo, "Live_S_4", new Vector3(-1.5f, top, sz), new Vector3(-1.5f, -0.3f, sz));

            // THE DEAD RUN. Stretch 0: ceiling west, down the west wall, south along it at hip height, over the pit's lip,
            // down the pit's north-west corner and along the pit's west wall at floor level into BREAK 1 (flush to that wall).
            float px = pitX0 + off;
            float pitRail = -pitDepth + onFloor;
            CircuitBridge b1 = BridgeAt(play, "Break_Pit", new Vector3(px, -pitDepth, -11f), 90f, 1.05f);   // A north, B south
            Vector3 b1In = geo.InverseTransformPoint(b1.AttachA), b1Out = geo.InverseTransformPoint(b1.AttachB);
            List<Renderer> s0 = new List<Renderer>();
            s0.Add(Conduit(geo, "Dead_0a", genTop, new Vector3(wx, top, gen.z)));
            s0.Add(Conduit(geo, "Dead_0b", new Vector3(wx, top, gen.z), new Vector3(wx, rail, gen.z)));
            s0.Add(Conduit(geo, "Dead_0c", new Vector3(wx, rail, gen.z), new Vector3(wx, rail, pitZ1 - off)));
            s0.Add(Conduit(geo, "Dead_0d", new Vector3(wx, rail, pitZ1 - off), new Vector3(px, rail, pitZ1 - off)));
            s0.Add(Conduit(geo, "Dead_0e", new Vector3(px, rail, pitZ1 - off), new Vector3(px, pitRail, pitZ1 - off)));
            s0.Add(Conduit(geo, "Dead_0f", new Vector3(px, pitRail, pitZ1 - off), b1In));
            // Stretch 1: on along the pit's west wall, up its south-west corner, east along the south wall at hip height, up
            // that wall just west of the gallery and along the gallery's floor into BREAK 2 (flush to the south wall).
            CircuitBridge b2 = BridgeAt(play, "Break_Gallery", new Vector3(6f, galleryY, sz), 0f, 1.05f);   // A west, B east
            Vector3 b2In = geo.InverseTransformPoint(b2.AttachA), b2Out = geo.InverseTransformPoint(b2.AttachB);
            float galleryRail = galleryY + onFloor;
            List<Renderer> s1 = new List<Renderer>();
            s1.Add(Conduit(geo, "Dead_1a", b1Out, new Vector3(px, pitRail, pitZ0 + off)));
            s1.Add(Conduit(geo, "Dead_1b", new Vector3(px, pitRail, pitZ0 + off), new Vector3(px, rail, pitZ0 + off)));
            s1.Add(Conduit(geo, "Dead_1c", new Vector3(px, rail, pitZ0 + off), new Vector3(px, rail, sz)));
            s1.Add(Conduit(geo, "Dead_1d", new Vector3(px, rail, sz), new Vector3(-0.5f, rail, sz)));
            s1.Add(Conduit(geo, "Dead_1e", new Vector3(-0.5f, rail, sz), new Vector3(-0.5f, galleryRail, sz)));
            s1.Add(Conduit(geo, "Dead_1f", new Vector3(-0.5f, galleryRail, sz), b2In));
            // Stretch 2: east a step along the gallery floor, UP the south wall and west along it at the height where the exit
            // door's own supply wire tops out and turns into the wall (SupplyY; the MagicDoor prefab's `Supply_In` crosses this
            // rail line there), so the dead run ends in the door's wire and the portal below is fed by it.
            List<Renderer> s2 = new List<Renderer>();
            s2.Add(Conduit(geo, "Dead_2a", b2Out, new Vector3(7.6f, galleryRail, sz)));
            s2.Add(Conduit(geo, "Dead_2b", new Vector3(7.6f, galleryRail, sz), new Vector3(7.6f, SupplyY, sz)));
            s2.Add(Conduit(geo, "Dead_2c", new Vector3(7.6f, SupplyY, sz), new Vector3(exitX, SupplyY, sz)));

            // The exit: a DEAD PORTAL (owner, round 29): no sliding slab; the portal and its wire wear the dead magic until the
            // circuit is whole, then come alive and the doorway opens.
            GameObject circuitGo = new GameObject("Circuit");
            circuitGo.transform.SetParent(play, false);
            Circuit circuit = circuitGo.AddComponent<Circuit>();
            GateSouth(room, DoorCondition.Mode.CircuitLive, null, null, null, null, 0f, exitX, circuit);
            List<Renderer> consumers = DoorMagic(room);
            SetArray(circuit, "bridges", new List<CircuitBridge> { b1, b2 });
            SetSegments(circuit, new List<List<Renderer>> { s0, s1, s2 });
            SetArray(circuit, "consumers", consumers);

            // Decorations: flush to each other or 1.5 m clear of everything; the crate stack hides the pit's break from the entry.
            // No pillars (owner, round 27).
            Deco(geo, DecoCrate, "Crate_1", new Vector3(-7.5f, 0f, -4.5f), 0f);
            Deco(geo, DecoCrate, "Crate_2", new Vector3(-6.5f, 0f, -4.5f), 0f);
            Deco(geo, DecoCrate, "Crate_3", new Vector3(-7.5f, 1f, -4.5f), 0f);
            Deco(geo, DecoBarrel, "Barrel_1", new Vector3(3f, 0f, 5.4f), 0f);
            Deco(geo, DecoBarrel, "Barrel_2", new Vector3(3.8f, 0f, 5.4f), 0f);
            Deco(geo, DecoShelf, "Shelf_1", new Vector3(W / 2f - 0.3f, 0f, 8f), 90f);   // flush to the east wall (round 29: 0.55 left a 0.25 m slot)

            Profile(room, "circuit_room", Names[3], "Two Sword-sized metal bodies lie still across the two breaks, in the pit and on the gallery; the conduit lights to the door.", 3);
            SaveRoom(room, PrefabPath(3));
        }

        // ------------------------------------------------------------------ Room 5: BAT ROOM (exit beyond)
        static void BatRoom()
        {
            // docs/level-design/rooms-v4-revamp.md: the fallen bridge. Both doors at bank level, a cistern between the
            // banks, a bridge of pure magic that is dead because the short break on the goblins' side is open. A heavy
            // bats a braced Dagger across (a bat always leaves at 35 deg or steeper); it plays dead among two goblins,
            // lies across the break, the bridge comes alive. One goblin on the near bank attacks whatever moves. A magnet
            // over the near lip lifts metal out of the cistern; a goblin stair in the far west corner serves the rest.
            // THE WIDTH IS THE MATH. Gravity is 20 m/s^2 and a braced bat leaves at the 35 deg lift floor at
            // batBraceSpeed * mA / (mA + mB). At the owner's "super far" 26: Hammer on Dagger 24.3 m/s flies 27.7 m, Mace
            // on Dagger 25 m, Orb on Dagger 20 m, Sword on Dagger 18 m, Staff on Dagger 16 m; abutment tip to tip is 12 m,
            // the far wall 25 m, so every batter clears it and the heavies smack the far wall. (At 19 it was Hammer 14.7,
            // Mace 13.4, Orb 10.9, Sword 9.7: only the heavies crossed.) The banks are 12 m up, the owner's 2x.
            const float W = 24f, L = 38f, H = 30f, bankY = 12f;
            const float gap = 14f;                                   // bank face to bank face, z -7..7
            const float stubLen = 1f;                                // the old bridge's abutments: the flight is gap - 2
            const float off = 0.3f, onFloor = 0.13f;
            float tipToTip = gap - 2f * stubLen;
            Transform geo, play;
            GameObject room = Shell("RoomV3_BatRoom", W, L, H, bankY, out geo, out play);
            // The entry up at bank level too: the north wall is rebuilt with its opening raised (Shell only raises the exit),
            // and the spawn point, torches, sign and number go up with it.
            DestroyChild(geo, "Wall_North");
            DoorwayWall(geo, "Wall_North", L / 2f + 0.25f, W, H, 0f, bankY);
            Transform north = room.transform.Find("Doorways/Doorway_North");
            if (north != null) north.localPosition = new Vector3(0f, bankY, L / 2f);
            Transform spawn = room.transform.Find("SpawnPoint");
            if (spawn != null) spawn.localPosition = new Vector3(0f, bankY + 0.6f, L / 2f - 4f);
            Transform fixtures = room.transform.Find("Fixtures");
            if (fixtures != null) foreach (Transform f in fixtures) f.localPosition += Vector3.up * bankY;

            // Banks, the old bridge's abutments over the gap, rubble below (1.5 m clear of everything), the goblin stair
            // flush to the west wall topping out at the near bank's face.
            float bankDepth = L / 2f - gap / 2f;
            Block(geo, "Bank_North", new Vector3(0f, bankY / 2f, gap / 2f + bankDepth / 2f), new Vector3(W, bankY, bankDepth), MLedge);
            Block(geo, "Bank_South", new Vector3(0f, bankY / 2f, -gap / 2f - bankDepth / 2f), new Vector3(W, bankY, bankDepth), MLedge);
            Block(geo, "BridgeStub_N", new Vector3(0f, bankY - 0.3f, gap / 2f - stubLen / 2f), new Vector3(4.4f, 0.6f, stubLen), MLedge);
            Block(geo, "BridgeStub_S", new Vector3(0f, bankY - 0.3f, -gap / 2f + stubLen / 2f), new Vector3(4.4f, 0.6f, stubLen), MLedge);
            // The fallen bridge's rubble, 1.5 m clear of the walls, the stair and each other.
            Deco(geo, DecoRubble, "Rubble_1", new Vector3(-5f, 0f, 2f), 20f);
            Deco(geo, DecoRubble, "Rubble_2", new Vector3(4f, 0f, 2f), -35f);
            Deco(geo, DecoRubble, "Rubble_3", new Vector3(-2f, 0f, -3f), 70f);
            Deco(geo, DecoRubble, "Rubble_4", new Vector3(7f, 0f, -2f), 10f);
            // The goblin stair runs ALONG the near bank's face, west to the corner: its foot near the middle of the cistern
            // floor, its top step at the bank's north-west corner, every step flush against the face (no pocket behind it).
            int steps = Mathf.RoundToInt(bankY / 0.5f);
            Stair(geo, "CisternStair", new Vector3(-W / 2f + steps * 0.6f, 0f, gap / 2f - 1f), Vector3.left, steps, 0.5f, 0.6f, 2f);

            // The magic bridge: dead until the circuit on the far bank is whole, and dead it is not there at all. Not in
            // the NavMesh. Its top a centimetre under the abutments' and its ends a hand inside them: no face shares a
            // plane with the banks (no flicker) and nothing of it shows past the abutments.
            GameObject bridgeGo = Kit(MagicBridgePrefab, play, "MagicBridge", new Vector3(0f, bankY, 0f), Quaternion.identity);
            MagicBridge magicBridge = bridgeGo.GetComponent<MagicBridge>();
            Transform deck = bridgeGo.transform.Find("Deck");
            if (deck != null)
            {
                deck.localPosition = new Vector3(0f, -0.16f, 0f);
                deck.localScale = new Vector3(4f, 0.3f, tipToTip + 0.2f);
                NavMeshModifier mod = deck.gameObject.AddComponent<NavMeshModifier>();
                mod.ignoreFromBuild = true;
            }

            // The far bank's conduit: out of the south wall beside the exit, down it, north along the bank's floor into the
            // short break (fed from the south), on to the old abutment and into the bridge.
            float sz = -L / 2f + off;
            float bankRail = bankY + onFloor;
            float stubZ = -gap / 2f + stubLen / 2f;
            CircuitBridge b3 = BridgeAt(play, "Break_Short", new Vector3(3.5f, bankY, -gap / 2f - 2f), -90f, 0.5f);   // A south, B north
            Vector3 b3In = geo.InverseTransformPoint(b3.AttachA), b3Out = geo.InverseTransformPoint(b3.AttachB);
            List<Renderer> s0 = new List<Renderer>();
            s0.Add(Conduit(geo, "Feed_0a", new Vector3(3.5f, bankY + 3f, -L / 2f - 0.2f), new Vector3(3.5f, bankY + 3f, sz)));
            s0.Add(Conduit(geo, "Feed_0b", new Vector3(3.5f, bankY + 3f, sz), new Vector3(3.5f, bankRail, sz)));
            s0.Add(Conduit(geo, "Feed_0c", new Vector3(3.5f, bankRail, sz), b3In));
            List<Renderer> s1 = new List<Renderer>();
            s1.Add(Conduit(geo, "Feed_1a", b3Out, new Vector3(3.5f, bankRail, stubZ)));
            s1.Add(Conduit(geo, "Feed_1b", new Vector3(3.5f, bankRail, stubZ), new Vector3(2.1f, bankRail, stubZ)));
            GameObject circuitGo = new GameObject("Circuit");
            circuitGo.transform.SetParent(play, false);
            Circuit circuit = circuitGo.AddComponent<Circuit>();
            SetArray(circuit, "bridges", new List<CircuitBridge> { b3 });
            SetSegments(circuit, new List<List<Renderer>> { s0, s1 });
            List<Renderer> consumers = new List<Renderer>();
            if (deck != null) consumers.Add(deck.GetComponent<Renderer>());
            SetArray(circuit, "consumers", consumers);
            SetRef(magicBridge, "circuit", circuit);

            // The magnet over the near lip, east side, clear of the bat's flight line down the middle. Metal gathers under the
            // red head itself (the prefab's own hold point, 1.7 m below it). Its box covers the cistern beneath it and the
            // lip but NOT the bank's top, so a body that launches off the hold onto the bank is out of its reach at once.
            GameObject magnet = Kit(MagnetPrefab, play, "Magnet", new Vector3(9f, bankY + 6f, gap / 2f - 1f), Quaternion.identity);
            BoxCollider volume = magnet.GetComponent<BoxCollider>();
            if (volume != null) { volume.center = new Vector3(-1.5f, -(bankY + 6f) / 2f, -1.75f); volume.size = new Vector3(9f, bankY + 7f, 6.5f); }
            SetFloat(magnet.GetComponent<MagnetZone>(), "range", bankY + 8f);
            Block(geo, "MagnetPost", new Vector3(9f, bankY + 3f, gap / 2f + 1.2f), new Vector3(0.4f, 6f, 0.4f), MMetal);
            Block(geo, "MagnetArm", new Vector3(9f, bankY + 6.2f, gap / 2f + 0.1f), new Vector3(0.4f, 0.4f, 2.6f), MMetal);

            // Goblins: one on the near bank, two on the far bank, each hearing only its own bank.
            GameObject nearVol = Kit(RoomVolumePrefab, play, "NearBankVolume", new Vector3(0f, bankY + 5f, gap / 2f + bankDepth / 2f), Quaternion.identity);
            BoxCollider nearBox = nearVol.GetComponent<BoxCollider>();
            if (nearBox != null) { nearBox.center = Vector3.zero; nearBox.size = new Vector3(W, 10f, bankDepth); }
            GameObject farVol = Kit(RoomVolumePrefab, play, "FarBankVolume", new Vector3(0f, bankY + 5f, -gap / 2f - bankDepth / 2f), Quaternion.identity);
            BoxCollider farBox = farVol.GetComponent<BoxCollider>();
            if (farBox != null) { farBox.center = Vector3.zero; farBox.size = new Vector3(W, 10f, bankDepth); }
            RoomVolume nearRoom = nearVol.GetComponent<RoomVolume>();
            RoomVolume farRoom = farVol.GetComponent<RoomVolume>();
            SetString(nearRoom, "roomName", "Near Bank");
            SetString(farRoom, "roomName", "Far Bank");
            GameObject gNear = Kit(GoblinPrefab, play, "Goblin_Near", new Vector3(6f, bankY, gap / 2f + 6f), Quaternion.identity);
            GameObject gFarA = Kit(GoblinPrefab, play, "Goblin_Far_A", new Vector3(-5f, bankY, -gap / 2f - 5f), Quaternion.identity);
            GameObject gFarB = Kit(GoblinPrefab, play, "Goblin_Far_B", new Vector3(5f, bankY, -gap / 2f - 8f), Quaternion.identity);
            SetArray(nearRoom, "enemies", new List<GoblinBrain> { gNear.GetComponent<GoblinBrain>() });
            SetArray(farRoom, "enemies", new List<GoblinBrain> { gFarA.GetComponent<GoblinBrain>(), gFarB.GetComponent<GoblinBrain>() });
            SetRef(gNear.GetComponent<GoblinBrain>(), "room", nearRoom);
            SetRef(gFarA.GetComponent<GoblinBrain>(), "room", farRoom);
            SetRef(gFarB.GetComponent<GoblinBrain>(), "room", farRoom);

            // Decorations: barrels and crates on the goblins' bank; flush or 1.5 m clear. No pillars (owner, round 27).
            Deco(geo, DecoBarrel, "Barrel_1", new Vector3(-8f, bankY, -L / 2f + 4f), 0f);
            Deco(geo, DecoBarrel, "Barrel_2", new Vector3(-7.2f, bankY, -L / 2f + 4f), 0f);
            Deco(geo, DecoCrate, "Crate_1", new Vector3(8f, bankY, -L / 2f + 3f), 0f);
            Deco(geo, DecoCrate, "Crate_2", new Vector3(8f, bankY + 1f, -L / 2f + 3f), 0f);

            Door exit = GateSouth(room, DoorCondition.Mode.AlwaysOpen, null, null, null, null, bankY);
            Line(play, "Line_Exit", exit, DoorMagic(room));
            Profile(room, "bat_room", Names[4], "The heavy bats a braced Dagger over the cistern; it plays dead, lies across the short break, the magic bridge comes alive; everyone crosses to the Exit.", 4);
            SaveRoom(room, PrefabPath(4));
        }

        // ------------------------------------------------------------------ data + install
        static void ConfigureData()
        {
            RunDef run = AssetDatabase.LoadAssetAtPath<RunDef>(RunDefAsset);
            if (run == null) { Debug.LogError("Missing " + RunDefAsset); return; }
            run.roomsPerRun = Names.Length;
            run.guaranteedRoomIds = new int[Names.Length];
            for (int i = 0; i < Names.Length; i++) run.guaranteedRoomIds[i] = FirstRoomId + i;
            run.timerSeconds = 480f;   // the designer's budget for a pair over five rooms is 5:45-7:00; the owner may trim
            EditorUtility.SetDirty(run);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Installs the built prefabs into the open Run scene: all five, or only the indices in `only`.</summary>
        public static void InstallIntoOpenRunScene(Scene scene, int[] only = null)
        {
            if (!scene.IsValid() || scene.path != RunScene) { Debug.LogError("[RoomsV3] expected the Run scene, got " + scene.path); return; }
            GameObject environment = FindRoot(scene, "Environment");
            GameObject managers = FindRoot(scene, "_Managers");
            Transform roomsRoot = environment != null ? environment.transform.Find("Rooms") : null;
            if (roomsRoot == null || managers == null) { Debug.LogError("[RoomsV3] Run scene is missing Environment/Rooms or _Managers."); return; }
            for (int i = 0; i < Names.Length; i++) if (Wanted(only, i)) ReplaceRoom(roomsRoot, FirstRoomId + i, PrefabPath(i), Names[i]);

            WorldAuthority authority = managers.GetComponentInChildren<WorldAuthority>(true);
            LabyrinthDirector director = managers.GetComponentInChildren<LabyrinthDirector>(true);
            RunDirector run = managers.GetComponentInChildren<RunDirector>(true);
            LevelClock clock = managers.GetComponentInChildren<LevelClock>(true);
            for (int i = 0; i < Names.Length; i++)
            {
                if (!Wanted(only, i)) continue;
                Transform room = roomsRoot.Find("Room_" + (FirstRoomId + i).ToString("00") + "_" + SafeName(Names[i]));
                if (room != null) WireSceneReferences(room.gameObject, authority, director, clock, Names[i]);
            }
            RebuildSceneRegistries(scene, roomsRoot, authority, director, run);
            RunSceneBuilder.AssignSceneIds(scene);
            BakeNavMesh(environment, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

static void BakeNavMesh(GameObject environment, Scene scene)
        {
            NavMeshSurface surface = environment.GetComponent<NavMeshSurface>();
            if (surface == null) surface = environment.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.layerMask = 1 << LayerWorld;
            surface.BuildNavMesh();

            // NavMeshData only serializes in binary. Left embedded in the scene it turns the WHOLE scene file binary
            // (no diffs, no merges), so it goes into its own asset next to the scene, as the Bake button does.
            UnityEngine.AI.NavMeshData data = surface.navMeshData;
            if (data == null || AssetDatabase.Contains(data)) return;
            string sceneDir = System.IO.Path.GetDirectoryName(scene.path).Replace('\\', '/');
            string folder = sceneDir + "/" + scene.name;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(sceneDir, scene.name);
            string assetPath = folder + "/NavMesh-" + surface.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            data.name = "NavMesh-" + surface.name;
            AssetDatabase.CreateAsset(data, assetPath);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(assetPath);
            EditorUtility.SetDirty(surface);
        }

static void ReplaceRoom(Transform roomsRoot, int roomId, string prefabPath, string displayName)
        {
            // The old occupant of this id: by id, by installed name, or by prefab name (a variant re-save resets an
            // instance's overrides: name, position and roomId all go back to the asset's).
            Transform old = null;
            string prefix = "Room_" + roomId.ToString("00") + "_";
            string prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            foreach (Transform child in roomsRoot)
            {
                LabyrinthRoom candidate = child.GetComponent<LabyrinthRoom>();
                bool byId = candidate != null && candidate.RoomId == roomId;
                if (byId || child.name.StartsWith(prefix) || child.name == prefabName) { old = child; break; }
            }
            // Rooms sit on a 5-wide, 200 m lattice by id; keep a sane old position, else recompute it.
            Vector3 position = new Vector3((roomId % 5) * 200f, 0f, -(roomId / 5) * 200f);
            Quaternion rotation = Quaternion.identity;
            int sibling = -1;
            if (old != null)
            {
                if (old.position.sqrMagnitude > 1f || roomId == 0) { position = old.position; rotation = old.rotation; }
                sibling = old.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);
            }
            else Debug.LogWarning("[RoomsV3] no previous room for id " + roomId + "; placing it on the lattice");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, roomsRoot);
            room.name = "Room_" + roomId.ToString("00") + "_" + SafeName(displayName);
            room.transform.SetPositionAndRotation(position, rotation);
            if (sibling >= 0) room.transform.SetSiblingIndex(sibling);
            SetInt(room.GetComponent<LabyrinthRoom>(), "roomId", roomId);
        }

        static void WireSceneReferences(GameObject room, WorldAuthority authority, LabyrinthDirector director, LevelClock clock, string roomName)
        {
            foreach (MagicDoor x in room.GetComponentsInChildren<MagicDoor>(true)) { SetRef(x, "authority", authority); SetRef(x, "labyrinth", director); }
            foreach (Door x in room.GetComponentsInChildren<Door>(true)) SetRef(x, "authority", authority);
            foreach (ImpactLever x in room.GetComponentsInChildren<ImpactLever>(true)) { SetRef(x, "authority", authority); SetRef(x, "clock", clock); }
            foreach (PressurePlate x in room.GetComponentsInChildren<PressurePlate>(true)) SetRef(x, "authority", authority);
            foreach (CircuitBridge x in room.GetComponentsInChildren<CircuitBridge>(true)) SetRef(x, "authority", authority);
            foreach (Rope x in room.GetComponentsInChildren<Rope>(true)) { SetRef(x, "authority", authority); SetRef(x, "clock", clock); }
            foreach (DropRamp x in room.GetComponentsInChildren<DropRamp>(true)) SetRef(x, "clock", clock);
            foreach (PorterGate x in room.GetComponentsInChildren<PorterGate>(true)) SetRef(x, "authority", authority);
            foreach (GoblinBrain x in room.GetComponentsInChildren<GoblinBrain>(true)) SetRef(x, "authority", authority);
            RoomVolume[] volumes = room.GetComponentsInChildren<RoomVolume>(true);
            foreach (RoomVolume v in volumes) if (v.transform.parent == room.transform) SetString(v, "roomName", roomName);
            // Enemies with no room of their own belong to the room's main volume.
            RoomVolume main = null;
            foreach (RoomVolume v in volumes) if (v.transform.parent == room.transform) { main = v; break; }
            if (main != null)
            {
                List<GoblinBrain> own = new List<GoblinBrain>();
                foreach (GoblinBrain g in room.GetComponentsInChildren<GoblinBrain>(true))
                {
                    SerializedObject so = new SerializedObject(g);
                    SerializedProperty p = so.FindProperty("room");
                    if (p != null && p.objectReferenceValue == null) { p.objectReferenceValue = main; so.ApplyModifiedPropertiesWithoutUndo(); own.Add(g); }
                }
                if (own.Count > 0) SetArray(main, "enemies", own);
            }
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
            SetArray(authority, "enemies", ComponentsInScene<GoblinBrain>(scene));
            SetArray(authority, "magicDoors", ComponentsInScene<MagicDoor>(scene));
            SetArray(authority, "rooms", ComponentsInScene<RoomVolume>(scene));
            SetArray(authority, "doors", ComponentsInScene<Door>(scene));
            SetArray(authority, "plates", ComponentsInScene<PressurePlate>(scene));
            SetArray(authority, "circuitBridges", ComponentsInScene<CircuitBridge>(scene));
            SetArray(authority, "levers", ComponentsInScene<ImpactLever>(scene));
            SetArray(authority, "ropes", ComponentsInScene<Rope>(scene));
            SetArray(authority, "porterGates", ComponentsInScene<PorterGate>(scene));
            SetArray(authority, "counterweights", ComponentsInScene<CounterweightPair>(scene));
            SetArray(authority, "scales", ComponentsInScene<ScalesLock>(scene));
            SetArray(authority, "lightningFields", ComponentsInScene<LightningField>(scene));
            SetArray(authority, "magnets", ComponentsInScene<MagnetZone>(scene));
            SetArray(authority, "lifts", ComponentsInScene<Lift>(scene));
            SetArray(authority, "anvils", ComponentsInScene<AnvilStation>(scene));
        }

        // ------------------------------------------------------------------ room shell
        /// <summary>A TwoDoor room unpacked, its square geometry replaced by a W x L x H box with the entry (north) at
        /// floor level and the exit (south) doorway at exitY, offset exitX along the south wall.</summary>
        static GameObject Shell(string objectName, float w, float l, float h, float exitY, out Transform geo, out Transform play, float exitX = 0f)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TwoDoorPrefab);
            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(source);
            room.name = objectName;
            // Kept as a prefab VARIANT of the two-door room: only the removed base geometry, the moved doorways and the
            // added pieces are serialized, not every doorway frame, torch and label again (an unpacked copy is 7-9k lines).
            geo = room.transform.Find("Geometry");
            play = room.transform.Find("Gameplay");
            if (play == null) { GameObject g = new GameObject("Gameplay"); g.transform.SetParent(room.transform, false); play = g.transform; }
            for (int i = geo.childCount - 1; i >= 0; i--) Object.DestroyImmediate(geo.GetChild(i).gameObject);
            DestroyChild(play, "Seal");
            Transform doorways = room.transform.Find("Doorways");
            DestroyChild(doorways, "Gate_South");

            Block(geo, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(w, 0.5f, l), MFloor);
            Block(geo, "Roof", new Vector3(0f, h + 0.25f, 0f), new Vector3(w + 1f, 0.5f, l + 1f), MWall);
            Block(geo, "Wall_East", new Vector3(w / 2f + 0.25f, h / 2f, 0f), new Vector3(0.5f, h, l + 1f), MWall);
            Block(geo, "Wall_West", new Vector3(-w / 2f - 0.25f, h / 2f, 0f), new Vector3(0.5f, h, l + 1f), MWall);
            DoorwayWall(geo, "Wall_North", l / 2f + 0.25f, w, h, 0f, 0f);
            DoorwayWall(geo, "Wall_South", -l / 2f - 0.25f, w, h, exitX, exitY);

            Transform north = doorways.Find("Doorway_North");
            Transform south = doorways.Find("Doorway_South");
            if (north != null) north.localPosition = new Vector3(0f, 0f, l / 2f);
            if (south != null) south.localPosition = new Vector3(exitX, exitY, -l / 2f);

            Transform volume = room.transform.Find("RoomVolume");
            BoxCollider vb = volume != null ? volume.GetComponent<BoxCollider>() : null;
            if (vb != null) { volume.localPosition = Vector3.zero; vb.center = new Vector3(0f, h / 2f, 0f); vb.size = new Vector3(w, h + 1f, l); }
            Transform footprint = room.transform.Find("Footprint");
            BoxCollider fb = footprint != null ? footprint.GetComponent<BoxCollider>() : null;
            if (fb != null) { footprint.localPosition = Vector3.zero; fb.center = new Vector3(0f, h / 2f, 0f); fb.size = new Vector3(w + 12f, h + 30f, l + 12f); }
            Transform spawn = room.transform.Find("SpawnPoint");
            if (spawn != null) spawn.localPosition = new Vector3(0f, 0.6f, l / 2f - 4f);
            Transform fixtures = room.transform.Find("Fixtures");
            if (fixtures != null)
            {
                // No signs: the owner is removing them all (a narrator will hint later). The two-door base still carries one.
                DestroyChild(fixtures, "RoomSign");
                Transform number = fixtures.Find("FloorNumber");
                if (number != null) number.localPosition = new Vector3(0f, 0.02f, l / 2f - 8f);
                string[] torches = { "Torch_1", "Torch_2", "Torch_3", "Torch_4" };
                Vector3[] at = { new Vector3(-w / 2f + 0.6f, 2.5f, l / 4f), new Vector3(w / 2f - 0.6f, 2.5f, l / 4f), new Vector3(-w / 2f + 0.6f, 2.5f, -l / 4f), new Vector3(w / 2f - 0.6f, 2.5f, -l / 4f) };
                for (int i = 0; i < torches.Length; i++) { Transform t = fixtures.Find(torches[i]); if (t != null) t.localPosition = at[i]; }
            }
            return room;
        }

        /// <summary>Four corner walls that turn the W x W box into an octagon (flat to flat = W).</summary>
/// <summary>Four corner walls that turn the W x W box into a regular octagon (flat to flat = W, no gaps:
        /// each corner wall is centred W/2 from the room centre along the diagonal and overlaps the straight walls).</summary>
        static void Octagonise(Transform geo, float w, float h)
        {
            float a = w / 2f;                                   // apothem
            float side = 2f * a * Mathf.Tan(22.5f * Mathf.Deg2Rad);   // 0.4142 w
            float cornerLen = side + 1.4f;                      // the ends bury themselves in the straight walls
            float d = a / 1.41421f;                             // centre offset along each axis
            Block(geo, "Corner_NE", new Vector3(d, h / 2f, d), new Vector3(cornerLen, h, 0.5f), MWall, Quaternion.Euler(0f, 45f, 0f));
            Block(geo, "Corner_NW", new Vector3(-d, h / 2f, d), new Vector3(cornerLen, h, 0.5f), MWall, Quaternion.Euler(0f, -45f, 0f));
            Block(geo, "Corner_SE", new Vector3(d, h / 2f, -d), new Vector3(cornerLen, h, 0.5f), MWall, Quaternion.Euler(0f, -45f, 0f));
            Block(geo, "Corner_SW", new Vector3(-d, h / 2f, -d), new Vector3(cornerLen, h, 0.5f), MWall, Quaternion.Euler(0f, 45f, 0f));
        }

        /// <summary>A wall across the room at z with a 3 x 3.5 opening at x = openingX whose sill is at sillY.</summary>
        static void DoorwayWall(Transform geo, string name, float z, float w, float h, float openingX, float sillY)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(geo, false);
            Transform t = wall.transform;
            float leftW = (openingX - 1.5f) + w / 2f;
            float rightW = w / 2f - (openingX + 1.5f);
            if (leftW > 0.01f) Block(t, "Left", new Vector3(-w / 2f + leftW / 2f, h / 2f, z), new Vector3(leftW, h, 0.5f), MWall);
            if (rightW > 0.01f) Block(t, "Right", new Vector3(w / 2f - rightW / 2f, h / 2f, z), new Vector3(rightW, h, 0.5f), MWall);
            float top = sillY + 3.5f;
            if (h - top > 0.01f) Block(t, "Lintel", new Vector3(openingX, (top + h) / 2f, z), new Vector3(3f, h - top, 0.5f), MWall);
            if (sillY > 0.01f) Block(t, "Sill", new Vector3(openingX, sillY / 2f, z), new Vector3(3f, sillY, 0.5f), MWall);
        }

        // ------------------------------------------------------------------ kit helpers
        static GameObject Kit(string prefabPath, Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (source == null) { Debug.LogError("[RoomsV3] missing prefab " + prefabPath); return new GameObject(name); }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            go.name = name;
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return go;
        }

        static PressurePlate Plate(Transform play, string name, Vector3 pos, float threshold, bool latching, bool metalOnly)
        {
            GameObject go = Kit(PlatePrefab, play, name, pos, Quaternion.identity);
            PressurePlate plate = go.GetComponent<PressurePlate>();
            SetFloat(plate, "massThreshold", threshold);
            SetBool(plate, "latching", latching);
            SetBool(plate, "metalOnly", metalOnly);
            return plate;
        }

        static ImpactLever Lever(Transform play, string name, Vector3 pos, Quaternion rot)
        {
            GameObject go = Kit(LeverPrefab, play, name, pos, rot);
            ImpactLever lever = go.GetComponent<ImpactLever>();
            SetBool(lever, "latching", true);
            return lever;
        }

        static Door DoorAt(Transform play, string prefab, string name, Vector3 pos, Quaternion rot)
        {
            GameObject go = Kit(prefab, play, name, pos, rot);
            return go.GetComponent<Door>();
        }

        /// <summary>The exit gate on the south doorway with one condition.</summary>
        static Door GateSouth(GameObject room, DoorCondition.Mode mode, PressurePlate plate, ImpactLever lever, RoomVolume volume, List<PressurePlate> plates, float sillY, float exitX = 0f, Circuit circuit = null)
        {
            Transform doorways = room.transform.Find("Doorways");
            Door gate = DoorAt(doorways, DoorPrefab, "Gate_South", new Vector3(exitX, sillY, -RoomLength(room) / 2f), Quaternion.identity);
            DoorCondition c = gate.Condition;
            SetInt(c, "mode", (int)mode);
            if (plate != null) SetRef(c, "plate", plate);
            if (lever != null) SetRef(c, "lever", lever);
            if (volume != null) SetRef(c, "room", volume);
            if (plates != null) SetArray(c, "plates", plates);
            if (circuit != null) SetRef(c, "circuit", circuit);
            Transform south = doorways.Find("Doorway_South");
            if (south != null) SetRef(south.GetComponent<MagicDoor>(), "gate", gate);
            return gate;
        }

        static float RoomLength(GameObject room)
        {
            Transform floor = room.transform.Find("Geometry/Floor");
            if (floor != null) return floor.localScale.z;
            Transform south = room.transform.Find("Doorways/Doorway_South");
            return south != null ? -2f * south.localPosition.z : 24f;
        }

        /// <summary>Two wooden uprights 3.6 m apart (blades stick in them) and a crossbar, centred at pos, facing yaw.</summary>
        static void Frame(Transform play, string name, Vector3 pos, float yaw, float height)
        {
            GameObject frame = new GameObject(name);
            frame.transform.SetParent(play, false);
            frame.transform.localPosition = pos;
            frame.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Wood(frame.transform, "Upright_A", new Vector3(-1.8f, height / 2f, 0f), new Vector3(0.9f, height, 0.9f));
            Wood(frame.transform, "Upright_B", new Vector3(1.8f, height / 2f, 0f), new Vector3(0.9f, height, 0.9f));
            Wood(frame.transform, "Crossbar", new Vector3(0f, height + 0.25f, 0f), new Vector3(4.5f, 0.5f, 0.5f));
        }

        static void Wood(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            GameObject go = Kit(WoodPrefab, parent, name, pos, Quaternion.identity);
            go.transform.localScale = scale;
            go.layer = LayerWorld;
            foreach (Transform c in go.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = LayerWorld;
        }

        static void RoutePoints(Transform route, params Vector3[] points)
        {
            for (int i = route.childCount - 1; i >= 0; i--) Object.DestroyImmediate(route.GetChild(i).gameObject);
            List<Transform> list = new List<Transform>();
            for (int i = 0; i < points.Length; i++)
            {
                GameObject p = new GameObject("P" + (i + 1));
                p.transform.SetParent(route, false);
                p.transform.localPosition = points[i];
                list.Add(p.transform);
            }
            SetArray(route.GetComponent<PatrolRoute>(), "points", list);
        }

        /// <summary>Vertical bars on a door panel so it reads as a portcullis / cage gate.</summary>
/// <summary>Vertical bars on a door panel so it reads as a portcullis / cage gate. side = which face of the
        /// panel the bars stand proud of, in the panel's local Z (-0.6 = its -Z face).</summary>
        static void Lattice(Transform panel, float side = -0.6f)
        {
            if (panel == null) return;
            Vector3 s = panel.localScale;
            for (int i = 0; i < 7; i++)
            {
                float x = -0.42f + i * 0.14f;
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Bar_" + i;
                Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.transform.SetParent(panel, false);
                bar.transform.localPosition = new Vector3(x, 0f, side);
                bar.transform.localScale = new Vector3(0.05f, 1f, 0.3f);
                Paint(bar, MMetal);
            }
            for (int i = 0; i < 3; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Rail_" + i;
                Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.transform.SetParent(panel, false);
                bar.transform.localPosition = new Vector3(0f, -0.4f + i * 0.4f, side * 0.9f);   // a hair behind the bars: no shared face (round 29c)
                bar.transform.localScale = new Vector3(1f, 0.04f, 0.3f);
                Paint(bar, MMetal);
            }
            // The slab stays as the collider (and navmesh obstacle / bell); the bars are what you see. Its renderer is REMOVED,
            // not just disabled: the room culling once force-enabled every renderer and painted the slab over the bars.
            MeshRenderer r = panel.GetComponent<MeshRenderer>();
            if (r != null) Object.DestroyImmediate(r);
            MeshFilter f = panel.GetComponent<MeshFilter>();
            if (f != null) Object.DestroyImmediate(f);
        }

/// <summary>A chain as ONE dark cylinder from A to B (per-link objects were 100 lines of YAML each).</summary>
        static void ChainRun(Transform geo, string name, Vector3 from, Vector3 to)
        {
            Cord(geo, name, from, to, 0.22f, MMetal);
        }

        static void Cord(Transform geo, string name, Vector3 from, Vector3 to, float thickness, string material)
        {
            Vector3 d = to - from;
            GameObject cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cord.name = name;
            Object.DestroyImmediate(cord.GetComponent<Collider>());
            cord.transform.SetParent(geo, false);
            cord.transform.localPosition = from + d / 2f;
            cord.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            cord.transform.localScale = new Vector3(thickness, d.magnitude / 2f, thickness);
            cord.layer = LayerWorld;
            Paint(cord, material);
            GameObjectUtility.SetStaticEditorFlags(cord, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
        }

        /// <summary>One more straight run of a Rope: an unscaled object carrying a metric capsule (so the radius is in
        /// metres and never degenerates) and a RopeSegment that forwards hits to the rope, with the visible cord as a thin
        /// child cylinder. Under Gameplay, not static: the Rope switches it off when it is cut. from/to are room-local.</summary>
        static RopeSegment RopeCord(Transform play, Rope rope, string name, Vector3 from, Vector3 to, float thickness, float radius, string material)
        {
            Vector3 d = to - from;
            GameObject seg = new GameObject(name);
            seg.transform.SetParent(play, false);
            seg.transform.localPosition = from;
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);   // local +Y runs along the cord
            seg.layer = LayerWorld;
            CapsuleCollider capsule = seg.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = d.magnitude;                               // caps included: the capsule ends exactly at from and to
            capsule.center = new Vector3(0f, d.magnitude / 2f, 0f);
            capsule.excludeLayers = 1 << SceneValidator.LayerSoul;      // a rope never blocks a soul
            RopeSegment segment = seg.AddComponent<RopeSegment>();
            SetRef(segment, "rope", rope);
            GameObject cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cord.name = "Cord";
            Object.DestroyImmediate(cord.GetComponent<Collider>());
            cord.transform.SetParent(seg.transform, false);
            cord.transform.localPosition = new Vector3(0f, d.magnitude / 2f, 0f);
            cord.transform.localScale = new Vector3(thickness, d.magnitude / 2f, thickness);
            cord.layer = LayerWorld;
            Paint(cord, material);
            return segment;
        }

        /// <summary>A run of magical conduit: a 0.22 m rail on M_MagicFlow, static, no collider (nothing wedges against it).
        /// Each end runs HALF A RAIL PAST its point: two rails meeting at a corner close it, and a rail into a break's attach
        /// point, a door's feed strip or a wall buries its end. No gaps. Returns its renderer so a Circuit can light it.</summary>
        static Renderer Conduit(Transform geo, string name, Vector3 from, Vector3 to)
        {
            const float half = 0.09f;   // past each point; SHORTER than any rail's half-thickness (0.10..0.12), so an end face is always buried in the rail it meets
            Vector3 n = (to - from).normalized;
            Vector3 a = from - n * half, b = to + n * half;
            Vector3 up = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
            // The cross-section depends on the rail's axis (x 0.24, z 0.23, y 0.20; a break's stubs are 0.22), so where two rails
            // cross or one ends inside another no two faces share a plane: coplanar faces flicker (round 29c, the validator's
            // coplanar check). The MagicDoor prefab's wire pieces follow the same rule.
            float thick = Mathf.Abs(n.y) > 0.9f ? 0.20f : (Mathf.Abs(n.x) >= Mathf.Abs(n.z) ? 0.24f : 0.23f);
            GameObject railGo = Block(geo, name, (a + b) / 2f, new Vector3(thick, thick, (b - a).magnitude), MMagicFlow, Quaternion.LookRotation(n, up), true);
            Collider c = railGo.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            return railGo.GetComponent<Renderer>();
        }

        /// <summary>A break in a conduit: the CircuitBridge kit piece with its stubs set `gap` apart (1.05 takes a Sword, 0.5 a Dagger).
        /// yaw 0 lies along X (A west, B east), 90 along Z (A north, B south), -90 along Z (A south, B north); A is the fed
        /// side. pos is the floor point under the cradle's centre; 0.3 off a wall face puts the cradle flush to that wall with its
        /// stubs on the wall's rail line. Its Attach_A / Attach_B points (the stubs' outer faces, CircuitBridge.AttachA / AttachB)
        /// are where the conduits run to and from.</summary>
        static CircuitBridge BridgeAt(Transform play, string name, Vector3 pos, float yaw, float gap)
        {
            GameObject go = Kit(CircuitBridgePrefab, play, name, pos, Quaternion.Euler(0f, yaw, 0f));
            Transform a = go.transform.Find("StubA"), b = go.transform.Find("StubB");
            if (a != null) a.localPosition = new Vector3(-gap / 2f, a.localPosition.y, 0f);
            if (b != null) b.localPosition = new Vector3(gap / 2f, b.localPosition.y, 0f);
            float len = gap + 0.65f;
            foreach (string nm in new[] { "Base", "Rail_N", "Rail_S" }) { Transform t = go.transform.Find(nm); if (t != null) t.localScale = new Vector3(nm == "Base" ? len : len - 0.02f, t.localScale.y, t.localScale.z); }   // rails 2 cm shorter: no shared end face
            // The side rails sit a centimetre in from the base's edges too, so their outer faces do not lie in the base's sides.
            foreach (string nm in new[] { "Rail_N", "Rail_S" }) { Transform t = go.transform.Find(nm); if (t != null) { Vector3 lp = t.localPosition; t.localPosition = new Vector3(lp.x, lp.y, Mathf.Sign(lp.z) * Mathf.Max(0f, Mathf.Abs(lp.z) - 0.01f)); } }
            BoxCollider trig = go.GetComponent<BoxCollider>();
            if (trig != null) trig.size = new Vector3(gap + 0.85f, trig.size.y, trig.size.z);
            Transform sparks = go.transform.Find("Sparks");
            if (sparks != null) sparks.localPosition = new Vector3(-gap / 2f + 0.08f, sparks.localPosition.y, 0f);
            Transform fill = go.transform.Find("Fill");
            if (fill != null) fill.localScale = new Vector3(gap, fill.localScale.y, fill.localScale.z);
            Transform attachA = go.transform.Find("Attach_A"), attachB = go.transform.Find("Attach_B");
            if (attachA != null) attachA.localPosition = new Vector3(-gap / 2f - 0.11f, attachA.localPosition.y, 0f);
            if (attachB != null) attachB.localPosition = new Vector3(gap / 2f + 0.11f, attachB.localPosition.y, 0f);
            return go.GetComponent<CircuitBridge>();
        }

        /// <summary>The Circuit's stretches: a nested array (Run.pieces) the generic SetArray cannot reach.</summary>
        static void SetSegments(Circuit circuit, List<List<Renderer>> runs)
        {
            SerializedObject so = new SerializedObject(circuit);
            SerializedProperty p = so.FindProperty("segments");
            p.arraySize = runs.Count;
            for (int i = 0; i < runs.Count; i++)
            {
                SerializedProperty pieces = p.GetArrayElementAtIndex(i).FindPropertyRelative("pieces");
                pieces.arraySize = runs[i].Count;
                for (int j = 0; j < runs[i].Count; j++) pieces.GetArrayElementAtIndex(j).objectReferenceValue = runs[i][j];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A goblin stair: `count` solid steps from `foot` (the ground point at the bottom-front edge) climbing along `dir`,
        /// each `rise` higher and `tread` deeper, `width` across. Plain boxes: nothing wedges.</summary>
        static void Stair(Transform geo, string name, Vector3 foot, Vector3 dir, int count, float rise, float tread, float width)
        {
            GameObject stair = new GameObject(name);
            stair.transform.SetParent(geo, false);
            bool alongZ = Mathf.Abs(dir.z) > Mathf.Abs(dir.x);
            for (int k = 1; k <= count; k++)
            {
                float h = k * rise;
                Vector3 centre = foot + dir.normalized * ((k - 0.5f) * tread) + Vector3.up * (h / 2f);
                // Every step is a full-height box, so neighbours' side faces would lie in the same plane and flicker: even
                // steps are 2 cm narrower (round 29c).
                float w = k % 2 == 0 ? width - 0.02f : width;
                Vector3 scale = alongZ ? new Vector3(w, h, tread) : new Vector3(tread, h, w);
                Block(stair.transform, "Step_" + k, centre, scale, MLedge);
            }
        }

        /// <summary>A decoration from the Deco kit (a plain box with a plain collider, static, art swapped later per prefab).</summary>
        static GameObject Deco(Transform geo, string prefab, string name, Vector3 pos, float yaw)
        {
            return Kit(prefab, geo, name, pos, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>A Deco piece that RIDES a moving kit piece (cargo on the Well Room's lift): parented to it and stripped of its
        /// static flags, so it is neither batched in place nor baked into the NavMesh.</summary>
        static GameObject DecoMoving(Transform mover, string prefab, string name, Vector3 pos, float yaw)
        {
            GameObject go = Kit(prefab, mover, name, pos, Quaternion.Euler(0f, yaw, 0f));
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            return go;
        }

        /// <summary>The generator: a crystal core floor to ceiling in a four-post cage, two halos, a dark base. Lives on M_MagicFlow.</summary>
        static void Generator(Transform geo, Vector3 at, float h)
        {
            GameObject g = new GameObject("Generator");
            g.transform.SetParent(geo, false);
            g.transform.localPosition = at;
            Cylinder(g.transform, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(3.2f, 0.3f, 3.2f), MAnvil, Quaternion.identity);
            Cylinder(g.transform, "Core", new Vector3(0f, h / 2f, 0f), new Vector3(1.6f, h / 2f, 1.6f), MMagicFlow, Quaternion.identity);
            Cylinder(g.transform, "Halo_Low", new Vector3(0f, h * 0.55f, 0f), new Vector3(3.4f, 0.06f, 3.4f), MMagicFlow, Quaternion.identity);
            Cylinder(g.transform, "Halo_High", new Vector3(0f, h * 0.8f, 0f), new Vector3(2.6f, 0.06f, 2.6f), MMagicFlow, Quaternion.identity);
            float r = 1.7f;
            Block(g.transform, "Post_NE", new Vector3(r, h / 2f, r), new Vector3(0.4f, h, 0.4f), MMetal);
            Block(g.transform, "Post_NW", new Vector3(-r, h / 2f, r), new Vector3(0.4f, h, 0.4f), MMetal);
            Block(g.transform, "Post_SE", new Vector3(r, h / 2f, -r), new Vector3(0.4f, h, 0.4f), MMetal);
            Block(g.transform, "Post_SW", new Vector3(-r, h / 2f, -r), new Vector3(0.4f, h, 0.4f), MMetal);
        }

        static void Wire(Transform geo, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            Block(geo, "Wire", from + d / 2f, new Vector3(0.3f, 0.1f, d.magnitude), MMetal, Quaternion.LookRotation(d.normalized, Vector3.up), true);
        }

        /// <summary>The exit doorway as a DEAD PORTAL (owner, round 29): no sliding slab. The gate's panel keeps its collider
        /// (a dead portal is solid to weapons and souls) and loses its renderer; the doorway's plane stays in view, dark, and
        /// comes alive when whatever drives it is live. Returns the doorway's magic renderers (the plane and its wire) for the
        /// room's PowerLine or Circuit to drive. Call after GateSouth.</summary>
        static List<Renderer> DoorMagic(GameObject room)
        {
            List<Renderer> pieces = new List<Renderer>();
            Transform doorways = room.transform.Find("Doorways");
            Transform panel = doorways != null ? doorways.Find("Gate_South/Panel") : null;
            if (panel != null)
            {
                MeshRenderer r = panel.GetComponent<MeshRenderer>();
                if (r != null) Object.DestroyImmediate(r);
                MeshFilter f = panel.GetComponent<MeshFilter>();
                if (f != null) Object.DestroyImmediate(f);
            }
            Transform south = doorways != null ? doorways.Find("Doorway_South") : null;
            if (south == null) return pieces;
            SetBool(south.GetComponent<MagicDoor>(), "showPlaneWhenShut", true);
            foreach (string nm in DoorMagicPieces)
            {
                Transform t = south.Find(nm);
                if (t != null && t.GetComponent<Renderer>() != null) pieces.Add(t.GetComponent<Renderer>());
            }
            return pieces;
        }

        /// <summary>A SWITCH IS A WIRE (round 29): a PowerLine under Gameplay whose source is a plate, a lever, a door, a circuit or a
        /// junction, lighting `rails` (conduit pieces and whatever they feed) while the source is on.</summary>
        static PowerLine Line(Transform play, string name, Object source, List<Renderer> rails)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(play, false);
            PowerLine line = go.AddComponent<PowerLine>();
            if (source is PressurePlate) SetRef(line, "plate", source);
            else if (source is ImpactLever) SetRef(line, "lever", source);
            else if (source is Door) SetRef(line, "door", source);
            else if (source is Circuit) SetRef(line, "circuit", source);
            else if (source is PowerJunction) SetRef(line, "junction", source);
            SetArray(line, "rails", rails);
            return line;
        }

        /// <summary>Where lines meet on a wall: a 0.6 m M_MagicFlow box (no collider) at pos and a PowerJunction under Gameplay that is
        /// live when any input is (OR).</summary>
        static PowerJunction Junction(Transform play, Transform geo, string name, Vector3 pos, List<PowerLine> inputs)
        {
            GameObject box = Block(geo, name + "_Box", pos, new Vector3(0.6f, 0.6f, 0.6f), MMagicFlow);
            Collider c = box.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            GameObject go = new GameObject(name);
            go.transform.SetParent(play, false);
            go.transform.localPosition = pos;
            PowerJunction junction = go.AddComponent<PowerJunction>();
            SetArray(junction, "inputs", inputs);
            SetRef(junction, "box", box.GetComponent<Renderer>());
            return junction;
        }

        /// <summary>A door that is open while `plate` is held OR `lever` is on (a portcullis and its counterweight).</summary>
        static void HeldOrPinned(Door door, PressurePlate plate, ImpactLever lever)
        {
            DoorCondition c = door.Condition;
            SetInt(c, "mode", (int)DoorCondition.Mode.PlateHeld);
            SetRef(c, "plate", plate);
            GameObject orGo = new GameObject("OrLeverPinned");
            orGo.transform.SetParent(door.transform, false);
            DoorCondition cb = orGo.AddComponent<DoorCondition>();
            SetInt(cb, "mode", (int)DoorCondition.Mode.LeverOn);
            SetRef(cb, "lever", lever);
            SetRef(c, "orCondition", cb);
        }

        static void Profile(GameObject room, string id, string display, string summary, int tier)
        {
            PuzzleRoomProfile profile = room.GetComponent<PuzzleRoomProfile>();
            if (profile == null) profile = room.AddComponent<PuzzleRoomProfile>();
            SetString(profile, "puzzleId", id);
            SetString(profile, "displayName", display);
            SetString(profile, "summary", summary);
            SetInt(profile, "minimumPlayers", 1);
            SetInt(profile, "optimalPlayers", 2);
            SetInt(profile, "maximumPlayers", 8);
            SetFloat(profile, "expectedSolveSeconds", 90f);
            SetInt(profile, "difficultyTier", tier);
        }

        // ------------------------------------------------------------------ primitives
        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 scale, string material) { return Block(parent, name, pos, scale, material, Quaternion.identity, true); }
        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 scale, string material, Quaternion rot) { return Block(parent, name, pos, scale, material, rot, true); }
        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 scale, string material, Quaternion rot, bool isStatic)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.layer = LayerWorld;
            Paint(go, material);
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, string material, Quaternion rot)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.layer = LayerWorld;
            Paint(go, material);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        static void Paint(GameObject go, string materialPath)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Renderer r = go.GetComponent<Renderer>();
            if (m != null && r != null) r.sharedMaterial = m;
        }

        // ------------------------------------------------------------------ serialized property helpers
        static List<T> ComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> list = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }
        static GameObject FindRoot(Scene scene, string name) { foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == name) return go; return null; }
        static void DestroyChild(Transform parent, string name) { Transform child = parent != null ? parent.Find(name) : null; if (child != null) Object.DestroyImmediate(child.gameObject); }
        static string SafeName(string value) { return value.Replace(" ", ""); }
        static void SaveRoom(GameObject room, string path) { PrefabUtility.SaveAsPrefabAsset(room, path); Object.DestroyImmediate(room); }
        static void SetRef(Object target, string property, Object value) { SetProperty(target, property, p => p.objectReferenceValue = value); }
        static void SetString(Object target, string property, string value) { SetProperty(target, property, p => p.stringValue = value); }
        static void SetInt(Object target, string property, int value) { SetProperty(target, property, p => { if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value; else p.intValue = value; }); }
        static void SetFloat(Object target, string property, float value) { SetProperty(target, property, p => p.floatValue = value); }
        static void SetVector3(Object target, string property, Vector3 value) { SetProperty(target, property, p => p.vector3Value = value); }
        static void SetBool(Object target, string property, bool value) { SetProperty(target, property, p => p.boolValue = value); }
        static void SetProperty(Object target, string name, System.Action<SerializedProperty> write)
        {
            if (target == null) { Debug.LogError("[RoomsV3] missing target for " + name); return; }
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(name);
            if (property == null) { Debug.LogError("[RoomsV3] " + target.GetType().Name + " has no property " + name); return; }
            write(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void SetArray<T>(Object target, string property, List<T> values) where T : Object
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError("[RoomsV3] " + target.GetType().Name + " has no " + property); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
