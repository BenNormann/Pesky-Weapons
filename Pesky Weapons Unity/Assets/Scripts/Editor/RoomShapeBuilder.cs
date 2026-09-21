using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pesky.Game;

namespace Pesky.Editor
{
    /// <summary>The floor plans the tower is built from. Every room is a closed polygon of straight walls.</summary>
    public enum RoomShape
    {
        Round,        // 12-sided
        Octagon,      // 8-sided
        Hexagon,      // 6-sided
        Wedge,        // trapezoid: narrow end 'inner', wide end 'across', 'depth' deep
        Ring,         // corridor around a solid core
        LongGallery,  // rectangle, long in Z
        TallShaft,    // 8-sided, tall
        LShape,       // two arms of width 'inner'
        Crescent      // half annulus, caps at both ends
    }

    /// <summary>One authored room. Sizes are the OUTER size across the wall centre lines.</summary>
    [Serializable]
    public sealed class RoomShapeSpec
    {
        [Header("Identity")]
        public string groupName = "Room06_RopeRoom";
        public string signText = "6  ROPE ROOM";
        public int roomId;
        public int signId;
        public int spawnId;

        [Header("Shape")]
        public RoomShape shape = RoomShape.Round;
        public Vector3 floorCentre = Vector3.zero;   // world position of the floor's top surface at the centre
        public float across = 18f;                   // diameter / X size
        public float depth = 18f;                    // Z size (rectangular shapes only)
        public float height = 10f;                   // clear interior height, floor top to wall top
        public float inner = 8f;                     // Ring / Crescent inner diameter, Wedge narrow end, LShape arm width
        public float yaw;                            // rotation of the whole room about Y

        [Header("Doorways")]
        [Tooltip("Compass angles in degrees (0 = +Z, 90 = +X) of the walls that get a 3 x 3.5 opening.")]
        public float[] doorwayAngles = new float[0];
        [Tooltip("Explicit polygon edge indices; when not empty these win over doorwayAngles.")]
        public int[] doorwayEdges = new int[0];
        [Tooltip("Ring only: angles of openings in the inner (core) wall.")]
        public float[] innerDoorwayAngles = new float[0];

        [Header("Parts")]
        public bool buildFloor = true;
        public bool buildRoof = true;
        public bool coreFloor = true;                // Ring: cap the core with a floor and a roof
        public int torches = 4;                      // at most 6
        public bool gameplayObjects = true;          // false = a pure shell: no RoomVolume, no SpawnPoint
        public bool createGroups = true;             // make Environment/<groupName>/{Geometry,Gameplay,Lighting,Spawns}
        public string parentPath = "Environment";    // where the room group goes, or the group the section goes in
        public string sectionName = "Shell";         // the name of the geometry container
    }

    /// <summary>Where a doorway ended up: floor centre of the opening, facing INTO the room.</summary>
    public struct Doorway
    {
        public int edge;
        public Vector3 position;
        public float yaw;          // rotation whose +Z points into the room
        public float wallAngle;    // the compass angle of the wall's outward normal
        public float length;
    }

    /// <summary>
    /// Authors a grey-box room in the OPEN scene out of prefab instances. Nothing is generated at
    /// runtime: this is an editor tool that places the same prefabs a human would drag in.
    /// </summary>
    public static class RoomShapeBuilder
    {
        public const float WallThickness = 0.5f;
        public const float SlabThickness = 0.5f;
        public const float DoorWidth = 3f;
        public const float DoorHeight = 3.5f;
        public const float Overlap = 0.5f;      // walls are this much longer so corners mitre
        public const int RingCoreSides = 8;     // a 12-sided core has chords too short for a 3 m doorway

        const string PrefabWall = "Assets/Prefabs/Rooms/WallSegment.prefab";
        const string PrefabDoorway = "Assets/Prefabs/Rooms/DoorwayWallSegment.prefab";
        const string PrefabDiscFloor = "Assets/Prefabs/Rooms/DiscFloor.prefab";
        const string PrefabDiscRoof = "Assets/Prefabs/Rooms/DiscRoof.prefab";
        const string PrefabFloor = "Assets/Prefabs/Rooms/Floor.prefab";
        const string PrefabRoof = "Assets/Prefabs/Rooms/Roof.prefab";
        const string PrefabTorch = "Assets/Prefabs/Kit/Torch.prefab";
        const string PrefabSign = "Assets/Prefabs/Kit/Sign.prefab";
        const string PrefabSpawn = "Assets/Prefabs/Kit/SpawnPoint.prefab";
        const string PrefabRoomVolume = "Assets/Prefabs/Kit/RoomVolume.prefab";

        // ------------------------------------------------------------------ public API

        /// <summary>Builds the room and returns its group (or the section container when createGroups is false).</summary>
        public static GameObject Build(RoomShapeSpec s, out List<Doorway> doorways)
        {
            doorways = new List<Doorway>();
            Vector2[] poly = Polygon(s);
            if (poly == null || poly.Length < 3) { Debug.LogError("RoomShapeBuilder: no polygon for " + s.groupName); return null; }

            Transform parent = FindOrCreate(s.parentPath);
            Transform group = parent;
            Transform geometry = parent, gameplay = parent, lighting = parent, spawns = parent;

            if (s.createGroups)
            {
                group = FindOrCreateChild(parent, s.groupName);
                geometry = FindOrCreateChild(group, "Geometry");
                gameplay = FindOrCreateChild(group, "Gameplay");
                lighting = FindOrCreateChild(group, "Lighting");
                spawns = FindOrCreateChild(group, "Spawns");
            }

            Transform section = FindOrCreateChild(geometry, s.sectionName);
            section.gameObject.layer = LayerMask.NameToLayer("World");

            HashSet<int> doorEdges = ChooseDoorwayEdges(s, poly);

            // ---- floor and roof
            if (s.buildFloor) BuildSlab(s, poly, section, false);
            if (s.buildRoof) BuildSlab(s, poly, section, true);

            // ---- walls
            BuildWalls(s, poly, section, doorEdges, "Wall", doorways, false);

            // ---- the Ring's core
            if (s.shape == RoomShape.Ring)
            {
                Vector2[] core = RegularPolygon(s.inner * 0.5f, RingCoreSides);
                HashSet<int> coreDoors = new HashSet<int>();
                for (int i = 0; i < s.innerDoorwayAngles.Length; i++)
                    coreDoors.Add(NearestEdge(core, s.innerDoorwayAngles[i]));
                List<Doorway> coreWays = new List<Doorway>();
                BuildWalls(s, core, section, coreDoors, "Core", coreWays, true);
                doorways.AddRange(coreWays);
                if (s.coreFloor)
                {
                    float cr = s.inner * 0.5f * Mathf.Cos(Mathf.PI / RingCoreSides) + WallThickness * 0.5f;
                    Disc(section, "CoreFloor", s.floorCentre + Vector3.up * (-SlabThickness * 0.5f), cr, false);
                    if (s.buildRoof)
                        Disc(section, "CoreRoof", s.floorCentre + Vector3.up * (s.height + SlabThickness * 0.5f), cr, true);
                }
            }

            if (!s.createGroups) return section.gameObject;

            // ---- room volume, sign, spawn point, torches
            Bounds b = PolyBounds(poly);
            if (s.gameplayObjects)
            {
            GameObject vol = Instantiate(PrefabRoomVolume, gameplay, "RoomVolume_" + Num(s.groupName));
            vol.transform.position = s.floorCentre;
            vol.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);
            BoxCollider box = vol.GetComponent<BoxCollider>();
            box.center = new Vector3(b.center.x, s.height * 0.5f, b.center.z);
            box.size = new Vector3(b.size.x + 1f, s.height + 1f, b.size.z + 1f);
            SetInt(vol.GetComponent<RoomVolume>(), "id", s.roomId);
            SetString(vol.GetComponent<RoomVolume>(), "roomName", s.signText);

            GameObject spawn = Instantiate(PrefabSpawn, spawns, "SoulSpawn_" + Num(s.groupName));
            spawn.transform.position = s.floorCentre + new Vector3(0f, 1f, 0f);
            SetInt(spawn.GetComponent<SpawnPoint>(), "id", s.spawnId);
            }

            if (!string.IsNullOrEmpty(s.signText))
            {
                Vector3 signAt = s.floorCentre;
                float signYaw = s.yaw;
                if (doorways.Count > 0)
                {
                    Doorway d = doorways[0];
                    Vector3 into = Quaternion.Euler(0f, d.yaw, 0f) * Vector3.forward;
                    Vector3 side = Quaternion.Euler(0f, d.yaw + 90f, 0f) * Vector3.forward;
                    signAt = d.position + into * 1.6f + side * 2.6f;
                    signYaw = d.yaw + 200f;
                }
                GameObject sign = Instantiate(PrefabSign, gameplay, "Sign_" + Num(s.groupName));
                sign.transform.position = signAt;
                sign.transform.rotation = Quaternion.Euler(0f, signYaw, 0f);
                Sign sc = sign.GetComponent<Sign>();
                SetInt(sc, "id", s.signId);
                SetString(sc, "text", s.signText);
                RefreshSignLabel(sign);
            }

            BuildTorches(s, poly, lighting, doorEdges);
            EditorSceneMarkDirty();
            return group.gameObject;
        }

        /// <summary>A readable edge table, for picking doorway indices by hand.</summary>
        public static string Describe(RoomShapeSpec s)
        {
            Vector2[] poly = Polygon(s);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(s.groupName + " " + s.shape + " edges=" + poly.Length);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b2 = poly[(i + 1) % poly.Length];
                Vector2 mid = (a + b2) * 0.5f;
                Vector2 outw = -Inward(poly, i);
                sb.AppendLine("  " + i + " len=" + (b2 - a).magnitude.ToString("F2") +
                              " mid=" + mid.ToString("F2") + " normal=" + Angle(outw).ToString("F0") + " deg");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ polygons

        public static Vector2[] Polygon(RoomShapeSpec s)
        {
            float R = s.across * 0.5f;
            switch (s.shape)
            {
                case RoomShape.Round: return RegularPolygon(R, 12);
                case RoomShape.Octagon: return RegularPolygon(R, 8);
                case RoomShape.TallShaft: return RegularPolygon(R, 8);
                case RoomShape.Hexagon: return RegularPolygon(R, 6);
                case RoomShape.Ring: return RegularPolygon(R, 12);
                case RoomShape.LongGallery: return Rect(s.across, s.depth);
                case RoomShape.Wedge: return Trapezoid(s.inner, s.across, s.depth);
                case RoomShape.LShape: return LPoly(s.across, s.depth, s.inner);
                case RoomShape.Crescent: return CrescentPoly(R, s.inner * 0.5f, 8);
            }
            return null;
        }

        public static Vector2[] RegularPolygon(float radius, int n)
        {
            Vector2[] p = new Vector2[n];
            float step = 360f / n;
            for (int i = 0; i < n; i++)
            {
                float a = (i - 0.5f) * step * Mathf.Deg2Rad;
                p[i] = new Vector2(radius * Mathf.Sin(a), radius * Mathf.Cos(a));
            }
            return p;
        }

        static Vector2[] Rect(float w, float d)
        {
            float x = w * 0.5f, z = d * 0.5f;
            return new[] { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(x, z), new Vector2(-x, z) };
        }

        static Vector2[] Trapezoid(float narrow, float wide, float d)
        {
            float z = d * 0.5f, n = narrow * 0.5f, w = wide * 0.5f;
            return new[] { new Vector2(-n, -z), new Vector2(n, -z), new Vector2(w, z), new Vector2(-w, z) };
        }

        static Vector2[] LPoly(float w, float d, float arm)
        {
            float x0 = -w * 0.5f, x1 = w * 0.5f, z0 = -d * 0.5f, z1 = d * 0.5f;
            return new[]
            {
                new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z0 + arm),
                new Vector2(x0 + arm, z0 + arm), new Vector2(x0 + arm, z1), new Vector2(x0, z1)
            };
        }

        /// <summary>Half annulus: outer arc -90..+90, cap, inner arc back, cap. Caps face -Z and +Z.</summary>
        static Vector2[] CrescentPoly(float outer, float innerR, int segments)
        {
            List<Vector2> p = new List<Vector2>();
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-90f, 90f, i / (float)segments) * Mathf.Deg2Rad;
                p.Add(new Vector2(outer * Mathf.Sin(a), outer * Mathf.Cos(a)));
            }
            for (int i = segments; i >= 0; i--)
            {
                float a = Mathf.Lerp(-90f, 90f, i / (float)segments) * Mathf.Deg2Rad;
                p.Add(new Vector2(innerR * Mathf.Sin(a), innerR * Mathf.Cos(a)));
            }
            return p.ToArray();
        }

        // ------------------------------------------------------------------ geometry

        static void BuildWalls(RoomShapeSpec s, Vector2[] poly, Transform section, HashSet<int> doorEdges,
                               string prefix, List<Doorway> doorways, bool flipInward)
        {
            Quaternion roomRot = Quaternion.Euler(0f, s.yaw, 0f);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                Vector2 mid = (a + b) * 0.5f;
                float len = (b - a).magnitude;
                if (len < 0.05f) continue;
                Vector2 inward = Inward(poly, i);
                if (flipInward) inward = -inward;

                Vector3 worldMid = s.floorCentre + roomRot * new Vector3(mid.x, 0f, mid.y);
                float wallYaw = s.yaw + Angle(inward);
                bool isDoor = doorEdges.Contains(i);
                float L = len + Overlap;

                if (isDoor && L >= DoorWidth + 1f)
                {
                    GameObject g = Instantiate(PrefabDoorway, section, prefix + "_" + i + "_Doorway");
                    g.transform.position = worldMid;
                    g.transform.rotation = Quaternion.Euler(0f, wallYaw, 0f);
                    float side = L * 0.5f - DoorWidth * 0.5f;
                    SetBox(g.transform.Find("Left"), new Vector3(-(DoorWidth * 0.5f + side * 0.5f), s.height * 0.5f, 0f),
                           new Vector3(side, s.height, WallThickness));
                    SetBox(g.transform.Find("Right"), new Vector3(DoorWidth * 0.5f + side * 0.5f, s.height * 0.5f, 0f),
                           new Vector3(side, s.height, WallThickness));
                    SetBox(g.transform.Find("Lintel"), new Vector3(0f, (DoorHeight + s.height) * 0.5f, 0f),
                           new Vector3(DoorWidth, s.height - DoorHeight, WallThickness));

                    Doorway d;
                    d.edge = i;
                    d.position = worldMid;
                    d.yaw = wallYaw;
                    d.wallAngle = s.yaw + Angle(-inward);
                    d.length = len;
                    doorways.Add(d);
                }
                else
                {
                    if (isDoor) Debug.LogWarning("RoomShapeBuilder: edge " + i + " of " + s.groupName + " is too short for a doorway.");
                    GameObject g = Instantiate(PrefabWall, section, prefix + "_" + i);
                    g.transform.position = worldMid + Vector3.up * (s.height * 0.5f);
                    g.transform.rotation = Quaternion.Euler(0f, wallYaw, 0f);
                    g.transform.localScale = new Vector3(L, s.height, WallThickness);
                }
            }
        }

        static void BuildSlab(RoomShapeSpec s, Vector2[] poly, Transform section, bool roof)
        {
            float y = roof ? s.height + SlabThickness * 0.5f : -SlabThickness * 0.5f;
            Vector3 at = s.floorCentre + Vector3.up * y;
            string nm = roof ? "Roof" : "Floor";

            switch (s.shape)
            {
                case RoomShape.Round:
                case RoomShape.Octagon:
                case RoomShape.TallShaft:
                case RoomShape.Hexagon:
                    Disc(section, nm, at, s.across * 0.5f + WallThickness, roof);
                    break;

                case RoomShape.Ring:
                case RoomShape.Crescent:
                    Annulus(s, section, nm, at, roof);
                    break;

                case RoomShape.LShape:
                    {
                        float w = s.across, d = s.depth, arm = s.inner;
                        Box(section, nm + "_A", at + RoomOffset(s, new Vector2(0f, -d * 0.5f + arm * 0.5f)),
                            s.yaw, new Vector3(w + WallThickness, SlabThickness, arm + WallThickness), roof);
                        Box(section, nm + "_B", at + RoomOffset(s, new Vector2(-w * 0.5f + arm * 0.5f, arm * 0.5f)),
                            s.yaw, new Vector3(arm + WallThickness, SlabThickness, d - arm + WallThickness), roof);
                        break;
                    }

                default:
                    {
                        Bounds b = PolyBounds(poly);
                        Box(section, nm, at + RoomOffset(s, new Vector2(b.center.x, b.center.z)), s.yaw,
                            new Vector3(b.size.x + WallThickness, SlabThickness, b.size.z + WallThickness), roof);
                        break;
                    }
            }
        }

        /// <summary>A ring of overlapping boxes from the inner radius to the outer radius.</summary>
        static void Annulus(RoomShapeSpec s, Transform section, string nm, Vector3 at, bool roof)
        {
            const int n = 12;
            float rOut = s.across * 0.5f + WallThickness * 0.5f;
            float rIn = Mathf.Max(0.5f, s.inner * 0.5f * Mathf.Cos(Mathf.PI / RingCoreSides) - WallThickness * 0.5f);
            float rm = (rIn + rOut) * 0.5f;
            float band = rOut - rIn;
            float chord = 2f * rOut * Mathf.Tan(Mathf.PI / n) * 1.3f;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n;
                Vector2 dir = Dir(a);
                Box(section, nm + "_" + i, at + RoomOffset(s, dir * rm), s.yaw + a,
                    new Vector3(chord, SlabThickness, band), roof);
            }
        }

        static void Disc(Transform section, string nm, Vector3 at, float radius, bool roof)
        {
            GameObject g = Instantiate(roof ? PrefabDiscRoof : PrefabDiscFloor, section, nm);
            g.transform.position = at;
            g.transform.rotation = Quaternion.identity;
            g.transform.localScale = new Vector3(radius * 2f, SlabThickness * 0.5f, radius * 2f);
        }

        static void Box(Transform section, string nm, Vector3 at, float yaw, Vector3 size, bool roof)
        {
            GameObject g = Instantiate(roof ? PrefabRoof : PrefabFloor, section, nm);
            g.transform.position = at;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = size;
        }

        static void BuildTorches(RoomShapeSpec s, Vector2[] poly, Transform lighting, HashSet<int> doorEdges)
        {
            int want = Mathf.Clamp(s.torches, 0, 6);
            if (want == 0) return;
            List<int> usable = new List<int>();
            for (int i = 0; i < poly.Length; i++)
            {
                if (doorEdges.Contains(i)) continue;
                if ((poly[(i + 1) % poly.Length] - poly[i]).magnitude < 2f) continue;
                usable.Add(i);
            }
            if (usable.Count == 0) return;
            Quaternion roomRot = Quaternion.Euler(0f, s.yaw, 0f);
            int placed = 0;
            for (int k = 0; k < want; k++)
            {
                int i = usable[Mathf.RoundToInt(k * (usable.Count - 1) / (float)Mathf.Max(1, want - 1))];
                if (want == 1) i = usable[0];
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                Vector2 mid = (a + b) * 0.5f;
                Vector2 inward = Inward(poly, i);
                Vector3 at = s.floorCentre + roomRot * new Vector3(mid.x, 0f, mid.y)
                           + roomRot * new Vector3(inward.x, 0f, inward.y) * (WallThickness * 0.5f + 0.05f)
                           + Vector3.up * Mathf.Min(3f, s.height - 1f);
                GameObject t = Instantiate(PrefabTorch, lighting, "Torch_" + i);
                t.transform.position = at;
                t.transform.rotation = Quaternion.Euler(0f, s.yaw + Angle(inward), 0f);
                placed++;
                if (placed >= want) break;
            }
        }

        // ------------------------------------------------------------------ maths helpers

        public static float Angle(Vector2 dir) { return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; }

        public static Vector2 Dir(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

        static Vector3 RoomOffset(RoomShapeSpec s, Vector2 local)
        {
            return Quaternion.Euler(0f, s.yaw, 0f) * new Vector3(local.x, 0f, local.y);
        }

        static float SignedArea(Vector2[] p)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 u = p[i], v = p[(i + 1) % p.Length];
                a += u.x * v.y - v.x * u.y;
            }
            return a * 0.5f;
        }

        /// <summary>Unit normal of edge i pointing into the polygon.</summary>
        public static Vector2 Inward(Vector2[] p, int i)
        {
            Vector2 d = (p[(i + 1) % p.Length] - p[i]).normalized;
            Vector2 n = SignedArea(p) > 0f ? new Vector2(-d.y, d.x) : new Vector2(d.y, -d.x);
            return n;
        }

        static Bounds PolyBounds(Vector2[] p)
        {
            Vector3 min = new Vector3(p[0].x, 0f, p[0].y), max = min;
            for (int i = 1; i < p.Length; i++)
            {
                min = Vector3.Min(min, new Vector3(p[i].x, 0f, p[i].y));
                max = Vector3.Max(max, new Vector3(p[i].x, 0f, p[i].y));
            }
            Bounds b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        static HashSet<int> ChooseDoorwayEdges(RoomShapeSpec s, Vector2[] poly)
        {
            HashSet<int> set = new HashSet<int>();
            if (s.doorwayEdges != null && s.doorwayEdges.Length > 0)
            {
                for (int i = 0; i < s.doorwayEdges.Length; i++)
                {
                    int e = s.doorwayEdges[i];
                    if (e >= 0 && e < poly.Length) set.Add(e);
                }
                return set;
            }
            if (s.doorwayAngles != null)
                for (int i = 0; i < s.doorwayAngles.Length; i++) set.Add(NearestEdge(poly, s.doorwayAngles[i]));
            return set;
        }

        /// <summary>The edge whose outward normal best matches the compass angle; ties go to the furthest out.</summary>
        public static int NearestEdge(Vector2[] poly, float angleDeg)
        {
            Vector2 want = Dir(angleDeg);
            int best = -1; float bestScore = float.NegativeInfinity;
            int fallback = -1; float fallbackError = float.PositiveInfinity;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 outw = -Inward(poly, i);
                float err = Mathf.Abs(Mathf.DeltaAngle(Angle(outw), angleDeg));
                Vector2 mid = (poly[i] + poly[(i + 1) % poly.Length]) * 0.5f;
                float len = (poly[(i + 1) % poly.Length] - poly[i]).magnitude;
                if (err < fallbackError) { fallbackError = err; fallback = i; }
                if (err > 45f || len < DoorWidth + 1f) continue;
                float score = Vector2.Dot(mid, want);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best >= 0 ? best : fallback;
        }

        // ------------------------------------------------------------------ scene helpers

        public static Transform FindOrCreate(string path)
        {
            string[] parts = path.Split('/');
            Transform current = null;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == parts[0]) { current = root.transform; break; }
            if (current == null) current = new GameObject(parts[0]).transform;
            for (int i = 1; i < parts.Length; i++) current = FindOrCreateChild(current, parts[i]);
            return current;
        }

        public static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        static GameObject Instantiate(string prefabPath, Transform parent, string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogError("RoomShapeBuilder: missing prefab " + prefabPath); return null; }
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.name = name;
            return g;
        }

        static void SetBox(Transform t, Vector3 localPos, Vector3 localScale)
        {
            if (t == null) return;
            t.localPosition = localPos;
            t.localScale = localScale;
        }

        static string Num(string groupName)
        {
            int i = 0;
            while (i < groupName.Length && !char.IsDigit(groupName[i]) && groupName[i] != '_') i++;
            int j = groupName.IndexOf('_');
            return j > 0 ? groupName.Substring(4, j - 4) : groupName;
        }

        public static void SetInt(Component c, string field, int value)
        {
            if (c == null) return;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(field);
            if (p != null) { p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            so.Dispose();
        }

        public static void SetString(Component c, string field, string value)
        {
            if (c == null) return;
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(field);
            if (p != null) { p.stringValue = value; so.ApplyModifiedProperties(); }
            so.Dispose();
        }

        static void RefreshSignLabel(GameObject sign)
        {
            // Sign is [ExecuteAlways] and applies its own text in OnValidate; nudge it after the edit.
            Sign sc = sign.GetComponent<Sign>();
            if (sc != null) EditorUtility.SetDirty(sc);
        }

        static void EditorSceneMarkDirty()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }

    /// <summary>Menu front end: fill the fields in, press Build.</summary>
    public sealed class RoomShapeBuilderWindow : EditorWindow
    {
        [SerializeField] RoomShapeSpec spec = new RoomShapeSpec();
        SerializedObject _so;
        Vector2 _scroll;

        [MenuItem("Pesky/Rooms/Room Shape Builder")]
        public static void Open()
        {
            GetWindow<RoomShapeBuilderWindow>(false, "Room Shape Builder", true).minSize = new Vector2(340f, 420f);
        }

        void OnEnable() { _so = new SerializedObject(this); }

        void OnGUI()
        {
            _so.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.PropertyField(_so.FindProperty("spec"), true);
            EditorGUILayout.EndScrollView();
            _so.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Describe edges"))
                Debug.Log(RoomShapeBuilder.Describe(spec));
            if (GUILayout.Button("Build room in the open scene"))
            {
                List<Doorway> ways;
                GameObject g = RoomShapeBuilder.Build(spec, out ways);
                if (g != null)
                {
                    Selection.activeGameObject = g;
                    var sb = new System.Text.StringBuilder("Built " + g.name + " with " + ways.Count + " doorway(s):\n");
                    foreach (Doorway w in ways) sb.AppendLine("  edge " + w.edge + " at " + w.position.ToString("F2") + " yaw " + w.yaw.ToString("F1"));
                    Debug.Log(sb.ToString());
                }
            }
        }
    }
}
