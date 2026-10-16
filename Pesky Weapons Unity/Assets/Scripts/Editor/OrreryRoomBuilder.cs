using System.Collections.Generic;
using Pesky.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pesky.Editor
{
    /// <summary>
    /// THE ORRERY (docs/level-design/orrery-room.md): the goblins' star-clock. A 32 m octagon, 16 m high, with a
    /// turning brass orrery on a dais in the middle and three star levers on perches at three heights. Every lever
    /// lit opens the exit (Door on DoorCondition.Mode.LeversOn).
    ///   West, 2.5 m: the scribe's loft, up a goblin stair.          (anyone)
    ///   East, 4.5 m: the brass shelf, a LaunchPad on the floor.     (anyone)
    ///   South, 7.5 m: the star gallery over the exit, a LaunchPad on the dais, inside the sweep of the orrery's arm.
    /// Built from primitives and the kit, saved as a prefab variant of the two-door room, and dropped into its own test
    /// scene (a copy of BatTest). Change a number, rerun "Pesky/Rooms/Build The Orrery". Local axes: +Z = north = entry.
    /// </summary>
    public static class OrreryRoomBuilder
    {
        const string PrefabPath = "Assets/Prefabs/Rooms/Labyrinth/RoomV6_Orrery.prefab";
        const string TestScene = "Assets/Scenes/Dev/OrreryTest.unity";
        const string TemplateScene = "Assets/Scenes/Dev/BatTest.unity";
        const string TwoDoorPrefab = "Assets/Prefabs/Rooms/Labyrinth/LabyrinthRoom_TwoDoor.prefab";
        const string K = "Assets/Prefabs/Kit/";
        const string DoorPrefab = K + "Doors/Door.prefab";
        const string LeverPrefab = K + "Movers/ImpactLever.prefab";
        const string PadPrefab = K + "Movers/LaunchPad.prefab";
        const string SpinnerPrefab = K + "Movers/Spinner.prefab";
        const string M = "Assets/Materials/";
        const string MFloor = M + "M_Floor.mat", MWall = M + "M_Wall.mat", MAnvil = M + "M_Anvil.mat", MMetal = M + "M_Metal.mat";
        const string MWood = M + "M_Wood.mat", MLedge = M + "M_Ledge.mat", MFlow = M + "M_MagicFlow.mat", MKey = M + "M_Key.mat";
        const string MHazard = M + "M_Hazard.mat", MSilver = M + "M_Silver.mat";
        const int LayerWorld = 8;

        // The room
        const float W = 32f, H = 16f;
        // The orrery
        const float DaisR = 4.5f, DaisH = 1f, StepR = 5.4f, StepH = 0.5f, ArmReach = 4.95f, PeriodSeconds = 10f;
        // The perches: top heights and their faces toward the room
        const float LoftY = 2.5f, LoftFace = -12.5f;            // west, x -16..-12.5, z -4..4
        const float ShelfY = 4.5f, ShelfFace = 12.5f;          // east, x 12.5..16, z -3..5
        const float GalleryY = 7.5f, GalleryFace = -12f;       // south, x -6..6, z -16..-12, over the exit

        [MenuItem("Pesky/Rooms/Build The Orrery")]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("[Orrery] the open scene has UNSAVED changes. Save or discard them first; nothing was built.");
                return;
            }
            BuildPrefab();
            BuildTestScene();
            Debug.Log("[Orrery] built " + PrefabPath + " and " + TestScene + ".");
        }

        // ------------------------------------------------------------------ the room
        static void BuildPrefab()
        {
            Transform geo, play, fixtures;
            GameObject room = Shell("RoomV6_Orrery", out geo, out play, out fixtures);
            Octagonise(geo);

            Orrery(geo, play);
            ImpactLever loft = Loft(geo, play, fixtures);
            ImpactLever shelf = Shelf(geo, play);
            ImpactLever gallery = Gallery(geo, play);
            FloorChart(geo);
            Architecture(geo);
            Ceiling(geo);
            Dressing(geo);
            Lighting(fixtures);

            Door gate = ExitGate(room, new List<ImpactLever> { loft, shelf, gallery });
            Profile(room);
            PrefabUtility.SaveAsPrefabAsset(room, PrefabPath);
            Object.DestroyImmediate(room);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The dais, the turning arm (a Spinner whose rotor also carries the planets) and the sun.</summary>
        static void Orrery(Transform geo, Transform play)
        {
            GameObject dais = new GameObject("Orrery");
            dais.transform.SetParent(geo, false);
            Transform d = dais.transform;
            Disc(d, "Step", new Vector3(0f, StepH / 2f, 0f), StepR, StepH, MAnvil, true);
            Disc(d, "Dais", new Vector3(0f, DaisH / 2f, 0f), DaisR, DaisH, MAnvil, true);
            Disc(d, "Dais_Rim", new Vector3(0f, DaisH - 0.06f, 0f), DaisR + 0.06f, 0.1f, MKey, false);
            Disc(d, "Step_Rim", new Vector3(0f, StepH - 0.05f, 0f), StepR + 0.05f, 0.08f, MMetal, false);
            Disc(d, "Dais_Inlay", new Vector3(0f, DaisH + 0.004f, 0f), 3.4f, 0.006f, MFlow, false);
            Disc(d, "Dais_InlayCut", new Vector3(0f, DaisH + 0.008f, 0f), 3.25f, 0.006f, MAnvil, false);

            // The arm: the Spinner kit piece on the dais, its arm stretched to the dais edge and a touch past it, so a body
            // left lying on the dais is swept off. Ten seconds a turn.
            GameObject spinner = Kit(SpinnerPrefab, play, "Orrery_Spinner", new Vector3(0f, DaisH, 0f), Quaternion.identity);
            Transform rotor = spinner.transform.Find("Rotor");
            ClockSpinner cs = rotor.GetComponent<ClockSpinner>();
            SetFloat(cs, "periodSeconds", PeriodSeconds);
            // Raised to knee height (0.39-0.71 m over the dais): the arm clears the dais pad's base, so it is a hurdle to time a
            // hop past, not a floor sweeper; standing and hopping bodies are knocked, a body lying flat is left alone.
            Transform arm = rotor.Find("Arm");
            arm.localPosition = new Vector3(0f, 0.45f, 0f);
            arm.localScale = new Vector3(ArmReach * 2f, arm.localScale.y, arm.localScale.z);
            Paint(arm.gameObject, MAnvil);
            foreach (Transform c in rotor) if (c.name == "Cap") { c.localPosition = new Vector3(Mathf.Sign(c.localPosition.x) * (ArmReach + 0.09f), 0.5f, 0f); c.localScale = new Vector3(c.localScale.x, 0.5f, c.localScale.z); Paint(c.gameObject, MKey); }
            Transform hub = rotor.Find("Hub");
            hub.localScale = new Vector3(1.1f, hub.localScale.y, 1.1f);
            Paint(hub.gameObject, MKey);
            Transform ped = spinner.transform.Find("Pedestal");
            ped.localScale = new Vector3(1.6f, ped.localScale.y, 1.6f);

            // The machine above the arm turns with it: an axle, three planets on their own arms, the sun at the top.
            // Nothing up here has a collider (a weapon thrown to the gallery flies through the orbits, not into them).
            float hubTop = 1.0f;
            Visual(rotor, "Axle", PrimitiveType.Cylinder, new Vector3(0f, (hubTop + 10.3f) / 2f, 0f), Vector3.zero, new Vector3(0.36f, (10.3f - hubTop) / 2f, 0.36f), MKey);
            Visual(rotor, "Collar_Low", PrimitiveType.Cylinder, new Vector3(0f, 3.6f, 0f), Vector3.zero, new Vector3(0.7f, 0.12f, 0.7f), MAnvil);
            Visual(rotor, "Collar_Mid", PrimitiveType.Cylinder, new Vector3(0f, 6.1f, 0f), Vector3.zero, new Vector3(0.7f, 0.12f, 0.7f), MAnvil);
            Visual(rotor, "Collar_High", PrimitiveType.Cylinder, new Vector3(0f, 8.4f, 0f), Vector3.zero, new Vector3(0.6f, 0.12f, 0.6f), MAnvil);
            Planet(rotor, "Inner", 3.6f, 3.0f, 0f, 0.45f, M + "M_Rope.mat", false);        // a small grey world close to the axle
            Planet(rotor, "Blue", 6.1f, 8.0f, 130f, 0.95f, MLedge, true);         // the wide blue one, with a moon
            Planet(rotor, "Red", 8.4f, 5.4f, 245f, 0.7f, MHazard, false);         // the red one, high and quick-looking
            Visual(rotor, "Sun", PrimitiveType.Sphere, new Vector3(0f, 11.4f, 0f), Vector3.zero, Vector3.one * 2.4f, MFlow);
            Visual(rotor, "Sun_Halo", PrimitiveType.Cylinder, new Vector3(0f, 11.4f, 0f), new Vector3(8f, 0f, 0f), new Vector3(4.2f, 0.03f, 4.2f), MFlow);
            Visual(rotor, "Sun_Ring", PrimitiveType.Cylinder, new Vector3(0f, 11.4f, 0f), new Vector3(-14f, 0f, 12f), new Vector3(3.3f, 0.04f, 3.3f), MKey);
            // It hangs from the roof on one chain (static: the chain does not turn).
            Cord(geo, "Sun_Chain", new Vector3(0f, 12.55f, 0f), new Vector3(0f, H, 0f), 0.18f, MMetal);
            Disc(geo, "Roof_Boss", new Vector3(0f, H - 0.1f, 0f), 1.1f, 0.2f, MKey, false);
        }

        /// <summary>One planet: a thin rod out from the axle at `height`, the world at its end, a counterweight on the far side.</summary>
        static void Planet(Transform rotor, string name, float height, float radius, float yaw, float size, string material, bool moon)
        {
            GameObject armGo = new GameObject("Planet_" + name);
            armGo.transform.SetParent(rotor, false);
            armGo.transform.localPosition = new Vector3(0f, height, 0f);
            armGo.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform a = armGo.transform;
            Visual(a, "Rod", PrimitiveType.Cube, new Vector3(radius / 2f - 0.6f, 0f, 0f), Vector3.zero, new Vector3(radius + 1.2f, 0.1f, 0.1f), MKey);
            Visual(a, "World", PrimitiveType.Sphere, new Vector3(radius, 0f, 0f), Vector3.zero, Vector3.one * size * 2f, material);
            Visual(a, "Counterweight", PrimitiveType.Sphere, new Vector3(-1.2f, 0f, 0f), Vector3.zero, Vector3.one * 0.32f, MAnvil);
            if (!moon) return;
            Visual(a, "MoonRod", PrimitiveType.Cube, new Vector3(radius, 0f, 0.9f), new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.06f, 1.8f), MKey);
            Visual(a, "Moon", PrimitiveType.Sphere, new Vector3(radius, 0.05f, 1.8f), Vector3.zero, Vector3.one * 0.36f, M + "M_Sign.mat");
        }

        /// <summary>West, 2.5 m: the scribe's loft. A stair from the room, a desk where the goblins chart the stars, the first lever.</summary>
        static ImpactLever Loft(Transform geo, Transform play, Transform fixtures)
        {
            float x0 = -W / 2f, depth = LoftFace - x0;   // 3.5 m deep
            Block(geo, "Loft", new Vector3(x0 + depth / 2f, LoftY - 0.25f, 0f), new Vector3(depth, 0.5f, 8f), MWood);
            Block(geo, "Loft_Lip", new Vector3(LoftFace - 0.06f, LoftY + 0.06f, 0f), new Vector3(0.12f, 0.12f, 7.98f), MKey);
            // Posts stand UNDER the platform, flush with its edges: the outer face of each is the platform's edge.
            Post(geo, "Loft_Post_N", new Vector3(LoftFace - 0.25f, 0f, 3.75f), LoftY - 0.5f);
            Post(geo, "Loft_Post_S", new Vector3(LoftFace - 0.25f, 0f, -3.75f), LoftY - 0.5f);
            Block(geo, "Loft_Beam", new Vector3(LoftFace - 0.25f, LoftY - 0.76f, 0f), new Vector3(0.46f, 0.5f, 7.96f), MWood);   // inset from the posts' faces and tops
            Stair(geo, "Loft_Stair", new Vector3(-8.5f, 0f, -1f), Vector3.left, 5, 0.5f, 0.8f, 2.5f);

            ImpactLever lever = Lever(play, "StarLever_Loft", new Vector3(-13.6f, LoftY, -3.0f), Quaternion.Euler(0f, 90f, 0f));
            Deco(geo, "Deco_Table", "Loft_Desk", new Vector3(-15.45f, LoftY, 1.8f), 90f);   // the one prop on the loft: it says "this is where the astronomers work"
            return lever;
        }

        /// <summary>East, 4.5 m: the brass shelf, where the lenses are kept. A floor pad throws you up onto it.</summary>
        static ImpactLever Shelf(Transform geo, Transform play)
        {
            float x1 = W / 2f, depth = x1 - ShelfFace;
            Block(geo, "Shelf", new Vector3(ShelfFace + depth / 2f, ShelfY - 0.25f, 1f), new Vector3(depth, 0.5f, 8f), MWood);
            Block(geo, "Shelf_Lip", new Vector3(ShelfFace + 0.06f, ShelfY + 0.06f, 1f), new Vector3(0.12f, 0.12f, 7.98f), MKey);
            Post(geo, "Shelf_Post_N", new Vector3(ShelfFace + 0.25f, 0f, 4.75f), ShelfY - 0.5f);
            Post(geo, "Shelf_Post_S", new Vector3(ShelfFace + 0.25f, 0f, -2.75f), ShelfY - 0.5f);
            Block(geo, "Shelf_Beam", new Vector3(ShelfFace + 0.25f, ShelfY - 0.76f, 1f), new Vector3(0.46f, 0.5f, 7.96f), MWood);

            // up 15, out 5.5: a Sword thrown from the pad comes to rest about 1.4 m in from the shelf's face (play-tested)
            GameObject pad = Kit(PadPrefab, play, "Pad_Shelf", new Vector3(8.5f, 0f, 1f), Quaternion.Euler(0f, 90f, 0f));
            LaunchPad lp = pad.GetComponent<LaunchPad>();
            SetFloat(lp, "upSpeed", 15f);
            SetFloat(lp, "forwardSpeed", 5.5f);

            ImpactLever lever = Lever(play, "StarLever_Shelf", new Vector3(13.6f, ShelfY, -1.8f), Quaternion.Euler(0f, -90f, 0f));
            return lever;
        }

        /// <summary>South, 7.5 m: the star gallery over the exit, between two braziers. Only the dais pad reaches it, and the
        /// dais pad lies inside the sweep of the orrery's arm: step on between two passes.</summary>
        static ImpactLever Gallery(Transform geo, Transform play)
        {
            float z0 = -W / 2f, depth = GalleryFace - z0;   // 4 m deep
            Block(geo, "Gallery", new Vector3(0f, GalleryY - 0.25f, z0 + depth / 2f), new Vector3(12f, 0.5f, depth), MWood);
            Block(geo, "Gallery_Lip", new Vector3(0f, GalleryY + 0.06f, GalleryFace - 0.06f), new Vector3(11.98f, 0.12f, 0.12f), MKey);
            Post(geo, "Gallery_Post_W", new Vector3(-5.75f, 0f, GalleryFace - 0.25f), GalleryY - 0.5f);   // outer face on the platform's side edge (x +-6)
            Post(geo, "Gallery_Post_E", new Vector3(5.75f, 0f, GalleryFace - 0.25f), GalleryY - 0.5f);
            Block(geo, "Gallery_Beam", new Vector3(0f, GalleryY - 0.76f, GalleryFace - 0.25f), new Vector3(11.96f, 0.5f, 0.46f), MWood);

            // up 18, out 8.8 from the dais (1 m up): clears the gallery's face high and comes to rest about 1.5 m in (play-tested)
            GameObject pad = Kit(PadPrefab, play, "Pad_Gallery", new Vector3(0f, DaisH, -2.6f), Quaternion.Euler(0f, 180f, 0f));
            LaunchPad lp = pad.GetComponent<LaunchPad>();
            SetFloat(lp, "upSpeed", 18f);
            SetFloat(lp, "forwardSpeed", 8.8f);

            ImpactLever lever = Lever(play, "StarLever_Gallery", new Vector3(0f, GalleryY, -15.0f), Quaternion.identity);
            Deco(geo, "Deco_Brazier", "Gallery_Brazier_W", new Vector3(-5.65f, GalleryY, -15.65f), 0f);
            Deco(geo, "Deco_Brazier", "Gallery_Brazier_E", new Vector3(5.65f, GalleryY, -15.65f), 0f);
            return lever;
        }

        /// <summary>The floor is a star chart: two rings round the dais, and a lit line from each perch to the outer ring and
        /// from the dais to the exit, so the eye goes lever, ring, machine, door. Inlays only: no colliders.</summary>
        static void FloorChart(Transform geo)
        {
            GameObject chart = new GameObject("StarChart");
            chart.transform.SetParent(geo, false);
            Transform c = chart.transform;
            Ring(c, "Ring_Inner", 7.2f, 0.16f);
            Ring(c, "Ring_Outer", 10.6f, 0.22f);
            // perch lines: the foot of each perch to the outer ring
            Inlay(c, "Line_Loft", new Vector3(LoftFace + 0.05f, 0f, -3.75f), new Vector3(-9.92f, 0f, -3.75f));
            Inlay(c, "Line_Shelf", new Vector3(ShelfFace - 0.05f, 0f, -2.75f), new Vector3(10.24f, 0f, -2.75f));
            Inlay(c, "Line_Gallery_W", new Vector3(-5.75f, 0f, -11.95f), new Vector3(-5.75f, 0f, -8.9f));
            Inlay(c, "Line_Gallery_E", new Vector3(5.75f, 0f, -11.95f), new Vector3(5.75f, 0f, -8.9f));
            // the machine to the door: dais step to the threshold, then up both jambs and over the lintel
            Inlay(c, "Line_Exit", new Vector3(0f, 0f, -StepR - 0.05f), new Vector3(0f, 0f, -W / 2f + 0.05f));
            Rail(geo, "Jamb_W", new Vector3(-1.75f, 0f, -W / 2f + 0.3f), new Vector3(-1.75f, 3.75f, -W / 2f + 0.3f));
            Rail(geo, "Jamb_E", new Vector3(1.75f, 0f, -W / 2f + 0.3f), new Vector3(1.75f, 3.75f, -W / 2f + 0.3f));
            Rail(geo, "Lintel", new Vector3(-1.75f, 3.75f, -W / 2f + 0.3f), new Vector3(1.75f, 3.75f, -W / 2f + 0.3f));
            // and up the gallery posts to the lever's floor
            Rail(geo, "Post_Line_W", new Vector3(-5.75f, 0f, GalleryFace + 0.05f), new Vector3(-5.75f, GalleryY - 0.5f, GalleryFace + 0.05f));
            Rail(geo, "Post_Line_E", new Vector3(5.75f, 0f, GalleryFace + 0.05f), new Vector3(5.75f, GalleryY - 0.5f, GalleryFace + 0.05f));
            Rail(geo, "Loft_Line", new Vector3(LoftFace + 0.05f, 0f, -3.75f), new Vector3(LoftFace + 0.05f, LoftY - 0.5f, -3.75f));
            Rail(geo, "Shelf_Line", new Vector3(ShelfFace - 0.05f, 0f, -2.75f), new Vector3(ShelfFace - 0.05f, ShelfY - 0.5f, -2.75f));
        }

        /// <summary>Stone pilasters where the eight walls meet, and a brass band round the room at the shelf's height,
        /// broken by nothing: the room reads as one built drum, not eight boxes.</summary>
        static void Architecture(Transform geo)
        {
            GameObject g = new GameObject("Architecture");
            g.transform.SetParent(geo, false);
            Transform t = g.transform;
            float apothem = W / 2f, corner = apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad);
            float side = 2f * apothem * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            for (int k = 0; k < 8; k++)
            {
                float a = (22.5f + 45f * k) * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (corner - 0.2f);
                Block(t, "Pilaster_" + k, at + Vector3.up * (H / 2f), new Vector3(0.9f, H, 0.9f), MAnvil, Quaternion.Euler(0f, 22.5f + 45f * k, 0f));
                Block(t, "Pilaster_Base_" + k, at + Vector3.up * 0.4f, new Vector3(1.2f, 0.8f, 1.2f), MAnvil, Quaternion.Euler(0f, 22.5f + 45f * k, 0f));
                float b = 45f * k * Mathf.Deg2Rad;
                Vector3 face = new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)) * (apothem - 0.06f);
                GameObject band = Block(t, "Band_" + k, face + Vector3.up * 9.6f, new Vector3(side, 0.22f, 0.12f), MKey, Quaternion.Euler(0f, 45f * k, 0f));
                Object.DestroyImmediate(band.GetComponent<Collider>());
                GameObject plinth = Block(t, "Skirting_" + k, face + Vector3.up * 0.15f, new Vector3(side - 0.4f, 0.3f, 0.12f), MAnvil, Quaternion.Euler(0f, 45f * k, 0f));
                Object.DestroyImmediate(plinth.GetComponent<Collider>());
            }
        }

        /// <summary>The roof is the sky the machine copies: timber rafters from the walls to the boss the sun hangs from, a
        /// ring beam, and a few small constellations in lit points between them. Nothing up here has a collider.</summary>
        static void Ceiling(Transform geo)
        {
            GameObject g = new GameObject("Ceiling");
            g.transform.SetParent(geo, false);
            Transform t = g.transform;
            float y = H - 0.25f;
            for (int k = 0; k < 8; k++)
            {
                float a = 22.5f + 45f * k;
                float r0 = 1.0f, r1 = W / 2f / Mathf.Cos(22.5f * Mathf.Deg2Rad) - 0.6f;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                GameObject rafter = Block(t, "Rafter_" + k, dir * ((r0 + r1) / 2f) + Vector3.up * y, new Vector3(0.45f, 0.5f, r1 - r0), MWood, Quaternion.LookRotation(dir));
                Object.DestroyImmediate(rafter.GetComponent<Collider>());
            }
            for (int k = 0; k < 8; k++)
            {
                float a = 45f * k, r = 9f, len = 2f * r * Mathf.Tan(22.5f * Mathf.Deg2Rad) + 0.5f;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                // Even beams are 0.35 deep, odd ones 0.31 and 1 cm higher, so where two overlap at a corner no two faces share a plane.
                bool odd = (k & 1) == 1;
                GameObject ring = Block(t, "RingBeam_" + k, dir * r + Vector3.up * (y - (odd ? 0.34f : 0.35f)), new Vector3(len, odd ? 0.31f : 0.35f, odd ? 0.33f : 0.35f), MWood, Quaternion.Euler(0f, a, 0f));
                Object.DestroyImmediate(ring.GetComponent<Collider>());
            }
            // Constellations: each a short chain of stars (x, z on the roof), joined by faint lines.
            Vector2[][] figures =
            {
                new[] { new Vector2(-4f, 11f), new Vector2(-2.2f, 12.3f), new Vector2(-0.4f, 11.8f), new Vector2(1.1f, 13f) },          // the Ladle, over the entry
                new[] { new Vector2(10.5f, 2f), new Vector2(12f, -0.5f), new Vector2(11f, -3f), new Vector2(13f, -4.2f), new Vector2(12.4f, 1.2f) }, // the Anvil, over the shelf
                new[] { new Vector2(-11f, -2f), new Vector2(-12.8f, 0.5f), new Vector2(-11.6f, 3f) },                                     // the Hook, over the loft
                new[] { new Vector2(-3.5f, -11f), new Vector2(-1f, -12.6f), new Vector2(1.5f, -11.4f), new Vector2(3.6f, -12.8f) },        // the Blade, over the gallery
                new[] { new Vector2(6f, 7f), new Vector2(7.6f, 8.8f), new Vector2(9.4f, 7.4f) },
                new[] { new Vector2(-7.2f, -7.6f), new Vector2(-8.8f, -6f) },
            };
            int n = 0;
            foreach (Vector2[] fig in figures)
            {
                for (int i = 0; i < fig.Length; i++)
                {
                    float size = i == 0 ? 0.42f : 0.28f;
                    Static(Visual(t, "Star_" + n++, PrimitiveType.Cube, new Vector3(fig[i].x, H - 0.03f, fig[i].y), new Vector3(0f, 45f, 0f), new Vector3(size, 0.04f, size), MFlow));
                    if (i == 0) continue;
                    Vector3 from = new Vector3(fig[i - 1].x, H - 0.02f, fig[i - 1].y), to = new Vector3(fig[i].x, H - 0.02f, fig[i].y);
                    Vector3 d = to - from;
                    // Alternate lines sit 8 mm apart in height, so two that meet at a star do not share a plane.
                    Vector3 mid = (from + to) / 2f - Vector3.up * ((n % 2) * 0.008f);
                    Static(Visual(t, "Line_" + n, PrimitiveType.Cube, mid, Quaternion.LookRotation(d.normalized).eulerAngles, new Vector3(0.06f, 0.01f, d.magnitude), MKey));
                }
            }
        }

        /// <summary>What the goblins left lying about. Every piece flush to a wall, 1.5 m or more from anything else.</summary>
        static void Dressing(Transform geo)
        {
            // Nothing here is decoration for its own sake: the two banners mark the exit, so the eye finds the door from the
            // entry. The room's other props are the loft's desk (a reason for the perch) and the gallery's braziers (they
            // light the highest lever); everything else that was here was cut in review.
            GameObject g = new GameObject("Dressing");
            g.transform.SetParent(geo, false);
            Transform t = g.transform;
            Deco(t, "Deco_Banner", "Banner_Exit_W", new Vector3(-3.1f, 0f, -W / 2f), 180f);
            Deco(t, "Deco_Banner", "Banner_Exit_E", new Vector3(3.1f, 0f, -W / 2f), 180f);
        }

        /// <summary>Four torches where the eye needs them, and one violet light from the sun. Six lights at most per room.</summary>
        static void Lighting(Transform fixtures)
        {
            Vector3[] at = { new Vector3(-5f, 3.2f, W / 2f - 0.6f), new Vector3(0f, GalleryY + 2.4f, -W / 2f + 0.6f), new Vector3(-W / 2f + 0.6f, 5.2f, 2.6f), new Vector3(W / 2f - 0.6f, 2.6f, -4.6f) };
            string[] names = { "Torch_1", "Torch_2", "Torch_3", "Torch_4" };
            for (int i = 0; i < names.Length; i++) { Transform tr = fixtures.Find(names[i]); if (tr != null) tr.localPosition = at[i]; }
            GameObject sun = new GameObject("SunLight");
            sun.transform.SetParent(fixtures, false);
            sun.transform.localPosition = new Vector3(0f, 10.2f, 0f);
            Light l = sun.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.68f, 0.52f, 1f);
            l.range = 24f;
            l.intensity = 4.5f;
            l.shadows = LightShadows.None;
        }

        static Door ExitGate(GameObject room, List<ImpactLever> levers)
        {
            Transform doorways = room.transform.Find("Doorways");
            // 1 cm up: the slab's bottom and the frame jambs' bottoms otherwise share the floor plane (z-fight, shut and open).
            GameObject go = Kit(DoorPrefab, doorways, "Gate_South", new Vector3(0f, 0.01f, -W / 2f), Quaternion.identity);
            Door gate = go.GetComponent<Door>();
            DoorCondition c = gate.Condition;
            SetInt(c, "mode", (int)DoorCondition.Mode.LeversOn);
            SetArray(c, "levers", levers);
            Transform south = doorways.Find("Doorway_South");
            if (south != null) SetRef(south.GetComponent<MagicDoor>(), "gate", gate);
            return gate;
        }

        static void Profile(GameObject room)
        {
            PuzzleRoomProfile profile = room.GetComponent<PuzzleRoomProfile>();
            if (profile == null) profile = room.AddComponent<PuzzleRoomProfile>();
            SetString(profile, "puzzleId", "orrery");
            SetString(profile, "displayName", "The Orrery");
            SetString(profile, "summary", "Light the three star levers: climb to the loft, take the floor pad to the shelf, time the dais pad between sweeps of the orrery to reach the gallery.");
            SetInt(profile, "minimumPlayers", 1);
            SetInt(profile, "optimalPlayers", 3);
            SetInt(profile, "maximumPlayers", 8);
            SetFloat(profile, "expectedSolveSeconds", 75f);
            SetInt(profile, "difficultyTier", 2);
        }

        // ------------------------------------------------------------------ the test scene
        /// <summary>A copy of BatTest with the Bat Room swapped for the Orrery and the rack moved to the entry floor.</summary>
        static void BuildTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null) AssetDatabase.CopyAsset(TemplateScene, TestScene);
            Scene scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            GameObject env = null, managers = null;
            foreach (GameObject r in scene.GetRootGameObjects()) { if (r.name == "Environment") env = r; if (r.name == "_Managers") managers = r; }
            Transform e = env.transform;
            for (int i = e.childCount - 1; i >= 0; i--)
            {
                Transform ch = e.GetChild(i);
                if (ch.GetComponent<LabyrinthRoom>() != null) Object.DestroyImmediate(ch.gameObject);
            }
            Transform bot = e.Find("Weapons/Dagger_Bot");
            if (bot != null) Object.DestroyImmediate(bot.gameObject);

            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), e);
            room.name = "Orrery";
            room.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            room.transform.SetAsFirstSibling();

            // The rack against the entry wall, west of the door; the soul spawns in front of the door.
            Vector3 shift = new Vector3(-6f, 0f, 15.2f) - e.Find("TestRack").position;
            e.Find("TestRack").position += shift;
            foreach (Transform w in e.Find("Weapons")) w.position += shift;
            Transform sp = e.Find("SpawnPoint");
            if (sp != null) sp.position = new Vector3(0f, 0.6f, 12f);
            Transform cam = null;
            foreach (GameObject r in scene.GetRootGameObjects()) if (r.name == "_Cameras") cam = r.transform.Find("Main Camera");
            if (cam != null) { cam.position = new Vector3(0f, 6f, 13f); cam.rotation = Quaternion.LookRotation(new Vector3(0f, 2f, 0f) - cam.position); }

            // Doorways lead nowhere in a test scene: their portals are bricked up as in BatTest.
            foreach (MagicDoor md in room.GetComponentsInChildren<MagicDoor>(true))
            {
                Transform plane = md.transform.Find("Plane");
                if (plane != null) plane.gameObject.SetActive(false);
            }

            WorldAuthority authority = managers.GetComponentInChildren<WorldAuthority>(true);
            LevelClock clock = managers.GetComponentInChildren<LevelClock>(true);
            foreach (Door x in room.GetComponentsInChildren<Door>(true)) SetRef(x, "authority", authority);
            foreach (MagicDoor x in room.GetComponentsInChildren<MagicDoor>(true)) SetRef(x, "authority", authority);
            foreach (ImpactLever x in room.GetComponentsInChildren<ImpactLever>(true)) { SetRef(x, "authority", authority); SetRef(x, "clock", clock); }
            foreach (ClockSpinner x in room.GetComponentsInChildren<ClockSpinner>(true)) SetRef(x, "clock", clock);
            RunSceneBuilder.AssignSceneIds(scene);
            // The template's Bat Room left its goblins, magnet, pot and cracked wall in these lists: nothing of the kind here.
            foreach (string list in new[] { "enemies", "magnets", "pots", "crackedWalls", "ropes", "lifts", "porterGates", "counterweights", "scales", "lightningFields", "anvils", "keys", "runes" })
                SetArray(authority, list, new List<Object>());
            SetArray(authority, "levers", ComponentsInScene<ImpactLever>(scene));
            SetArray(authority, "doors", ComponentsInScene<Door>(scene));
            SetArray(authority, "magicDoors", ComponentsInScene<MagicDoor>(scene));
            SetArray(authority, "rooms", ComponentsInScene<RoomVolume>(scene));
            SetArray(authority, "weapons", ComponentsInScene<WeaponBody>(scene));
            SetArray(authority, "plates", new List<PressurePlate>());
            SetArray(authority, "circuitBridges", new List<CircuitBridge>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ shell
        static GameObject Shell(string objectName, out Transform geo, out Transform play, out Transform fixtures)
        {
            GameObject room = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TwoDoorPrefab));
            room.name = objectName;
            geo = room.transform.Find("Geometry");
            play = room.transform.Find("Gameplay");
            if (play == null) { GameObject g = new GameObject("Gameplay"); g.transform.SetParent(room.transform, false); play = g.transform; }
            for (int i = geo.childCount - 1; i >= 0; i--) Object.DestroyImmediate(geo.GetChild(i).gameObject);
            DestroyChild(play, "Seal");
            Transform doorways = room.transform.Find("Doorways");
            DestroyChild(doorways, "Gate_South");

            Block(geo, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(W, 0.5f, W), MFloor);
            Block(geo, "Roof", new Vector3(0f, H + 0.25f, 0f), new Vector3(W + 1f, 0.5f, W + 1f), MWall);
            Block(geo, "Wall_East", new Vector3(W / 2f + 0.25f, H / 2f, 0f), new Vector3(0.5f, H, W + 1f), MWall);
            Block(geo, "Wall_West", new Vector3(-W / 2f - 0.25f, H / 2f, 0f), new Vector3(0.5f, H, W + 1f), MWall);
            DoorwayWall(geo, "Wall_North", W / 2f + 0.25f);
            DoorwayWall(geo, "Wall_South", -W / 2f - 0.25f);
            Transform north = doorways.Find("Doorway_North"), south = doorways.Find("Doorway_South");
            if (north != null) north.localPosition = new Vector3(0f, 0f, W / 2f);
            if (south != null) south.localPosition = new Vector3(0f, 0f, -W / 2f);

            Transform volume = room.transform.Find("RoomVolume");
            BoxCollider vb = volume != null ? volume.GetComponent<BoxCollider>() : null;
            if (vb != null) { volume.localPosition = Vector3.zero; vb.center = new Vector3(0f, H / 2f, 0f); vb.size = new Vector3(W, H + 1f, W); }
            Transform footprint = room.transform.Find("Footprint");
            BoxCollider fb = footprint != null ? footprint.GetComponent<BoxCollider>() : null;
            if (fb != null) { footprint.localPosition = Vector3.zero; fb.center = new Vector3(0f, H / 2f, 0f); fb.size = new Vector3(W + 12f, H + 30f, W + 12f); }
            Transform spawn = room.transform.Find("SpawnPoint");
            if (spawn != null) spawn.localPosition = new Vector3(0f, 0.6f, W / 2f - 4f);
            fixtures = room.transform.Find("Fixtures");
            DestroyChild(fixtures, "RoomSign");
            Transform number = fixtures.Find("FloorNumber");
            if (number != null) number.localPosition = new Vector3(0f, 0.02f, W / 2f - 7f);
            return room;
        }

        static void Octagonise(Transform geo)
        {
            float a = W / 2f, side = 2f * a * Mathf.Tan(22.5f * Mathf.Deg2Rad), cornerLen = side + 1.4f, d = a / 1.41421f;
            Block(geo, "Corner_NE", new Vector3(d, H / 2f, d), new Vector3(cornerLen, H, 0.5f), MWall, Quaternion.Euler(0f, 45f, 0f));
            Block(geo, "Corner_NW", new Vector3(-d, H / 2f, d), new Vector3(cornerLen, H, 0.5f), MWall, Quaternion.Euler(0f, -45f, 0f));
            Block(geo, "Corner_SE", new Vector3(d, H / 2f, -d), new Vector3(cornerLen, H, 0.5f), MWall, Quaternion.Euler(0f, -45f, 0f));
            Block(geo, "Corner_SW", new Vector3(-d, H / 2f, -d), new Vector3(cornerLen, H, 0.5f), MWall, Quaternion.Euler(0f, 45f, 0f));
        }

        static void DoorwayWall(Transform geo, string name, float z)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(geo, false);
            Transform t = wall.transform;
            float side = W / 2f - 1.5f;
            Block(t, "Left", new Vector3(-W / 2f + side / 2f, H / 2f, z), new Vector3(side, H, 0.5f), MWall);
            Block(t, "Right", new Vector3(W / 2f - side / 2f, H / 2f, z), new Vector3(side, H, 0.5f), MWall);
            Block(t, "Lintel", new Vector3(0f, (3.5f + H) / 2f, z), new Vector3(3f, H - 3.5f, 0.5f), MWall);
        }

        // ------------------------------------------------------------------ pieces
        static GameObject Kit(string path, Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            return go;
        }

        static ImpactLever Lever(Transform play, string name, Vector3 pos, Quaternion rot)
        {
            ImpactLever lever = Kit(LeverPrefab, play, name, pos, rot).GetComponent<ImpactLever>();
            SetBool(lever, "latching", true);
            return lever;
        }

        static GameObject Deco(Transform parent, string deco, string name, Vector3 pos, float yaw)
        {
            return Kit(K + "Deco/" + deco + ".prefab", parent, name, pos, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>A timber post from the floor to just under a perch: the perch looks held up, and it is a plain box.</summary>
        static void Post(Transform geo, string name, Vector3 foot, float height)
        {
            Block(geo, name, foot + Vector3.up * (height / 2f), new Vector3(0.5f, height, 0.5f), MWood);
        }

        static void Stair(Transform geo, string name, Vector3 foot, Vector3 dir, int count, float rise, float tread, float width)
        {
            GameObject stair = new GameObject(name);
            stair.transform.SetParent(geo, false);
            bool alongZ = Mathf.Abs(dir.z) > Mathf.Abs(dir.x);
            for (int k = 1; k <= count; k++)
            {
                float h = k * rise;
                Vector3 centre = foot + dir.normalized * ((k - 0.5f) * tread) + Vector3.up * (h / 2f);
                float w = width - ((k & 1) == 0 ? 0.02f : 0f);
                Block(stair.transform, "Step_" + k, centre, alongZ ? new Vector3(w, h, tread) : new Vector3(tread, h, w), MWood);
            }
        }

        /// <summary>A flat disc. Solid ones get a mesh collider (a primitive cylinder's own capsule would round its edge).</summary>
        static GameObject Disc(Transform parent, string name, Vector3 pos, float radius, float height, string material, bool solid)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            go.layer = LayerWorld;
            Paint(go, material);
            if (solid) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            Static(go);
            return go;
        }

        /// <summary>A ring inlaid in the floor: a thin lit disc with a floor-coloured disc over all but its edge.</summary>
        static void Ring(Transform parent, string name, float radius, float width)
        {
            Disc(parent, name, new Vector3(0f, 0.004f, 0f), radius, 0.006f, MFlow, false);
            Disc(parent, name + "_Cut", new Vector3(0f, 0.008f, 0f), radius - width, 0.006f, MFloor, false);
        }

        static void Inlay(Transform parent, string name, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            GameObject go = Block(parent, name, (from + to) / 2f + Vector3.up * 0.012f, new Vector3(0.18f, 0.012f, d.magnitude), MFlow, Quaternion.LookRotation(d.normalized, Vector3.up));
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static void Rail(Transform geo, string name, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            Vector3 up = Mathf.Abs(d.normalized.y) > 0.9f ? Vector3.forward : Vector3.up;
            GameObject go = Block(geo, name, (from + to) / 2f, new Vector3(0.16f, 0.16f, d.magnitude + 0.16f), MFlow, Quaternion.LookRotation(d.normalized, up));
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static void Cord(Transform geo, string name, Vector3 from, Vector3 to, float thickness, string material)
        {
            Vector3 d = to - from;
            GameObject go = Visual(geo, name, PrimitiveType.Cylinder, from + d / 2f, Vector3.zero, new Vector3(thickness, d.magnitude / 2f, thickness), material);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            Static(go);
        }

        /// <summary>A collider-less primitive (a look, not a surface).</summary>
        static GameObject Visual(Transform parent, string name, PrimitiveType prim, Vector3 pos, Vector3 euler, Vector3 scale, string material)
        {
            GameObject go = GameObject.CreatePrimitive(prim);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.layer = LayerWorld;
            Paint(go, material);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 scale, string material) { return Block(parent, name, pos, scale, material, Quaternion.identity); }
        static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 scale, string material, Quaternion rot)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.layer = LayerWorld;
            Paint(go, material);
            Static(go);
            return go;
        }

        static GameObject Static(GameObject go)
        {
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
        static void DestroyChild(Transform parent, string name) { Transform c = parent != null ? parent.Find(name) : null; if (c != null) Object.DestroyImmediate(c.gameObject); }
        static void SetRef(Object target, string property, Object value) { SetProperty(target, property, p => p.objectReferenceValue = value); }
        static void SetString(Object target, string property, string value) { SetProperty(target, property, p => p.stringValue = value); }
        static void SetFloat(Object target, string property, float value) { SetProperty(target, property, p => p.floatValue = value); }
        static void SetBool(Object target, string property, bool value) { SetProperty(target, property, p => p.boolValue = value); }
        static void SetInt(Object target, string property, int value) { SetProperty(target, property, p => { if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value; else p.intValue = value; }); }
        static void SetProperty(Object target, string name, System.Action<SerializedProperty> write)
        {
            if (target == null) { Debug.LogError("[Orrery] missing target for " + name); return; }
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(name);
            if (property == null) { Debug.LogError("[Orrery] " + target.GetType().Name + " has no property " + name); return; }
            write(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void SetArray<T>(Object target, string property, List<T> values) where T : Object
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError("[Orrery] " + target.GetType().Name + " has no " + property); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
