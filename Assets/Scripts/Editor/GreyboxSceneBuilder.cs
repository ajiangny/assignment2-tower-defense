using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private const string HeroModelDir = "Assets/Models/Hero";
        private const string TownDir = "Assets/Models/Town";
        private const string GroundTextureDir = "Assets/Textures/Ground";
        private static readonly string[] HeroLevels = { "HeroL1", "HeroL5", "HeroL10" };
        private const float HeroHeight = 1.5f;

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
            new HandProp("HeroL1", "HeroSword", "RightHand", new Vector3(55f, 0f, 0f), Vector3.zero),
            new HandProp("HeroL5", "HeroSword", "RightHand", new Vector3(55f, 0f, 0f), Vector3.zero),
            new HandProp("HeroL10", "HeroSword", "RightHand", new Vector3(55f, 0f, 0f), Vector3.zero),
        };
        private const int WaveCount = 20;

        // The road's corners, from the graveyard (off-screen left) to the town gate (right).
        // Corners are rounded by RoundCorners, so zombies walk smooth bends.
        private static readonly Vector3[] RoadCorners =
        {
            new Vector3(-24f, 0f, 9f),
            new Vector3(-12f, 0f, 9f),
            new Vector3(-12f, 0f, -8f),
            new Vector3(-1f, 0f, -8f),
            new Vector3(-1f, 0f, 7f),
            new Vector3(9f, 0f, 7f),
            new Vector3(9f, 0f, -5f),
            new Vector3(20f, 0f, -5f),
        };
        private const float RoadCornerRadius = 3f;
        private const float RoadWidth = 2.6f;

        private static readonly Vector3[] PathPoints = RoundCorners(RoadCorners, RoadCornerRadius, 5);

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

            string[] towerNames = System.Array.ConvertAll(towers, t => t.name);

            Enemy shambler = BuildEnemy("Shambler", 2.5f, 8f, 0f, 5, 1f, new Color(0.45f, 0.6f, 0.3f), null, 0);
            Enemy runner = BuildEnemy("Runner", 5.5f, 4f, 0f, 3, 0.7f, new Color(0.75f, 0.85f, 0.3f), null, 0);
            Enemy clubber = BuildEnemy("Clubber", 2f, 25f, 0f, 12, 1.4f, new Color(0.3f, 0.45f, 0.2f), null, 0);
            Enemy bloater = BuildEnemy("Plague Bloater", 2f, 15f, 0f, 8, 1.5f, new Color(0.6f, 0.55f, 0.15f), runner, 3);
            Enemy revenant = BuildEnemy("Armored Revenant", 2.2f, 30f, 2f, 15, 1.2f, new Color(0.5f, 0.35f, 0.3f), null, 0);
            Enemy king = BuildEnemy("Zombie King", 1.2f, 600f, 0f, 200, 2.5f, new Color(0.5f, 0.1f, 0.5f), null, 0);

            BuildHeroPrefab();

            WaveDefinition[] waves = BuildWaves(shambler, runner, clubber, bloater, revenant, king);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(9f, 1f, 7f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            ApplyGroundTexture(groundMat, "Grass", new Vector2(7.5f, 5.8f));

            ApplyGroundTexture(pathMat, "Cobblestone", Vector2.one);
            EnemyPath path = BuildPath(pathMat);
            BuildTown();
            BuildAtmosphere();

            GameObject systems = new GameObject("Game");
            systems.AddComponent<GameManager>();
            systems.AddComponent<Economy>();
            WaveSpawner spawner = systems.AddComponent<WaveSpawner>();
            spawner.Configure(waves, path);
            TowerPlacer placer = systems.AddComponent<TowerPlacer>();
            placer.Configure(towers, ground.GetComponent<Collider>(), path, previewMat);
            systems.AddComponent<Hud>().Configure(placer, spawner, RenderTowerPortraits(towerNames));

            Camera camera = Camera.main;
            camera.transform.position = new Vector3(-1f, 30f, -27f);
            camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            camera.fieldOfView = 50f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

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

            // One cobblestone ribbon along the rounded path. No collider, so it doesn't intercept clicks or shots.
            GameObject road = new GameObject("Road");
            road.transform.SetParent(root.transform);
            road.AddComponent<MeshFilter>().sharedMesh = BuildRoadMesh(PathPoints, RoadWidth);
            road.AddComponent<MeshRenderer>().sharedMaterial = material;
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
        private static readonly string[] ClipStates = { "Walk", "Idle", "Hit", "Attack", "LevelUp", "Die" };

        private static bool ControllerMatches(AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips)
        {
            int expected = 0;
            foreach (string state in ClipStates) if (clips.ContainsKey(state)) expected++;
            if (expected == 0 || machine.states.Length != expected || machine.defaultState == null) return false;
            foreach (ChildAnimatorState child in machine.states)
            {
                if (!clips.TryGetValue(child.state.name, out AnimationClip clip) || child.state.motion != clip) return false;
            }
            return true;
        }

        private static AnimatorController GetOrCreateUnitController(string assetName, string modelPath)
        {
            // Reuse the asset so its GUID (and every prefab reference to it) stays stable across rebuilds.
            string path = $"{Path.GetDirectoryName(modelPath).Replace('\\', '/')}/{assetName}.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            var clips = new Dictionary<string, AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) clips[clip.name] = clip;

            // Leave an up-to-date controller untouched so rebuilds don't rewrite it.
            if (ControllerMatches(machine, clips)) return controller;
            foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);

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
                foreach (string oneShot in new[] { "Hit", "Attack", "LevelUp" })
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

        /// <summary>
        /// Hero prefab with one child per armour level (1, 5, 10). Level 1 is shown; hero code switches them.
        /// Not placed in the scene yet: the hero's gameplay isn't built.
        /// </summary>
        private static void BuildHeroPrefab()
        {
            GameObject root = new GameObject("Hero");
            bool any = false;
            foreach (string level in HeroLevels)
            {
                string path = $"{HeroModelDir}/{level}.fbx";
                if (!ConfigureRiggedImport(path)) continue;
                GameObject unit = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                unit.name = level;
                unit.transform.SetParent(root.transform, false);
                Material material = GetOrCreateZombieMaterial(level, HeroModelDir);
                foreach (Renderer r in unit.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
                float current = ModelHeight(unit);
                if (current > 0.01f) unit.transform.localScale *= HeroHeight / current;
                float feet = ModelBounds(unit).min.y - unit.transform.position.y;
                unit.transform.localPosition = new Vector3(0f, -feet, 0f);
                Animator animator = unit.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.runtimeAnimatorController = GetOrCreateUnitController(level, path);
                    animator.applyRootMotion = false;
                }
                AttachHandProps(level, unit.transform);
                unit.SetActive(!any);
                any = true;
            }

            if (any) PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Hero.prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>Replaces each interior corner with a circular arc of the given radius.</summary>
        private static Vector3[] RoundCorners(Vector3[] corners, float radius, int arcSteps)
        {
            var points = new List<Vector3> { corners[0] };
            for (int i = 1; i < corners.Length - 1; i++)
            {
                Vector3 inDir = (corners[i] - corners[i - 1]).normalized;
                Vector3 outDir = (corners[i + 1] - corners[i]).normalized;
                float r = Mathf.Min(radius, Vector3.Distance(corners[i], corners[i - 1]) / 2f, Vector3.Distance(corners[i], corners[i + 1]) / 2f);
                Vector3 a = corners[i] - inDir * r;
                Vector3 b = corners[i] + outDir * r;
                // Quadratic Bezier through the corner: close to a circular arc for right angles.
                for (int k = 0; k <= arcSteps; k++)
                {
                    float t = k / (float)arcSteps;
                    points.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * corners[i] + t * t * b);
                }
            }
            points.Add(corners[corners.Length - 1]);
            return points.ToArray();
        }

        private static Mesh BuildRoadMesh(Vector3[] points, float width)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float distance = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 forward = i == 0 ? points[1] - points[0]
                    : i == points.Length - 1 ? points[i] - points[i - 1]
                    : (points[i + 1] - points[i - 1]);
                forward.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, forward.normalized) * (width / 2f);
                if (i > 0) distance += Vector3.Distance(points[i], points[i - 1]);
                Vector3 lift = Vector3.up * 0.03f;
                vertices.Add(points[i] - side + lift);
                vertices.Add(points[i] + side + lift);
                float v = distance / width;
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
                if (i == 0) continue;
                int b = vertices.Count - 4;
                triangles.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = "Road" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            string path = "Assets/Models/Town/Road.asset";
            Directory.CreateDirectory("Assets/Models/Town");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void ApplyGroundTexture(Material material, string name, Vector2 tiling)
        {
            Texture2D baseMap = LoadTexture($"{GroundTextureDir}/{name}_BaseColor.jpg", false);
            if (baseMap == null) return;
            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetColor("_BaseColor", Color.white);
            Texture2D normal = LoadTexture($"{GroundTextureDir}/{name}_Normal.jpg", true);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", tiling);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
        }

        /// <summary>A town decoration: model name in Assets/Models/Town, ground position, yaw, and size multiplier.</summary>
        private struct TownPiece
        {
            public string Model; public float X, Z, Yaw, Scale;
            public TownPiece(string model, float x, float z, float yaw, float scale = 1f)
            { Model = model; X = x; Z = z; Yaw = yaw; Scale = scale; }
        }

        // Models face +Z at yaw 0. Houses and walls sit on the edges; the pockets inside the bends stay open for towers.
        private static readonly TownPiece[] TownLayout =
        {
            // Graveyard where the zombies rise (top-left). The road starts inside it and leaves through the gate.
            new TownPiece("CemeteryGate", -17.5f, 9f, 90f),
            new TownPiece("CemeteryFence", -23f, 13.8f, 0f), new TownPiece("CemeteryFence", -23f, 4.2f, 180f),
            new TownPiece("CemeteryFence", -26.2f, 9f, 90f),
            new TownPiece("Gravestones", -24f, 12.2f, 170f, 0.75f), new TownPiece("Gravestones", -20.5f, 12.1f, 200f, 0.7f),
            new TownPiece("Gravestones", -24f, 5.9f, 10f, 0.75f), new TownPiece("Gravestones", -20.5f, 5.8f, -15f, 0.7f),
            new TownPiece("DeadTree", -14.5f, 14f, 30f), new TownPiece("DeadTree", -24f, -1.5f, 200f, 1.15f),

            // Ruined houses around the edges.
            new TownPiece("HouseTimber", -5.5f, 13.5f, 180f), new TownPiece("HouseStone", 3.5f, 12.8f, 170f),
            new TownPiece("HouseTimber", 14.5f, 10.5f, 200f, 0.9f),
            new TownPiece("HouseStone", -19f, -11.5f, 160f), new TownPiece("HouseTimber", 3.5f, -14.5f, 190f, 0.95f),
            new TownPiece("HouseStone", 12.5f, -12.5f, 200f, 0.9f),

            // The broken town gate the zombies must not reach, with the wall either side.
            new TownPiece("TownGate", 18f, -5f, -90f),
            new TownPiece("TownWall", 18f, -11.5f, 90f), new TownPiece("TownWall", 18f, 1.5f, 90f),
            new TownPiece("TownWall", 18f, 8f, 90f, 0.95f),

            // Street clutter beside the road.
            new TownPiece("Well", 4f, 1f, 30f),
            new TownPiece("Cart", -7f, -12f, 75f), new TownPiece("Barrels", 13f, -1.5f, 40f),
            new TownPiece("Barrels", -15.5f, 1.5f, -30f, 0.9f), new TownPiece("Cart", 13.5f, 4.5f, -120f, 0.9f),
            new TownPiece("DeadTree", -18f, -5f, 120f, 0.85f),
            new TownPiece("Lantern", -14.2f, 6.8f, 0f), new TownPiece("Lantern", -9.8f, -5.8f, 0f),
            new TownPiece("Lantern", 1.2f, 0.5f, 0f), new TownPiece("Lantern", 6.8f, 9.2f, 180f),
            new TownPiece("Lantern", 11.2f, -2.8f, 0f), new TownPiece("Lantern", 15.8f, -7.2f, 0f),
        };

        private static void BuildTown()
        {
            GameObject town = new GameObject("Town");
            foreach (TownPiece piece in TownLayout)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{TownDir}/{piece.Model}.fbx");
                if (model == null) continue;
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name = piece.Model;
                go.transform.SetParent(town.transform, false);
                go.transform.SetPositionAndRotation(new Vector3(piece.X, 0f, piece.Z), Quaternion.Euler(0f, piece.Yaw, 0f));
                go.transform.localScale *= piece.Scale;
                go.isStatic = true;
                foreach (Animator a in go.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
                Material material = GetOrCreateZombieMaterial(piece.Model, TownDir);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;

                // Trigger footprint: blocks placement clicks and the placement check, not shots or zombies.
                Bounds bounds = ModelBounds(go);
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = go.transform.InverseTransformPoint(bounds.center);
                Vector3 size = go.transform.InverseTransformVector(bounds.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                go.AddComponent<Scenery>();

                if (piece.Model == "Lantern") AddLanternLight(go, bounds);
            }
        }

        private static void AddLanternLight(GameObject lantern, Bounds bounds)
        {
            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(lantern.transform, false);
            glow.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.5f, bounds.center.z);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.68f, 0.35f);
            light.range = 6f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
        }

        /// <summary>Dusk light, fog, and post-processing so emissive eyes and crystals bloom.</summary>
        private static void BuildAtmosphere()
        {
            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.82f, 0.66f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.42f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.5f, 0.55f, 0.6f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 110f;

            string profilePath = "Assets/Settings/MapVolume.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            Bloom bloom = GetOrAddOverride<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.6f);
            Tonemapping tonemapping = GetOrAddOverride<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = GetOrAddOverride<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(12f);
            color.saturation.Override(8f);
            Vignette vignette = GetOrAddOverride<Vignette>(profile);
            vignette.intensity.Override(0.28f);
            EditorUtility.SetDirty(profile);

            GameObject volumeObject = new GameObject("Post Processing");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        private const string PortraitDir = "Assets/UI/Portraits";

        /// <summary>Renders each tower's defender, head and shoulders, into a sprite for the HUD cards.</summary>
        private static Sprite[] RenderTowerPortraits(string[] towerNames)
        {
            Directory.CreateDirectory(PortraitDir);
            var sprites = new Sprite[towerNames.Length];
            for (int i = 0; i < towerNames.Length; i++)
            {
                string name = towerNames[i];
                string unitPath = $"{TowerRiggedDir}/{name}.fbx";
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(unitPath);
                if (model == null) continue;
                string pngPath = $"{PortraitDir}/{name}.png";
                RenderPortrait(model, GetOrCreateZombieMaterial(name, TowerRiggedDir), unitPath, name, pngPath);
                AssetDatabase.ImportAsset(pngPath);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
                if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            }
            return sprites;
        }

        private static void RenderPortrait(GameObject model, Material material, string modelPath, string assetName, string pngPath)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject unit = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                foreach (Renderer r in unit.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                    if (asset is AnimationClip clip && clip.name == "Idle") clip.SampleAnimation(unit, 0f);

                Bounds bounds = ModelBounds(unit);
                foreach (SkinnedMeshRenderer skinned in unit.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked = new Mesh();
                    skinned.BakeMesh(baked, true);
                    foreach (Vector3 v in baked.vertices) bounds.Encapsulate(skinned.transform.TransformPoint(v));
                    Object.DestroyImmediate(baked);
                }

                AddPreviewLight(preview, new Vector3(35f, -30f, 0f), 1.3f, new Color(1f, 0.95f, 0.88f));
                AddPreviewLight(preview, new Vector3(20f, 150f, 0f), 0.6f, new Color(0.7f, 0.78f, 1f));

                GameObject cameraObject = new GameObject("Portrait Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.scene = preview;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.fieldOfView = 24f;
                camera.nearClipPlane = 0.05f;
                float height = bounds.size.y;
                Vector3 focus = new Vector3(bounds.center.x, bounds.max.y - height * 0.36f, bounds.center.z);
                Vector3 direction = new Vector3(0.35f, 0.12f, 1f).normalized; // models face +Z
                camera.transform.position = focus + direction * height * 2.2f;
                camera.transform.LookAt(focus);

                const int size = 256;
                var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;
                File.WriteAllBytes(pngPath, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void AddPreviewLight(Scene scene, Vector3 euler, float intensity, Color color)
        {
            GameObject go = new GameObject("Light");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(euler);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
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
