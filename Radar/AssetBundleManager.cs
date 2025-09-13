using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Radar
{
    internal static class AssetFileManager
    {
        public static GameObject RadarhudPrefab { get; private set; }
        public static GameObject RadarBliphudPrefab { get; private set; }

        public static Sprite[] NormalEnemyBlips { get; private set; } = new Sprite[3];
        public static Sprite[] BossEnemyBlips { get; private set; } = new Sprite[3];
        public static Sprite[] DeadEnemyBlips { get; private set; } = new Sprite[3];
        public static Sprite[] LootBlips { get; private set; } = new Sprite[3];
        public static Sprite[] BTRBlips { get; private set; } = new Sprite[3];
        public static Sprite[] MineBlips { get; private set; } = new Sprite[3];

        internal static bool Loaded { get; private set; } = false;

        /// <summary>
        /// Load prefab from an AssetBundle file sitting on disk.
        /// If you want *all* prefabs as loose .prefab assets,
        /// put them under Resources and use Resources.Load instead.
        /// </summary>
        private static GameObject? LoadPrefabFromBundle(string bundlePath, string assetName)
        {
            if (!File.Exists(bundlePath))
            {
                Debug.LogError($"Missing bundle at {bundlePath}");
                return null;
            }

            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Debug.LogError($"Cannot load bundle: {bundlePath}");
                return null;
            }

            var prefab = bundle.LoadAsset<GameObject>(assetName);
            bundle.Unload(false);
            return prefab;
        }

        private static Sprite LoadPngFromResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Debug.LogError($"Missing embedded resource: {resourceName}");
                return null;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            byte[] bytes = ms.ToArray();

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, bytes, true))
            {
                Debug.LogError($"Failed to decode PNG resource {resourceName}");
                return null;
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }

        internal static void LoadFromFolder()
        {
            if (Loaded) return;

            using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Radar.bundle.radarhud.bundle");
            if (resourceStream == null)
            {
                Debug.LogError("Failed to find radar AssetBundle from embedded resource!");
                return;
            }

            try
            {
                // ---- Prefabs ----
                // Option 1: if you still keep prefabs in .unity3d/.bundle files on disk
                //RadarhudPrefab = LoadPrefabFromBundle(Path.Combine(baseFolder, "radarhud.bundle"), "Assets/Examples/Halo Reach/Hud/RadarHUD.prefab");
                //RadarBliphudPrefab = LoadPrefabFromBundle(Path.Combine(baseFolder, "radarhud.bundle"), "Assets/Examples/Halo Reach/Hud/RadarBlipHUD.prefab");
                var bundle = AssetBundle.LoadFromStream(resourceStream);
                RadarhudPrefab = bundle.LoadAsset<GameObject>("Assets/Examples/Halo Reach/Hud/RadarHUD.prefab")!;
                RadarBliphudPrefab = bundle.LoadAsset<GameObject>("Assets/Examples/Halo Reach/Hud/RadarBlipHUD.prefab")!;
                // Option 2: if you export RadarHUD.prefab as loose .prefab in Resources folder
                // RadarhudPrefab = Resources.Load<GameObject>("RadarHUD");
                // RadarBliphudPrefab = Resources.Load<GameObject>("RadarBlipHUD");

                // ---- Blip Sprites ----
                NormalEnemyBlips[0] = LoadPngFromResource("Radar.bundle.normal_enemy.png");
                NormalEnemyBlips[1] = LoadPngFromResource("Radar.bundle.normal_enemy_up.png");
                NormalEnemyBlips[2] = LoadPngFromResource("Radar.bundle.normal_enemy_down.png");

                BossEnemyBlips[0] = LoadPngFromResource("Radar.bundle.boss_enemy.png");
                BossEnemyBlips[1] = LoadPngFromResource("Radar.bundle.boss_enemy_up.png");
                BossEnemyBlips[2] = LoadPngFromResource("Radar.bundle.boss_enemy_down.png");

                DeadEnemyBlips[0] = LoadPngFromResource("Radar.bundle.dead_enemy.png");
                DeadEnemyBlips[1] = LoadPngFromResource("Radar.bundle.dead_enemy_up.png");
                DeadEnemyBlips[2] = LoadPngFromResource("Radar.bundle.dead_enemy_down.png");

                LootBlips[0] = LoadPngFromResource("Radar.bundle.loot.png");
                LootBlips[1] = LoadPngFromResource("Radar.bundle.loot_up.png");
                LootBlips[2] = LoadPngFromResource("Radar.bundle.loot_down.png");

                BTRBlips[0] = LoadPngFromResource("Radar.bundle.btr.png");
                BTRBlips[1] = LoadPngFromResource("Radar.bundle.btr_up.png");
                BTRBlips[2] = LoadPngFromResource("Radar.bundle.btr_down.png");

                MineBlips[0] = LoadPngFromResource("Radar.bundle.mine.png");
                MineBlips[1] = LoadPngFromResource("Radar.bundle.mine_up.png");
                MineBlips[2] = LoadPngFromResource("Radar.bundle.mine_down.png");

                Radar.Log.LogInfo("Assets loaded from folder!");
                Loaded = true;
            }
            catch (Exception e)
            {
                Radar.Log.LogError($"Error loading assets: {e}");
            }
        }
    }
}
