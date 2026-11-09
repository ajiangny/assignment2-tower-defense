using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.EditorTools
{
    /// <summary>Rebuilds the grey-box scene, prefabs, materials and stats from primitives.</summary>
    public static class GreyboxSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PrefabDir = "Assets/Prefabs";
        private const string MaterialDir = "Assets/Materials";
        private const string DataDir = "Assets/Data";
        private const string WaveDir = "Assets/Data/Waves";
        private const int WaveCount = 20;

        private static readonly Vector3[] PathPoints =
        {
            new Vector3(-18f, 0f, -12f),
            new Vector3(-6f, 0f, -12f),
            new Vector3(-6f, 0f, 8f),
            new Vector3(6f, 0f, 8f),
            new Vector3(6f, 0f, -8f),
            new Vector3(18f, 0f, -8f),
        };

        [MenuItem("Tower Defense/Build Greybox Scene")]
        public static void Build()
        {
            foreach (string dir in new[] { PrefabDir, MaterialDir, DataDir, WaveDir, "Assets/Scenes" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            Material groundMat = GetOrCreateMaterial("Ground", new Color(0.32f, 0.42f, 0.30f));
            Material pathMat = GetOrCreateMaterial("Path", new Color(0.55f, 0.47f, 0.35f));
            Material previewMat = GetOrCreateMaterial("Preview", Color.white);

            Tower[] towers =
            {
                BuildTower("KnightBastion", "Knight Bastion", 100, 2f, 14f, 2.5f, 9f, 2f, 0f, new Color(0.45f, 0.48f, 0.55f),
                    BuildProjectile("Axe", 0.35f, new Color(0.75f, 0.75f, 0.8f))),
                BuildTower("ArcherTower", "Archer Tower", 150, 1f, 35f, 7f, 20f, 5f, 0f, new Color(0.45f, 0.32f, 0.2f),
                    BuildProjectile("Arrow", 0.2f, new Color(0.9f, 0.8f, 0.5f))),
                BuildTower("MageSpire", "Mage Spire", 225, 0.5f, 10f, 5f, 13f, 8f, 2.5f, new Color(0.4f, 0.25f, 0.7f),
                    BuildProjectile("Orb", 0.6f, new Color(0.4f, 0.8f, 1f))),
            };

            Enemy shambler = BuildEnemy("Shambler", 2.5f, 8f, 0f, 5, 1f, new Color(0.45f, 0.6f, 0.3f), null, 0);
            Enemy runner = BuildEnemy("Runner", 5.5f, 4f, 0f, 3, 0.7f, new Color(0.75f, 0.85f, 0.3f), null, 0);
            Enemy clubber = BuildEnemy("Clubber", 2f, 25f, 0f, 12, 1.4f, new Color(0.3f, 0.45f, 0.2f), null, 0);
            Enemy bloater = BuildEnemy("Plague Bloater", 2f, 15f, 0f, 8, 1.5f, new Color(0.6f, 0.55f, 0.15f), runner, 3);
            Enemy revenant = BuildEnemy("Armored Revenant", 2.2f, 30f, 2f, 15, 1.2f, new Color(0.5f, 0.35f, 0.3f), null, 0);
            Enemy king = BuildEnemy("Zombie King", 1.2f, 600f, 0f, 200, 2.5f, new Color(0.5f, 0.1f, 0.5f), null, 0);

            WaveDefinition[] waves = BuildWaves(shambler, runner, clubber, bloater, revenant, king);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4.4f, 1f, 3.2f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            EnemyPath path = BuildPath(pathMat);

            GameObject systems = new GameObject("Game");
            systems.AddComponent<GameManager>();
            systems.AddComponent<Economy>();
            WaveSpawner spawner = systems.AddComponent<WaveSpawner>();
            spawner.Configure(waves, path);
            TowerPlacer placer = systems.AddComponent<TowerPlacer>();
            placer.Configure(towers, ground.GetComponent<Collider>(), path, previewMat);
            systems.AddComponent<Hud>().Configure(placer, spawner);

            Camera camera = Camera.main;
            camera.transform.position = new Vector3(0f, 34f, -24f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[TowerDefense] Built {ScenePath}");
        }

        private static EnemyPath BuildPath(Material material)
        {
            GameObject root = new GameObject("EnemyPath");
            EnemyPath path = root.AddComponent<EnemyPath>();

            Transform[] waypoints = new Transform[PathPoints.Length];
            for (int i = 0; i < PathPoints.Length; i++)
            {
                GameObject waypoint = new GameObject($"Waypoint {i}");
                waypoint.transform.SetParent(root.transform);
                waypoint.transform.position = PathPoints[i];
                waypoints[i] = waypoint.transform;
            }
            path.SetWaypoints(waypoints);

            // Flat strips with no colliders, so they don't intercept placement clicks or shots.
            for (int i = 0; i < PathPoints.Length - 1; i++)
            {
                Vector3 from = PathPoints[i];
                Vector3 to = PathPoints[i + 1];
                GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = $"Path Strip {i}";
                Object.DestroyImmediate(strip.GetComponent<Collider>());
                strip.transform.SetParent(root.transform);
                strip.transform.position = (from + to) / 2f + Vector3.up * 0.02f;
                strip.transform.rotation = Quaternion.LookRotation(to - from);
                strip.transform.localScale = new Vector3(2f, 0.02f, Vector3.Distance(from, to) + 2f);
                strip.GetComponent<Renderer>().sharedMaterial = material;
            }
            return path;
        }

        private static WaveDefinition[] BuildWaves(Enemy shambler, Enemy runner, Enemy clubber, Enemy bloater, Enemy revenant, Enemy king)
        {
            WaveDefinition[] waves = new WaveDefinition[WaveCount];
            for (int n = 1; n <= WaveCount; n++)
            {
                List<WaveDefinition.Group> groups = new List<WaveDefinition.Group>();
                float interval = Mathf.Lerp(1.4f, 0.6f, (n - 1f) / (WaveCount - 1f));

                groups.Add(Group(shambler, 4 + n, interval, 0f));
                if (n >= 3) groups.Add(Group(runner, n - 1, interval, 3f));
                if (n >= 5) groups.Add(Group(clubber, n / 3, interval * 2f, 6f));
                if (n >= 8) groups.Add(Group(bloater, (n - 6) / 2, interval * 2f, 8f));
                if (n >= 11) groups.Add(Group(revenant, (n - 8) / 2, interval * 2f, 10f));
                if (n == WaveCount) groups.Add(Group(king, 1, 1f, 12f));

                WaveDefinition wave = GetOrCreateAsset<WaveDefinition>($"{WaveDir}/Wave{n:00}.asset");
                wave.groups = groups.ToArray();
                wave.completionGold = 40 + n * 10;
                EditorUtility.SetDirty(wave);
                waves[n - 1] = wave;
            }
            return waves;
        }

        private static WaveDefinition.Group Group(Enemy enemy, int count, float interval, float startDelay)
        {
            return new WaveDefinition.Group { enemy = enemy, count = count, interval = interval, startDelay = startDelay };
        }

        private static Projectile BuildProjectile(string name, float size, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(name, color);

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<Projectile>();

            return SavePrefab<Projectile>(go, name);
        }

        private static Enemy BuildEnemy(string displayName, float speed, float hp, float armor, int gold, float size, Color color, Enemy splitInto, int splitCount)
        {
            string assetName = displayName.Replace(" ", "");
            EnemyStats stats = GetOrCreateAsset<EnemyStats>($"{DataDir}/{assetName}.asset");
            stats.displayName = displayName;
            stats.speed = speed;
            stats.maxHp = hp;
            stats.armor = armor;
            stats.goldReward = gold;
            stats.splitInto = splitInto;
            stats.splitCount = splitCount;
            EditorUtility.SetDirty(stats);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = assetName;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(assetName, color);

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            Enemy enemy = go.AddComponent<Enemy>();
            SetReference(enemy, "stats", stats);
            SerializedObject serialized = new SerializedObject(enemy);
            serialized.FindProperty("heightAbovePath").floatValue = size / 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<Enemy>(go, assetName);
        }

        private static Tower BuildTower(string assetName, string displayName, int cost, float rate, float projectileSpeed, float height, float range, float damage, float splash, Color color, Projectile projectile)
        {
            TowerStats stats = GetOrCreateAsset<TowerStats>($"{DataDir}/{assetName}.asset");
            stats.displayName = displayName;
            stats.cost = cost;
            stats.shotsPerSecond = rate;
            stats.projectileSpeed = projectileSpeed;
            stats.height = height;
            stats.range = range;
            stats.damage = damage;
            stats.splashRadius = splash;
            EditorUtility.SetDirty(stats);

            Material material = GetOrCreateMaterial(assetName, color);
            GameObject root = new GameObject(assetName);

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Column";
            column.transform.SetParent(root.transform);
            column.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            column.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(column.GetComponent<CapsuleCollider>());
            column.AddComponent<BoxCollider>();

            GameObject head = new GameObject("Head");
            head.transform.SetParent(root.transform);

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            Object.DestroyImmediate(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(head.transform);
            barrel.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            barrel.transform.localScale = new Vector3(0.35f, 0.35f, 1.4f);
            barrel.GetComponent<Renderer>().sharedMaterial = material;

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(head.transform);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 1.4f);

            Tower tower = root.AddComponent<Tower>();
            SetReference(tower, "stats", stats);
            SetReference(tower, "projectilePrefab", projectile);
            SetReference(tower, "column", column.transform);
            SetReference(tower, "head", head.transform);
            SetReference(tower, "muzzle", muzzle.transform);

            return SavePrefab<Tower>(root, assetName);
        }

        private static T SavePrefab<T>(GameObject instance, string name) where T : Component
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<T>();
        }

        private static void SetReference(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
