using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
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
        private const string ZombieModelDir = "Assets/Models/Zombies";
        private const string RiggedDir = "Assets/Models/Zombies/Rigged";
        private const string PropDir = "Assets/Models/Props";
        private const string TowerModelDir = "Assets/Models/Towers";
        private const string TowerRiggedDir = "Assets/Models/Towers/Rigged";

        /// <summary>A weapon held rigidly in a hand. Rotation and offset are relative to the character, set at the rest pose.</summary>
        private struct HandProp
        {
            public string Enemy, Prop, Bone;
            public Vector3 Euler, Offset;
            public HandProp(string enemy, string prop, string bone, Vector3 euler, Vector3 offset)
            { Enemy = enemy; Prop = prop; Bone = bone; Euler = euler; Offset = offset; }
        }

        // Props have their origin at the grip, long axis up (+Y) and front facing +Z.
        private static readonly HandProp[] HandProps =
        {
            new HandProp("ArmoredRevenant", "Sword", "RightHand", new Vector3(115f, 0f, 0f), Vector3.zero),
            new HandProp("ArmoredRevenant", "Shield", "LeftHand", new Vector3(0f, -25f, 0f), new Vector3(-0.06f, 0f, 0.05f)),
            new HandProp("Clubber", "Club", "RightHand", new Vector3(100f, 60f, 0f), Vector3.zero),
            new HandProp("ZombieKing", "Scepter", "RightHand", new Vector3(10f, 0f, 0f), Vector3.zero),
            new HandProp("KnightBastion", "Axe", "RightHand", new Vector3(20f, 0f, 0f), Vector3.zero),
            new HandProp("ArcherTower", "Bow", "LeftHand", new Vector3(0f, 90f, 0f), Vector3.zero),
            new HandProp("MageSpire", "Staff", "RightHand", new Vector3(5f, 0f, 0f), Vector3.zero),
        };
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

            // A matching prop model (the knight's axe) replaces the sphere's look; the sphere stays as the collider.
            GameObject propModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/{name}.fbx");
            if (propModel != null)
            {
                go.GetComponent<Renderer>().enabled = false;
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(propModel);
                visual.name = "Visual";
                foreach (Collider c in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (Animator a in visual.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
                Material propMaterial = GetOrCreateZombieMaterial(name, PropDir);
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = propMaterial;
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = Vector3.one / size;
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // head of the axe leads the flight
            }

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

            // Prefer the rigged model; fall back to the static one, then to a sphere.
            string riggedPath = $"{RiggedDir}/{assetName}.fbx";
            bool rigged = ConfigureRiggedImport(riggedPath);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(rigged ? riggedPath : $"{ZombieModelDir}/{assetName}.fbx");
            GameObject go;
            float centreHeight;
            if (model != null)
            {
                go = BuildEnemyFromModel(assetName, model, out centreHeight);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = assetName;
                go.transform.localScale = Vector3.one * size;
                go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(assetName, color);
                centreHeight = size / 2f;
            }

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            Enemy enemy = go.AddComponent<Enemy>();
            SetReference(enemy, "stats", stats);
            SerializedObject serialized = new SerializedObject(enemy);
            serialized.FindProperty("heightAbovePath").floatValue = centreHeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<Enemy>(go, assetName);
        }

        /// <summary>
        /// Root at the body's centre with a capsule collider; the imported model is a child with its feet on the path.
        /// Models are exported from Blender with the origin at the feet, facing +Z.
        /// </summary>
        private static GameObject BuildEnemyFromModel(string assetName, GameObject model, out float centreHeight)
        {
            GameObject go = new GameObject(assetName);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Model";
            visual.transform.SetParent(go.transform, false);

            // A rig regenerated from its own model brings its own textures; otherwise share the static model's.
            string modelDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(model)).Replace('\\', '/');
            string textureDir = File.Exists($"{modelDir}/{assetName}_BaseColor.png") ? modelDir : ZombieModelDir;
            Material material = GetOrCreateZombieMaterial(assetName, textureDir);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            // The static model carries the designed in-game height; scale a rig to match it.
            float height = ModelHeight(visual);
            GameObject staticModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{ZombieModelDir}/{assetName}.fbx");
            if (staticModel != null && staticModel != model)
            {
                GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(staticModel);
                float target = ModelHeight(probe);
                Object.DestroyImmediate(probe);
                if (height > 0.01f && Mathf.Abs(target - height) > 0.01f)
                {
                    visual.transform.localScale *= target / height;
                    height = ModelHeight(visual);
                }
            }

            centreHeight = height / 2f;
            // Put the lowest point of the mesh on the path, wherever the model's own origin is.
            float feet = ModelBounds(visual).min.y - visual.transform.position.y;
            visual.transform.localPosition = new Vector3(0f, -centreHeight - feet, 0f);
            Animator animator = visual.GetComponent<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = GetOrCreateUnitController(assetName, AssetDatabase.GetAssetPath(model));
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            visual.AddComponent<ProceduralAnimator>();

            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = height;
            capsule.radius = Mathf.Min(height * 0.35f, height / 2f);
            capsule.center = Vector3.zero;

            if (animator != null) AttachHandProps(assetName, visual.transform);
            return go;
        }

        /// <summary>Parents each listed prop to its hand bone so it moves rigidly instead of being skinned.</summary>
        private static void AttachHandProps(string assetName, Transform modelRoot)
        {
            foreach (HandProp entry in HandProps)
            {
                if (entry.Enemy != assetName) continue;
                GameObject propModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/{entry.Prop}.fbx");
                Transform hand = FindDeep(modelRoot, entry.Bone);
                if (propModel == null || hand == null)
                {
                    Debug.LogWarning($"[TowerDefense] {assetName}: missing prop {entry.Prop} or bone {entry.Bone}");
                    continue;
                }

                // Grip sits in the palm: halfway from the wrist to the middle-finger knuckle.
                Transform knuckle = FindDeep(hand, entry.Bone + "Middle1");
                Vector3 palm = knuckle != null ? Vector3.Lerp(hand.position, knuckle.position, 0.5f) : hand.position;

                GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(propModel);
                prop.name = entry.Prop;
                foreach (Collider c in prop.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (Animator a in prop.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
                Material material = GetOrCreateZombieMaterial(entry.Prop, PropDir);
                foreach (Renderer r in prop.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;

                Transform character = modelRoot.parent != null ? modelRoot.parent : modelRoot;
                prop.transform.SetPositionAndRotation(palm + character.TransformDirection(entry.Offset),
                    character.rotation * Quaternion.Euler(entry.Euler));
                prop.transform.SetParent(hand, true);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static float ModelHeight(GameObject model) => ModelBounds(model).size.y;

        private static Bounds ModelBounds(GameObject model)
        {
            Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Bounds rendererBounds = RestPoseBounds(renderer);
                if (first) { bounds = rendererBounds; first = false; }
                else bounds.Encapsulate(rendererBounds);
            }
            return bounds;
        }

        /// <summary>World bounds of the mesh as modelled. A skinned renderer's own bounds also cover its animation poses.</summary>
        private static Bounds RestPoseBounds(Renderer renderer)
        {
            if (!(renderer is SkinnedMeshRenderer skinned) || skinned.sharedMesh == null) return renderer.bounds;
            Bounds local = skinned.sharedMesh.bounds;
            Bounds world = new Bounds(skinned.transform.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                world.Encapsulate(skinned.transform.TransformPoint(corner));
            }
            return world;
        }

        /// <summary>Sets a rigged zombie FBX to Generic with Walk (looping), Hit and Die clips. False if there is no rigged FBX.</summary>
        private static bool ConfigureRiggedImport(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return false;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;

            var clips = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation source in importer.defaultClipAnimations)
            {
                string name = System.Array.Find(ClipStates, state => source.takeName.EndsWith(state));
                if (name == null) continue;
                source.name = name;
                source.loopTime = name == "Walk" || name == "Idle";
                source.lockRootPositionXZ = true;
                clips.Add(source);
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
            return true;
        }

        // Clip names the Blender scripts export. Walk and Idle loop; Hit and Attack return to them when done.
        private static readonly string[] ClipStates = { "Walk", "Idle", "Hit", "Attack", "Die" };

        private static AnimatorController GetOrCreateUnitController(string assetName, string modelPath)
        {
            // Reuse the asset so its GUID (and every prefab reference to it) stays stable across rebuilds.
            string path = $"{Path.GetDirectoryName(modelPath).Replace('\\', '/')}/{assetName}.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);

            var clips = new Dictionary<string, AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) clips[clip.name] = clip;

            var states = new Dictionary<string, AnimatorState>();
            foreach (string state in ClipStates)
            {
                if (!clips.TryGetValue(state, out AnimationClip clip)) continue;
                AnimatorState animatorState = machine.AddState(state);
                animatorState.motion = clip;
                states[state] = animatorState;
            }
            AnimatorState home = states.TryGetValue("Walk", out AnimatorState walk) ? walk
                : states.TryGetValue("Idle", out AnimatorState idle) ? idle : null;
            if (home != null)
            {
                machine.defaultState = home;
                foreach (string oneShot in new[] { "Hit", "Attack" })
                {
                    if (!states.TryGetValue(oneShot, out AnimatorState shot)) continue;
                    AnimatorStateTransition back = shot.AddTransition(home);
                    back.hasExitTime = true;
                    back.exitTime = 0.9f;
                    back.duration = 0.1f;
                }
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static Material GetOrCreateZombieMaterial(string assetName, string textureDir)
        {
            string path = $"{textureDir}/{assetName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", LoadTexture($"{textureDir}/{assetName}_BaseColor.png", false));
            material.SetColor("_BaseColor", Color.white);

            Texture2D normal = LoadTexture($"{textureDir}/{assetName}_Normal.png", true);
            material.SetTexture("_BumpMap", normal);
            if (normal != null) material.EnableKeyword("_NORMALMAP");

            Texture2D emission = LoadTexture($"{textureDir}/{assetName}_Emission.png", false);
            material.SetTexture("_EmissionMap", emission);
            if (emission != null)
            {
                material.SetColor("_EmissionColor", Color.white * 2.5f);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
            }

            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(string path, bool normalMap)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            TextureImporterType wanted = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != wanted)
            {
                importer.textureType = wanted;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

            GameObject structureModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{TowerModelDir}/{assetName}_Structure.fbx");
            string unitPath = $"{TowerRiggedDir}/{assetName}.fbx";
            if (structureModel != null && ConfigureRiggedImport(unitPath))
                return BuildModelledTower(root, assetName, stats, projectile, structureModel, unitPath);

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

        private static readonly Dictionary<string, float> TowerUnitHeights = new Dictionary<string, float>
        {
            { "KnightBastion", 1.4f }, { "ArcherTower", 1.4f }, { "MageSpire", 1.5f },
        };

        /// <summary>
        /// A static structure built to the stats height with a rigged defender standing on top.
        /// Only the defender (under Head) turns to aim, like a Bloons tower.
        /// </summary>
        private static Tower BuildModelledTower(GameObject root, string assetName, TowerStats stats, Projectile projectile, GameObject structureModel, string unitPath)
        {
            GameObject column = (GameObject)PrefabUtility.InstantiatePrefab(structureModel);
            column.name = "Column";
            column.transform.SetParent(root.transform, false);
            Material structureMaterial = GetOrCreateZombieMaterial($"{assetName}_Structure", TowerModelDir);
            foreach (Renderer r in column.GetComponentsInChildren<Renderer>()) r.sharedMaterial = structureMaterial;
            foreach (Animator a in column.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
            Bounds structureBounds = ModelBounds(column);
            BoxCollider box = column.AddComponent<BoxCollider>();
            box.center = column.transform.InverseTransformPoint(structureBounds.center);
            box.size = structureBounds.size;

            GameObject head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = Vector3.up * stats.height;

            GameObject unitModel = AssetDatabase.LoadAssetAtPath<GameObject>(unitPath);
            GameObject unit = (GameObject)PrefabUtility.InstantiatePrefab(unitModel);
            unit.name = "Unit";
            unit.transform.SetParent(head.transform, false);
            Material unitMaterial = GetOrCreateZombieMaterial(assetName, TowerRiggedDir);
            foreach (Renderer r in unit.GetComponentsInChildren<Renderer>()) r.sharedMaterial = unitMaterial;
            float unitHeight = TowerUnitHeights.TryGetValue(assetName, out float h) ? h : 1.4f;
            float current = ModelHeight(unit);
            if (current > 0.01f) unit.transform.localScale *= unitHeight / current;
            float feet = ModelBounds(unit).min.y - unit.transform.position.y;
            unit.transform.localPosition = new Vector3(0f, -feet, 0f);

            Animator animator = unit.GetComponent<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = GetOrCreateUnitController(assetName, unitPath);
                animator.applyRootMotion = false;
            }
            AttachHandProps(assetName, unit.transform);

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(head.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, unitHeight * 0.7f, 0.45f);

            Tower tower = root.AddComponent<Tower>();
            SetReference(tower, "stats", stats);
            SetReference(tower, "projectilePrefab", projectile);
            SetReference(tower, "column", column.transform);
            SetReference(tower, "head", head.transform);
            SetReference(tower, "muzzle", muzzle.transform);
            SerializedObject serialized = new SerializedObject(tower);
            serialized.FindProperty("sizeColumnFromStats").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

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
