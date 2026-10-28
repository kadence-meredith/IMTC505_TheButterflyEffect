using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ButterflyEffect.Prototype.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string Root = "Assets/MahmoudPrototype";
        private const string ScenePath = Root + "/Generated/Scenes/Mahmoud_InteractionPrototype.unity";
        private static Material wood, white, blue, orange, coal, grass, metal, red, cyan;
        private static Mesh sphereMesh;

        [MenuItem("Tools/Butterfly Effect/Create or Open Interaction Prototype")]
        public static void CreateOrOpen()
        {
            if (!RequireEditMode()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            CreateScene();
        }

        [MenuItem("Tools/Butterfly Effect/Repair Existing Prototype")]
        public static void RepairExisting()
        {
            if (!RequireEditMode() || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = SceneManager.GetActiveScene();
            DesktopPlayer player = FindPlayer(scene);
            if (player == null)
            {
                if (!File.Exists(ScenePath)) { CreateScene(); return; }
                scene = EditorSceneManager.OpenScene(ScenePath);
                player = FindPlayer(scene);
            }
            if (player == null) { Debug.LogError("No Desktop Player was found in the prototype scene."); return; }

            string backup = Root + "/Generated/Backups/" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            EnsureFolder(backup);
            if (!string.IsNullOrEmpty(scene.path)) AssetDatabase.CopyAsset(scene.path, backup + "/" + Path.GetFileName(scene.path));
            string prefabFolder = Root + "/Generated/Prefabs";
            if (AssetDatabase.IsValidFolder(prefabFolder))
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.CopyAsset(path, backup + "/" + Path.GetFileName(path));
                    GameObject contents = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        RepairLabels(contents);
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                }
            foreach (GameObject root in scene.GetRootGameObjects()) RepairLabels(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player.gameObject;
            Debug.Log("Prototype repaired. Press Play: the starting cigarette stays in your hand while you walk. The previous generated scene and prefabs were copied to " + backup);
        }

        private static DesktopPlayer FindPlayer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                DesktopPlayer player = root.GetComponentInChildren<DesktopPlayer>(true);
                if (player != null) return player;
            }
            return null;
        }

        private static bool RequireEditMode()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            Debug.LogWarning("Stop Play mode before creating or repairing the prototype.");
            return false;
        }

        private static void RepairLabels(GameObject root)
        {
            foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true))
            {
                WorldLabel label = text.GetComponent<WorldLabel>();
                if (label == null) label = text.gameObject.AddComponent<WorldLabel>();
                label.ApplyReadableDefaults();
                EditorUtility.SetDirty(text); EditorUtility.SetDirty(label); EditorUtility.SetDirty(text.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text.transform);
            }
        }

        private static void CreateScene()
        {
            EnsureFolder(Root + "/Generated/Scenes"); EnsureFolder(Root + "/Generated/Prefabs"); EnsureFolder(Root + "/Generated/Materials");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            wood = MaterialAsset("Wood", new Color(0.32f, 0.18f, 0.1f));
            white = MaterialAsset("White", new Color(0.94f, 0.93f, 0.85f));
            blue = MaterialAsset("WaterBlue", new Color(0.05f, 0.45f, 0.9f));
            orange = MaterialAsset("FlameOrange", new Color(1f, 0.35f, 0.04f), true);
            coal = MaterialAsset("Coal", new Color(0.08f, 0.09f, 0.1f));
            grass = MaterialAsset("DryGrass", new Color(0.63f, 0.56f, 0.26f));
            metal = MaterialAsset("Metal", new Color(0.42f, 0.47f, 0.5f));
            red = MaterialAsset("FireworkRed", new Color(0.82f, 0.08f, 0.11f));
            cyan = MaterialAsset("GuideCyan", new Color(0.2f, 0.9f, 1f), true);
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(sphere);
            GameObject environment = New("Prototype Environment");
            Shape("Ground", PrimitiveType.Cube, environment.transform, new Vector3(0, -0.1f, 2), new Vector3(28, 0.2f, 18), MaterialAsset("Floor", new Color(0.17f, 0.26f, 0.25f)), true);
            for (int i = 0; i < 5; i++)
                Shape("Back panel", PrimitiveType.Cube, environment.transform, new Vector3(-10 + i * 5, 1.15f, 7.5f), new Vector3(4.8f, 2.3f, 0.15f), wood, true);
            GameObject sun = New("Directional Light"); Light light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f; light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.67f);
            RenderSettings.fog = false;
            GameObject sessionObject = New("Prototype Session"); sessionObject.AddComponent<PrototypeSession>();
            New("XR Interaction Manager").AddComponent<XRInteractionManager>();

            GameObject cigaretteStation = CigaretteStation(out CigaretteItem cigarette);
            GameObject fireStation = CampfireStation();
            GameObject bucketStation = BucketStation();
            GameObject fireworksStation = FireworksStation();
            SaveStation(cigaretteStation); SaveStation(fireStation); SaveStation(bucketStation); SaveStation(fireworksStation);

            GameObject playerObject = New("Desktop Player");
            CharacterController motor = playerObject.AddComponent<CharacterController>(); motor.height = 1.8f; motor.radius = 0.28f; motor.center = new Vector3(0, 0.9f, 0); motor.stepOffset = 0.25f;
            DesktopPlayer player = playerObject.AddComponent<DesktopPlayer>();
            GameObject cameraObject = New("Main Camera", playerObject.transform); cameraObject.tag = "MainCamera"; cameraObject.transform.localPosition = Vector3.up * 1.6f;
            Camera camera = cameraObject.AddComponent<Camera>(); camera.fieldOfView = 65f; camera.nearClipPlane = 0.04f; camera.farClipPlane = 100;
            camera.backgroundColor = new Color(0.3f, 0.55f, 0.65f); camera.clearFlags = CameraClearFlags.SolidColor; cameraObject.AddComponent<AudioListener>();
            player.view = camera; player.startingCigarette = cigarette;
            GameObject guide = New("Throw Trajectory"); player.throwGuide = Line(guide, cyan, 0.018f); player.throwGuide.enabled = false;
            Vector3[] positions = { new Vector3(-7, 0.05f, -0.2f), new Vector3(0, 0.05f, 1.15f), new Vector3(3.8f, 0.05f, 0), new Vector3(8, 0.05f, 0) };
            player.stationSpawns = new Transform[4];
            for (int i = 0; i < 4; i++) { GameObject spawn = New("Station " + (i + 1) + " arrival", environment.transform); spawn.transform.position = positions[i]; player.stationSpawns[i] = spawn.transform; }
            playerObject.transform.position = positions[0]; cameraObject.transform.localRotation = Quaternion.Euler(8, 0, 0);
            sessionObject.AddComponent<PrototypeHud>().player = player;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Selection.activeGameObject = playerObject;
            Debug.Log("Butterfly Effect prototype created. Open the Game tab and press Play. Controls are visible on screen. Scene: " + ScenePath);
        }

        private static GameObject CigaretteStation(out CigaretteItem cigarette)
        {
            GameObject root = New("01_CigaretteStation"); root.transform.position = new Vector3(-7, 0, 2);
            Table(root.transform, new Vector3(1.3f, 0, -0.2f));
            FireTarget patch = Fire(root.transform, new Vector3(0, 0.02f, -0.5f), FireSource.CigaretteGrass, 1.6f);
            Label(root.transform, new Vector3(0, 2.5f, 0.7f), "1 / CIGARETTE\nDrop, recover, or dispose", 0.08f);
            GameObject bin = New("Cigarette Disposal Bin", root.transform); bin.transform.localPosition = new Vector3(-1.3f, 0, 0);
            Shape("Bin base", PrimitiveType.Cube, bin.transform, new Vector3(0, 0.35f, 0), new Vector3(0.8f, 0.7f, 0.8f), blue, true);
            Shape("Left rim", PrimitiveType.Cube, bin.transform, new Vector3(-0.35f, 0.86f, 0), new Vector3(0.1f, 0.35f, 0.8f), blue, true);
            Shape("Right rim", PrimitiveType.Cube, bin.transform, new Vector3(0.35f, 0.86f, 0), new Vector3(0.1f, 0.35f, 0.8f), blue, true);
            Shape("Back rim", PrimitiveType.Cube, bin.transform, new Vector3(0, 0.86f, 0.35f), new Vector3(0.6f, 0.35f, 0.1f), blue, true);
            GameObject zone = New("Disposal Zone", bin.transform); zone.transform.localPosition = new Vector3(0, 1.0f, 0);
            BoxCollider trigger = zone.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(0.64f, 0.65f, 0.64f); zone.AddComponent<DisposalBin>();
            Label(bin.transform, new Vector3(0, 1.65f, 0), "DISPOSE HERE\nRelease inside the blue bin", 0.045f);
            GameObject item = New("Lit Cigarette", root.transform); item.transform.localPosition = new Vector3(1.3f, 1.06f, -0.2f);
            Shape("Cigarette", PrimitiveType.Cylinder, item.transform, Vector3.zero, new Vector3(0.045f, 0.14f, 0.045f), white).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Shape("Filter", PrimitiveType.Cylinder, item.transform, new Vector3(0, 0, -0.11f), new Vector3(0.047f, 0.04f, 0.047f), wood).transform.localRotation = Quaternion.Euler(90, 0, 0);
            GameObject tip = Shape("Glowing Tip", PrimitiveType.Sphere, item.transform, new Vector3(0, 0, 0.145f), Vector3.one * 0.055f, orange);
            cigarette = Item<CigaretteItem>(item, new Vector3(0.09f, 0.09f, 0.33f), Vector3.zero);
            cigarette.itemName = "lit cigarette"; cigarette.glowingTip = tip; cigarette.grassFire = patch;
            cigarette.holdOffset = new Vector3(0.26f, -0.28f, 0.72f);
            return root;
        }

        private static GameObject CampfireStation()
        {
            GameObject root = New("02_CampfireAndMarshmallowStation"); root.transform.position = new Vector3(0, 0, 3);
            FireTarget campfire = Fire(root.transform, Vector3.zero, FireSource.Campfire, 1.1f);
            Label(root.transform, new Vector3(0, 2.65f, 0.5f), "2 / CAMPFIRE & MARSHMALLOW\nLight, roast, eat, repeat", 0.07f);
            Table(root.transform, new Vector3(-1.7f, 0, -1.25f));
            Label(root.transform, new Vector3(1.6f, 1.4f, 0.3f), "FIRE BAN\nLighting a fire is an unsafe choice", 0.046f);
            GameObject leaveObject = Shape("Leave Campfire Unlit", PrimitiveType.Cube, root.transform,
                new Vector3(1.5f, 0.75f, -0.8f), new Vector3(0.6f, 0.24f, 0.45f), blue, true);
            AddButton(leaveObject, "F: leave the campfire unlit", campfire.LeaveUnlit);
            Label(root.transform, new Vector3(1.5f, 1.15f, -0.8f), "LEAVE UNLIT / F", 0.032f);
            GameObject lighterObject = New("Lighter", root.transform); lighterObject.transform.localPosition = new Vector3(-2.1f, 1.07f, -1.25f);
            Shape("Lighter body", PrimitiveType.Cube, lighterObject.transform, Vector3.zero, new Vector3(0.14f, 0.23f, 0.08f), red);
            Shape("Lighter head", PrimitiveType.Cube, lighterObject.transform, new Vector3(0, 0.14f, 0), new Vector3(0.14f, 0.06f, 0.08f), metal);
            LighterItem lighter = Item<LighterItem>(lighterObject, new Vector3(0.16f, 0.32f, 0.11f), new Vector3(0, 0.02f, 0)); lighter.itemName = "lighter";
            lighter.tip = New("Ignition Tip", lighterObject.transform).transform; lighter.tip.localPosition = Vector3.up * 0.2f;
            GameObject stick = New("Marshmallow Stick", root.transform); stick.transform.localPosition = new Vector3(-1.4f, 1.1f, -1.1f);
            Shape("Stick", PrimitiveType.Cylinder, stick.transform, new Vector3(0, 0, 0.27f), new Vector3(0.03f, 0.37f, 0.03f), wood).transform.localRotation = Quaternion.Euler(90, 0, 0);
            GameObject marshmallow = Shape("Marshmallow", PrimitiveType.Cylinder, stick.transform, new Vector3(0, 0, 0.63f), new Vector3(0.17f, 0.1f, 0.17f), white);
            marshmallow.transform.localRotation = Quaternion.Euler(90, 0, 0);
            MarshmallowItem roast = Item<MarshmallowItem>(stick, new Vector3(0.19f, 0.2f, 0.86f), new Vector3(0, 0, 0.28f));
            roast.itemName = "marshmallow stick"; roast.roastPoint = marshmallow.transform; roast.marshmallow = marshmallow.GetComponent<Renderer>(); roast.holdOffset = new Vector3(0.22f, -0.24f, 0.7f);
            return root;
        }

        private static GameObject BucketStation()
        {
            GameObject root = New("03_WaterBucketStation"); root.transform.position = new Vector3(3.8f, 0, 2);
            Table(root.transform, Vector3.zero);
            Label(root.transform, new Vector3(0, 2.5f, 0.6f), "3 / WATER BUCKET\nPut out flames AND cool embers", 0.067f);
            GameObject tank = Shape("Refill Tank", PrimitiveType.Cylinder, root.transform, new Vector3(0.75f, 0.75f, 0.95f), new Vector3(0.6f, 0.75f, 0.6f), blue, true);
            Label(root.transform, new Vector3(0.75f, 1.9f, 0.95f), "REFILL\nF while holding bucket", 0.04f);
            GameObject item = New("Water Bucket", root.transform); item.transform.localPosition = new Vector3(0, 1.22f, 0);
            Shape("Bucket body", PrimitiveType.Cylinder, item.transform, Vector3.zero, new Vector3(0.4f, 0.22f, 0.4f), metal);
            GameObject water = Shape("Water surface", PrimitiveType.Cylinder, item.transform, new Vector3(0, 0.226f, 0), new Vector3(0.35f, 0.008f, 0.35f), blue);
            Shape("Left handle", PrimitiveType.Cube, item.transform, new Vector3(-0.24f, 0.25f, 0), new Vector3(0.03f, 0.35f, 0.03f), coal);
            Shape("Right handle", PrimitiveType.Cube, item.transform, new Vector3(0.24f, 0.25f, 0), new Vector3(0.03f, 0.35f, 0.03f), coal);
            Shape("Handle grip", PrimitiveType.Cube, item.transform, new Vector3(0, 0.42f, 0), new Vector3(0.5f, 0.04f, 0.04f), coal);
            WaterBucketItem bucket = Item<WaterBucketItem>(item, new Vector3(0.5f, 0.65f, 0.45f), new Vector3(0, 0.1f, 0)); bucket.itemName = "water bucket";
            bucket.refillPoint = tank.transform; bucket.waterSurface = water.transform; bucket.holdOffset = new Vector3(0.32f, -0.36f, 0.9f);
            bucket.spout = New("Pour Spout", item.transform).transform; bucket.spout.localPosition = new Vector3(0, 0.25f, 0.24f);
            GameObject streamObject = New("Water Stream", item.transform); bucket.stream = Line(streamObject, blue, 0.06f); bucket.stream.positionCount = 2; bucket.stream.enabled = false;
            return root;
        }

        private static GameObject FireworksStation()
        {
            GameObject root = New("04_FireworksStation"); root.transform.position = new Vector3(8, 0, 2.5f);
            Label(root.transform, new Vector3(0, 2.5f, 0.5f), "4 / CANADA DAY FIREWORKS\nLaunch, look up, try another", 0.068f);
            Shape("Fireworks Box", PrimitiveType.Cube, root.transform, new Vector3(0, 0.45f, 0), new Vector3(0.95f, 0.9f, 0.8f), red, true);
            FireworkStation station = root.AddComponent<FireworkStation>();
            GameObject rocket = Shape("Rocket", PrimitiveType.Cylinder, root.transform, new Vector3(0, 1f, 0), new Vector3(0.15f, 0.28f, 0.15f), white);
            station.rocket = rocket.transform; station.trail = rocket.AddComponent<TrailRenderer>(); station.trail.sharedMaterial = cyan;
            station.trail.time = 0.6f; station.trail.startWidth = 0.08f; station.trail.endWidth = 0.01f;
            GameObject burstObject = New("Firework Burst", root.transform); ParticleSystem burst = burstObject.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burst.main; main.playOnAwake = false; main.loop = false; main.duration = 0.15f; main.startLifetime = 2.4f; main.startSpeed = 4f; main.startSize = 0.09f; main.gravityModifier = 0.25f; main.maxParticles = 100;
            var emission = burst.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });
            var shape = burst.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.05f;
            var renderer = burst.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = sphereMesh;
            renderer.sharedMaterial = MaterialAsset("FireworkParticles", Color.white, false, true);
            station.burst = burst; burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            station.grassFire = Fire(root.transform, new Vector3(1.35f, 0.02f, 0), FireSource.FireworksGrass, 0.9f);
            GameObject buttonObject = Shape("Launch Button", PrimitiveType.Cube, root.transform, new Vector3(0, 0.95f, -0.65f), new Vector3(0.45f, 0.2f, 0.4f), orange, true);
            AddButton(buttonObject, "F: light the fireworks   |   Or leave them unlit", station.Launch);
            Label(root.transform, new Vector3(0, 1.4f, -0.65f), "LAUNCH / F", 0.04f);
            GameObject leaveObject = Shape("Leave Fireworks Unlit", PrimitiveType.Cube, root.transform,
                new Vector3(-1.1f, 0.7f, -0.4f), new Vector3(0.6f, 0.24f, 0.45f), blue, true);
            AddButton(leaveObject, "F: leave the fireworks unlit", station.LeaveUnlit);
            Label(root.transform, new Vector3(-1.1f, 1.15f, -0.4f), "LEAVE UNLIT / F", 0.032f);
            return root;
        }

        private static FireTarget Fire(Transform parent, Vector3 position, FireSource source, float radius)
        {
            GameObject fireObject = New(source.ToString(), parent); fireObject.transform.localPosition = position;
            FireTarget fire = fireObject.AddComponent<FireTarget>(); fire.source = source; fire.catchRadius = radius;
            Shape("Ground patch", PrimitiveType.Cylinder, fireObject.transform, new Vector3(0, 0.02f, 0), new Vector3(radius * 1.7f, 0.02f, radius * 1.7f), source == FireSource.Campfire ? coal : grass);
            if (source == FireSource.Campfire)
                for (int i = 0; i < 3; i++)
                {
                    GameObject log = Shape("Log", PrimitiveType.Cylinder, fireObject.transform, new Vector3(0, 0.16f + i * 0.035f, 0), new Vector3(0.18f, 0.5f, 0.18f), wood);
                    log.transform.localRotation = Quaternion.Euler(90, i * 60, 0);
                }
            fire.embers = New("Hot Embers", fireObject.transform);
            for (int i = 0; i < 5; i++)
                Shape("Ember", PrimitiveType.Sphere, fire.embers.transform,
                    new Vector3(Mathf.Cos(i * 1.26f) * 0.3f, 0.18f, Mathf.Sin(i * 1.26f) * 0.3f), new Vector3(0.18f, 0.1f, 0.18f), orange);
            fire.flames = New("Flames", fireObject.transform);
            for (int i = 0; i < 3; i++)
            {
                GameObject flame = Shape("Flame", PrimitiveType.Sphere, fire.flames.transform,
                    new Vector3((i - 1) * 0.19f, 0.5f + i * 0.08f, 0), new Vector3(0.3f, 0.8f - i * 0.09f, 0.3f), orange);
                flame.AddComponent<FlamePulse>();
            }
            fire.flames.SetActive(false); fire.embers.SetActive(false); return fire;
        }

        private static T Item<T>(GameObject root, Vector3 colliderSize, Vector3 colliderCentre) where T : PrototypeItem
        {
            BoxCollider collider = root.AddComponent<BoxCollider>(); collider.size = colliderSize; collider.center = colliderCentre;
            Rigidbody body = root.AddComponent<Rigidbody>(); body.mass = typeof(T) == typeof(WaterBucketItem) ? 1.5f : 0.15f;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            T item = root.AddComponent<T>();
            XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>(); grab.colliders.Add(collider); grab.throwOnDetach = true; grab.useDynamicAttach = true;
            root.AddComponent<XrItemBridge>(); return item;
        }

        private static void Table(Transform parent, Vector3 position)
        {
            GameObject table = New("Table", parent); table.transform.localPosition = position;
            Shape("Top", PrimitiveType.Cube, table.transform, new Vector3(0, 0.9f, 0), new Vector3(1.7f, 0.12f, 0.85f), wood, true);
            for (int i = 0; i < 4; i++) Shape("Leg", PrimitiveType.Cube, table.transform,
                new Vector3(i < 2 ? -0.65f : 0.65f, 0.43f, i % 2 == 0 ? -0.28f : 0.28f), new Vector3(0.1f, 0.86f, 0.1f), coal, true);
        }

        private static void AddButton(GameObject target, string prompt, UnityEngine.Events.UnityAction action)
        {
            PrototypeButton button = target.AddComponent<PrototypeButton>(); button.prompt = prompt;
            UnityEventTools.AddPersistentListener(button.onPressed, action);
            target.AddComponent<XRSimpleInteractable>(); target.AddComponent<XrButtonBridge>();
        }

        private static GameObject New(string name, Transform parent = null)
        { GameObject go = new GameObject(name); if (parent != null) go.transform.SetParent(parent, false); return go; }
        private static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid = false)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        private static void Label(Transform parent, Vector3 position, string text, float characterSize)
        {
            GameObject go = New("Label", parent); go.transform.localPosition = position;
            TextMesh mesh = go.AddComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 64; mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = Color.white;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); mesh.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.AddComponent<WorldLabel>().ApplyReadableDefaults();
        }
        private static LineRenderer Line(GameObject go, Material material, float width)
        {
            LineRenderer line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.startWidth = width; line.endWidth = width;
            line.useWorldSpace = true; line.numCapVertices = 3; return line;
        }
        private static Material MaterialAsset(string name, Color colour, bool glow = false, bool particle = false)
        {
            string path = Root + "/Generated/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            Shader shader = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = name }; material.color = colour;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", colour * 1.8f); }
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void SaveStation(GameObject station)
        { PrefabUtility.SaveAsPrefabAssetAndConnect(station, Root + "/Generated/Prefabs/" + station.name + ".prefab", InteractionMode.AutomatedAction); }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
