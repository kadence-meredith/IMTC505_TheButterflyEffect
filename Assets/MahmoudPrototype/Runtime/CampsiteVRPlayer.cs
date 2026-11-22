using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace ButterflyEffect.Prototype
{
    // Add to the Starter Assets XR Origin. Uses the existing campsite gameplay scripts.
    public sealed class CampsiteVRPlayer : MonoBehaviour
    {
        [Tooltip("The campsite cigarette. If empty, the active scene is searched once at startup.")]
        public CigaretteItem startingCigarette;
        [Tooltip("Near-Far Interactor under Camera Offset / Right Controller.")]
        public XRBaseInteractor rightHandInteractor;
        [Tooltip("Dynamic Move Provider on the XR rig's Locomotion / Move object.")]
        public ContinuousMoveProvider moveProvider;
        public bool startWithCigarette = true;
        [Min(0f)] public float normalMoveSpeed = 2.2f;
        [Min(0f)] public float cigaretteMoveSpeed = 1.1f;

        private bool ownsStartingSelection;
        private bool sawGripPress;
        private float previousMoveSpeed;
        private bool capturedMoveSpeed;

        private void Awake()
        {
            if (moveProvider == null) moveProvider = GetComponentInChildren<ContinuousMoveProvider>(true);
            if (moveProvider != null)
            {
                previousMoveSpeed = moveProvider.moveSpeed;
                capturedMoveSpeed = true;
            }
        }

        private IEnumerator Start()
        {
            if (!startWithCigarette) yield break;
            if (startingCigarette == null) startingCigarette = FindFirstObjectByType<CigaretteItem>();
            if (startingCigarette == null || startingCigarette.IsDisposed) yield break;
            if (rightHandInteractor == null)
            {
                Debug.LogError("Assign the right controller's Near-Far Interactor to CampsiteVRPlayer.", this);
                yield break;
            }
            XRGrabInteractable grab = startingCigarette.GetComponent<XRGrabInteractable>();
            if (grab == null)
            {
                Debug.LogError("The starting cigarette needs XR Grab Interactable and Xr Item Bridge.", this);
                yield break;
            }

            // Let the controller, interaction manager, and item bridges finish initializing.
            yield return null;
            while (isActiveAndEnabled && startingCigarette != null && !startingCigarette.IsDisposed)
            {
                InputDevice hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                bool tracked = hand.isValid && hand.TryGetFeatureValue(CommonUsages.isTracked, out bool tracking) && tracking;
                if (!tracked || !rightHandInteractor.isActiveAndEnabled ||
                    rightHandInteractor.interactionManager == null || rightHandInteractor.hasSelection ||
                    !grab.isActiveAndEnabled || grab.isSelected)
                { yield return null; continue; }

                Transform attachment = rightHandInteractor.attachTransform != null
                    ? rightHandInteractor.attachTransform : rightHandInteractor.transform;
                Vector3 position = attachment.position + attachment.forward * 0.08f;
                startingCigarette.transform.SetPositionAndRotation(position, attachment.rotation);
                Rigidbody body = startingCigarette.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.position = position; body.rotation = attachment.rotation;
                    if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                }
                Physics.SyncTransforms();
                PrototypeSession session = PrototypeSession.Instance;
                int actions = session != null ? session.state.totalActions : 0;
                int pickups = session != null ? session.state.itemsPickedUp : 0;
                rightHandInteractor.StartManualInteraction((IXRSelectInteractable)grab);
                if (session != null) { session.state.totalActions = actions; session.state.itemsPickedUp = pickups; }

                if (rightHandInteractor.isPerformingManualInteraction && grab.isSelected)
                {
                    ownsStartingSelection = true;
                    sawGripPress = hand.TryGetFeatureValue(CommonUsages.gripButton, out bool grip) && grip;
                    yield break;
                }
                if (rightHandInteractor.isPerformingManualInteraction) rightHandInteractor.EndManualInteraction();
                yield return null;
            }
        }

        private void Update()
        {
            bool carryingCigarette = startingCigarette != null && startingCigarette.IsHeld && startingCigarette.HeldInXR;
            if (moveProvider != null)
                moveProvider.moveSpeed = carryingCigarette ? Mathf.Min(normalMoveSpeed, cigaretteMoveSpeed) : normalMoveSpeed;

            if (!ownsStartingSelection || rightHandInteractor == null) return;
            if (!rightHandInteractor.isPerformingManualInteraction)
            { ownsStartingSelection = false; return; }
            InputDevice hand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!hand.isValid || !hand.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed)) return;
            if (pressed) sawGripPress = true;
            if (sawGripPress && !pressed)
            {
                // The first real squeeze-and-release ends the automatic starting hold.
                // Further pickup, drop, and throw behavior is handled normally by XRI.
                rightHandInteractor.EndManualInteraction();
                ownsStartingSelection = false;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (ownsStartingSelection && rightHandInteractor != null && rightHandInteractor.isPerformingManualInteraction)
                rightHandInteractor.EndManualInteraction();
            ownsStartingSelection = false;
            if (capturedMoveSpeed && moveProvider != null) moveProvider.moveSpeed = previousMoveSpeed;
        }
    }
}
