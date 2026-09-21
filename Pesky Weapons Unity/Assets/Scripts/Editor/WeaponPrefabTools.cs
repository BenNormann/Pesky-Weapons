using Pesky.Data;
using Pesky.Game;
using UnityEditor;
using UnityEngine;

namespace Pesky.Editor
{
    /// <summary>Editor utilities that keep the weapon prefabs in step with their WeaponDef assets.</summary>
    public static class WeaponPrefabTools
    {
        const string DefFolder = "Assets/Data/Weapons";

        /// <summary>
        /// Copies mass / drag / physics material from every WeaponDef onto its prefab (Rigidbody and all
        /// child colliders) so the authored prefab shows the same numbers the game applies at runtime.
        /// </summary>
        [MenuItem("Pesky/Weapons/Apply Defs To Prefabs")]
        public static void ApplyDefsToPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:WeaponDef", new[] { DefFolder });
            int done = 0;
            foreach (string guid in guids)
            {
                WeaponDef def = AssetDatabase.LoadAssetAtPath<WeaponDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.prefab == null) continue;

                string path = AssetDatabase.GetAssetPath(def.prefab);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    WeaponBody body = root.GetComponent<WeaponBody>();
                    if (body == null || body.Def != def)
                    {
                        Debug.LogWarning("Pesky: " + path + " has no WeaponBody using " + def.name + ".", def);
                        continue;
                    }
                    body.ApplyDef();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("Pesky: applied " + done + " of " + guids.Length + " weapon defs to their prefabs.");
        }
    }
}
