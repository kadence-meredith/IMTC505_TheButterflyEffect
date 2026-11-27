using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ButterflyEffect.Prototype.Tests
{
    public sealed class DesktopCarryTests
    {
        private GameObject playerObject, itemObject, station, managerObject;
        private TerrainData testTerrain;
        private Mesh testMat;
        private DesktopPlayer player;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("Carry test manager"); managerObject.AddComponent<XRInteractionManager>();
            playerObject = new GameObject("Carry test player");
            playerObject.AddComponent<CharacterController>();
            player = playerObject.AddComponent<DesktopPlayer>(); player.inputEnabled = false;
            GameObject cameraObject = new GameObject("Carry test camera"); cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.6f, 0); camera = cameraObject.AddComponent<Camera>(); player.view = camera;
            station = new GameObject("Original item station");
        }

        private T MakeItem<T>() where T : PrototypeItem
        {
            itemObject = new GameObject("Carry test item"); itemObject.transform.SetParent(station.transform, false);
            itemObject.AddComponent<BoxCollider>();
            Rigidbody body = itemObject.AddComponent<Rigidbody>(); body.interpolation = RigidbodyInterpolation.Interpolate;
            T item = itemObject.AddComponent<T>();
            itemObject.AddComponent<XRGrabInteractable>(); itemObject.AddComponent<XrItemBridge>(); return item;
        }

        [UnityTest]
        public IEnumerator HeldPropFollowsWalkingAndLookingAcrossFrames()
        {
            PrototypeItem item = MakeItem<PrototypeItem>(); player.Grab(item);
            for (int i = 0; i < 12; i++)
            {
                playerObject.transform.position = new Vector3(i * 0.23f, 0, i * -0.18f);
                playerObject.transform.rotation = Quaternion.Euler(0, i * 13f, 0);
                camera.transform.localRotation = Quaternion.Euler(i * 3f, 0, 0);
                yield return null; // Allow the actual LateUpdate and physics loop to run.
                Vector3 expected = camera.transform.TransformPoint(item.holdOffset);
                Assert.That(Vector3.Distance(item.transform.position, expected), Is.LessThan(0.002f));
                Assert.That(Vector3.Distance(item.Body.position, expected), Is.LessThan(0.002f));
                Assert.That(item.transform.parent, Is.EqualTo(station.transform));
            }
        }

        [UnityTest]
        public IEnumerator DropRestoresPhysicsAndXrAfterExclusiveDesktopHold()
        {
            PrototypeItem item = MakeItem<PrototypeItem>();
            XRGrabInteractable xr = item.GetComponent<XRGrabInteractable>();
            player.Grab(item); yield return null;
            Assert.That(item.Body.isKinematic, Is.True);
            Assert.That(item.Body.interpolation, Is.EqualTo(RigidbodyInterpolation.None));
            Assert.That(item.Body.detectCollisions, Is.False);
            Assert.That(xr.enabled, Is.False);
            player.DropHeld(false);
            Assert.That(item.Body.isKinematic, Is.False);
            Assert.That(item.Body.useGravity, Is.True);
            Assert.That(item.Body.detectCollisions, Is.True);
            Assert.That(item.Body.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));
            Assert.That(xr.enabled, Is.True);
            Assert.That(item.IsHeld, Is.False);
            Assert.That(player.Held, Is.Null);
        }

        [UnityTest]
        public IEnumerator BucketCanTipWithoutLosingItsCarryPosition()
        {
            WaterBucketItem item = MakeItem<WaterBucketItem>();
            GameObject spout = new GameObject("Spout"); spout.transform.SetParent(item.transform, false); item.spout = spout.transform;
            GameObject water = new GameObject("Stream"); water.transform.SetParent(item.transform, false); item.stream = water.AddComponent<LineRenderer>(); item.stream.positionCount = 2;
            player.Grab(item); item.DesktopTilt(true);
            float until = Time.time + 0.5f;
            while (Time.time < until)
            {
                playerObject.transform.position += new Vector3(0.01f, 0, 0.02f);
                yield return null;
                Assert.That(Vector3.Distance(item.transform.position, camera.transform.TransformPoint(item.holdOffset)), Is.LessThan(0.002f));
            }
            Assert.That(Quaternion.Angle(item.DesktopHoldRotation, Quaternion.Euler(item.holdEuler)), Is.GreaterThan(45f));
            Assert.That(item.IsHeld, Is.True);
        }

        [UnityTest]
        public IEnumerator LookingDownOnlyPoursWhileDesktopPourIsHeld()
        {
            WaterBucketItem bucket = MakeItem<WaterBucketItem>();
            bucket.spout = new GameObject("Spout").transform;
            bucket.spout.SetParent(bucket.transform, false);
            camera.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            player.Grab(bucket);

            yield return new WaitForSeconds(0.25f);
            Assert.That(bucket.waterLeft, Is.EqualTo(bucket.capacity));
            bucket.DesktopTilt(true);
            yield return new WaitForSeconds(0.45f);
            Assert.That(bucket.waterLeft, Is.LessThan(bucket.capacity));

            bucket.DesktopTilt(false);
            float remaining = bucket.waterLeft;
            yield return new WaitForSeconds(0.25f);
            Assert.That(bucket.waterLeft, Is.EqualTo(remaining));
        }

        private GameObject MakeFlatTerrain()
        {
            var ground = new GameObject("Test terrain");
            ground.transform.SetParent(station.transform, false);
            ground.transform.position = new Vector3(-4f, 0f, -4f);
            testTerrain = new TerrainData { heightmapResolution = 33, size = new Vector3(8f, 1f, 8f) };
            ground.AddComponent<TerrainCollider>().terrainData = testTerrain;
            Physics.SyncTransforms();
            return ground;
        }

        [UnityTest]
        public IEnumerator PouringOnCampsiteTerrainCreatesAMissingWetGrassReceiver()
        {
            GameObject ground = MakeFlatTerrain();
            WaterBucketItem bucket = MakeItem<WaterBucketItem>();
            bucket.spout = bucket.transform;
            player.Grab(bucket);
            Physics.SyncTransforms();
            bucket.DesktopTilt(true);
            yield return new WaitForSeconds(0.15f);
            Assert.That(bucket.waterLeft, Is.LessThan(bucket.capacity));
            Assert.That(ground.GetComponent<WetGrass>(), Is.Null);

            ground.AddComponent<CampsiteGround>();
            yield return new WaitForSeconds(0.15f);
            var patch = ground.GetComponentInChildren<MeshRenderer>();
            Assert.That(patch, Is.Not.Null);
            Assert.That(patch.enabled && patch.gameObject.activeInHierarchy, Is.True);
            Assert.That(patch.sharedMaterial.shader.name, Is.EqualTo("Butterfly Effect/Wet Grass"));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.That(patch.sharedMaterial.shader.isSupported, Is.True);
            var appearance = new MaterialPropertyBlock();
            patch.GetPropertyBlock(appearance);
            Assert.That(appearance.GetFloat("_WetAmount"), Is.GreaterThan(0f));

            bucket.DesktopTilt(false);
            yield return null;
            bucket.DesktopTilt(true);
            yield return new WaitForSeconds(0.15f);
            Assert.That(ground.GetComponents<WetGrass>(), Has.Length.EqualTo(1));
            Assert.That(ground.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PourAtMatEdgeWetsOnlyTheTopOfTheMarkedMesh()
        {
            var root = new GameObject("Camp mat");
            root.transform.SetParent(station.transform, false);
            root.transform.position = new Vector3(-0.8f, 0f, 1.4f);
            root.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
            root.transform.localScale = new Vector3(2f, 0.5f, 2.5f);
            var mat = new GameObject("Mat mesh");
            mat.transform.SetParent(root.transform, false);
            testMat = new Mesh
            {
                vertices = new[] {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0.5f, -0.2f, 0.5f), new Vector3(0.5f, -0.2f, -0.5f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3, 3, 2, 4, 3, 4, 5 }
            };
            testMat.RecalculateNormals();
            mat.AddComponent<MeshFilter>().sharedMesh = testMat;
            mat.AddComponent<MeshRenderer>();
            var collider = mat.AddComponent<MeshCollider>();
            collider.sharedMesh = testMat;
            Physics.SyncTransforms();

            Vector3 aim = mat.transform.TransformPoint(new Vector3(0.47f, 0f, 0f));
            camera.transform.LookAt(aim);
            WaterBucketItem bucket = MakeItem<WaterBucketItem>();
            bucket.spout = bucket.transform;
            player.Grab(bucket);
            bucket.DesktopTilt(true);
            yield return new WaitForSeconds(0.2f);
            Assert.That(bucket.waterLeft, Is.LessThan(bucket.capacity));
            Assert.That(mat.GetComponent<WetGrass>(), Is.Null);

            root.AddComponent<CampsiteGround>();
            yield return new WaitForSeconds(0.2f);
            Assert.That(mat.GetComponents<WetGrass>(), Has.Length.EqualTo(1));
            Assert.That(root.GetComponent<WetGrass>(), Is.Null);
            Assert.That(mat.transform.childCount, Is.EqualTo(1));
            MeshFilter patch = mat.transform.GetChild(0).GetComponent<MeshFilter>();
            Assert.That(patch.sharedMesh.vertexCount, Is.GreaterThanOrEqualTo(3));
            float outermost = float.NegativeInfinity;
            foreach (Vector3 vertex in patch.sharedMesh.vertices)
            {
                Vector3 local = mat.transform.InverseTransformPoint(patch.transform.TransformPoint(vertex));
                Assert.That(local.x, Is.InRange(-0.501f, 0.501f));
                Assert.That(local.z, Is.InRange(-0.501f, 0.501f));
                Assert.That(local.y, Is.InRange(-0.001f, 0.04f));
                outermost = Mathf.Max(outermost, local.x);
            }
            Assert.That(outermost, Is.EqualTo(0.5f).Within(0.005f));
        }

        [UnityTest]
        public IEnumerator DesktopPourFollowsTheReticleAndStopsAtAnObstacleInItsPath()
        {
            GameObject ground = MakeFlatTerrain();
            ground.AddComponent<CampsiteGround>();
            Vector3 aim = new Vector3(0f, 0f, 1f);
            camera.transform.LookAt(aim);

            WaterBucketItem bucket = MakeItem<WaterBucketItem>();
            bucket.spout = bucket.transform;
            bucket.stream = itemObject.AddComponent<LineRenderer>();
            bucket.stream.useWorldSpace = true;
            bucket.holdOffset = camera.transform.InverseTransformPoint(new Vector3(0.65f, 1f, 0.8f));
            player.Grab(bucket);

            var obstacle = new GameObject("Block beside reticle").AddComponent<BoxCollider>();
            obstacle.transform.SetParent(station.transform, false);
            obstacle.transform.position = new Vector3(0.33f, 0.75f, 0.90f);
            obstacle.size = new Vector3(0.23f, 0.28f, 0.34f);
            Physics.SyncTransforms();
            Ray sight = new Ray(camera.transform.position, camera.transform.forward);
            Assert.That(obstacle.Raycast(sight, out _, 3.5f), Is.False);

            bucket.DesktopTilt(true);
            yield return new WaitForSeconds(0.2f);
            Assert.That(bucket.stream.enabled, Is.True);
            Vector3 blocked = bucket.stream.GetPosition(bucket.stream.positionCount - 1);
            Assert.That(Vector3.Distance(blocked, obstacle.ClosestPoint(blocked)), Is.LessThan(0.005f));
            Assert.That(ground.GetComponentInChildren<MeshRenderer>(), Is.Null);

            obstacle.enabled = false;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Vector3 contact = bucket.stream.GetPosition(bucket.stream.positionCount - 1);
            Assert.That(Vector3.Distance(contact, aim), Is.LessThan(0.04f));
            var patch = ground.GetComponentInChildren<MeshRenderer>();
            Assert.That(patch, Is.Not.Null);
            Vector3 wetCenter = Vector3.ProjectOnPlane(patch.bounds.center, Vector3.up);
            Assert.That(Vector3.Distance(wetCenter, aim), Is.LessThan(0.04f));
        }

        [Test]
        public void BucketPlacementClearsTheGroundAndAvoidsObstacles()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(station.transform, false);
            ground.transform.position = new Vector3(0f, -0.1f, 1f);
            ground.transform.localScale = new Vector3(8f, 0.2f, 8f);

            WaterBucketItem bucket = MakeItem<WaterBucketItem>();
            BoxCollider box = bucket.GetComponent<BoxCollider>();
            box.size = new Vector3(0.4f, 0.6f, 0.4f);
            box.center = new Vector3(0f, 0.1f, 0f);
            player.Grab(bucket);
            Physics.SyncTransforms();

            Assert.That(bucket.TryGetPlacementPose(player.transform, out Pose front), Is.True);
            Assert.That(Vector3.Angle(front.rotation * Vector3.up, Vector3.up), Is.LessThan(0.1f));
            float bottom = front.position.y + box.center.y - box.size.y * 0.5f;
            Assert.That(bottom, Is.InRange(0f, 0.02f));

            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.SetParent(station.transform, false);
            obstacle.transform.position = new Vector3(front.position.x, 0.5f, front.position.z);
            obstacle.transform.localScale = new Vector3(0.35f, 1f, 0.35f);
            Physics.SyncTransforms();
            Assert.That(bucket.TryGetPlacementPose(player.transform, out Pose alternate), Is.True);
            Assert.That(Vector3.Distance(front.position, alternate.position), Is.GreaterThan(0.38f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (player != null) player.DropHeld(false);
            Object.Destroy(playerObject); Object.Destroy(itemObject); Object.Destroy(station); Object.Destroy(managerObject);
            if (testTerrain != null) Object.Destroy(testTerrain);
            if (testMat != null) Object.Destroy(testMat);
            yield return null;
        }
    }
}
