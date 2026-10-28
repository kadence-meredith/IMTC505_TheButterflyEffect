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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (player != null) player.DropHeld(false);
            Object.Destroy(playerObject); Object.Destroy(itemObject); Object.Destroy(station); Object.Destroy(managerObject);
            yield return null;
        }
    }
}
