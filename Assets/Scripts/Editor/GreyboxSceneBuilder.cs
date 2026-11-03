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
            foreach (string dir in new[] { PrefabDir, MaterialDir, DataDir, "Assets/Scenes" })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();

            TowerStats towerStats = GetOrCreateAsset<TowerStats>($"{DataDir}/BasicTower.asset");
            EnemyStats enemyStats = GetOrCreateAsset<EnemyStats>($"{DataDir}/BasicEnemy.asset");

            Material groundMat = GetOrCreateMaterial("Ground", new Color(0.32f, 0.42f, 0.30f));
            Material pathMat = GetOrCreateMaterial("Path", new Color(0.55f, 0.47f, 0.35f));
            Material towerMat = GetOrCreateMaterial("Tower", new Color(0.35f, 0.45f, 0.70f));
            Material enemyMat = GetOrCreateMaterial("Enemy", new Color(0.75f, 0.25f, 0.20f));
            Material shotMat = GetOrCreateMaterial("Projectile", new Color(0.95f, 0.80f, 0.25f));

            Projectile projectilePrefab = BuildProjectilePrefab(shotMat);
            Tower towerPrefab = BuildTowerPrefab(towerStats, projectilePrefab, towerMat);
            Enemy enemyPrefab = BuildEnemyPrefab(enemyStats, enemyMat);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4.4f, 1f, 3.2f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            EnemyPath path = BuildPath(pathMat);

            GameObject systems = new GameObject("Game");
            systems.AddComponent<GameManager>();
            systems.AddComponent<WaveSpawner>().Configure(enemyPrefab, path);
            systems.AddComponent<TowerPlacer>().Configure(towerPrefab, ground.GetComponent<Collider>(), path);

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

        private static Projectile BuildProjectilePrefab(Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.localScale = Vector3.one * 0.35f;
            go.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<Projectile>();

            return SavePrefab<Projectile>(go, "Projectile");
        }

        private static Enemy BuildEnemyPrefab(EnemyStats stats, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Enemy";
            go.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            Enemy enemy = go.AddComponent<Enemy>();
            SetReference(enemy, "stats", stats);

            return SavePrefab<Enemy>(go, "Enemy");
        }

        private static Tower BuildTowerPrefab(TowerStats stats, Projectile projectile, Material material)
        {
            GameObject root = new GameObject("Tower");

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

            return SavePrefab<Tower>(root, "Tower");
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
