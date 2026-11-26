using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ButterflyEffect.Guidance
{
    /// <summary>Places the existing dialogue card in the player's world and enables XR UI rays.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class GuidanceVRPanel : MonoBehaviour
    {
        public Camera playerCamera;
        public GuidanceView view;
        [Min(1f)] public float distance = 2.5f;
        public float verticalOffset = -.65f;
        [Min(.5f)] public float width = 2.1f;
        [Min(1f)] public float turnThreshold = 25f;
        [Min(.05f)] public float movementThreshold = .35f;
        [Min(.1f)] public float followSpeed = 5f;

        public Canvas DialogueCanvas { get; private set; }
        private bool positioned;
        private bool following;
        private Vector3 lastHeading = Vector3.forward;

        private void Awake()
        {
            Configure(playerCamera, view);
        }

        /// <summary>Also called by scene authoring tools; keeps the saved card editable.</summary>
        public void Configure(Camera camera, GuidanceView guidanceView)
        {
            if (camera != null) playerCamera = camera;
            if (guidanceView != null) view = guidanceView;
            if (view == null) view = GetComponent<GuidanceView>();
            if (view == null) return;

            view.showDemoHeadings = false;
            view.choiceFontSize = 32;
            view.Build();
            DialogueCanvas = view.GetComponentInChildren<Canvas>(true);
            if (DialogueCanvas == null) return;
            foreach (var graphic in DialogueCanvas.GetComponentsInChildren<Graphic>(true))
                if (view.uiMaterial != null) graphic.material = view.uiMaterial;
            if (playerCamera == null) playerCamera = Camera.main;

            DialogueCanvas.renderMode = RenderMode.WorldSpace;
            DialogueCanvas.worldCamera = playerCamera;
            DialogueCanvas.sortingOrder = 100;
            var rect = (RectTransform)DialogueCanvas.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(1300f, 340f);
            rect.localScale = Vector3.one * (width / 1300f);

            var card = rect.Find("Butterfly dialogue card") as RectTransform;
            if (card != null)
            {
                card.anchorMin = Vector2.zero;
                card.anchorMax = Vector2.one;
                card.offsetMin = card.offsetMax = Vector2.zero;
            }
            var oldTitle = rect.Find("Location title");
            if (oldTitle != null) oldTitle.gameObject.SetActive(false);

            var scaler = DialogueCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                scaler.dynamicPixelsPerUnit = 1f;
            }
            if (DialogueCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                DialogueCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            EnsureXRInput();
            view.ApplyNaturalStyle();
            if (view.IsExplorationMode) view.SetExplorationMode(true);
            SnapToPlayer();
        }

        private void EnsureXRInput()
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                foreach (var candidate in FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene != gameObject.scene) continue;
                    events = candidate;
                    if (candidate.GetComponent<XRUIInputModule>() != null) break;
                }
            }
            if (events == null)
                events = new GameObject("Butterfly UI EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

            var xrInput = events.GetComponent<XRUIInputModule>();
            if (xrInput == null) xrInput = events.gameObject.AddComponent<XRUIInputModule>();
            xrInput.enabled = true;
            // The XR module handles tracked pointers and desktop mouse input. Keeping another
            // active module on the same EventSystem makes controller selection unreliable.
            foreach (var module in events.GetComponents<BaseInputModule>())
                if (module != xrInput) module.enabled = false;
        }

        public void SnapToPlayer()
        {
            if (playerCamera == null || DialogueCanvas == null) return;
            DesiredPose(out var position, out var rotation);
            DialogueCanvas.transform.SetPositionAndRotation(position, rotation);
            positioned = true;
            following = false;
        }

        private void LateUpdate()
        {
            if (playerCamera == null || DialogueCanvas == null) return;
            if (!positioned) { SnapToPlayer(); return; }
            DesiredPose(out var position, out var rotation);
            Transform panel = DialogueCanvas.transform;
            float gap = Vector3.Distance(panel.position, position);
            float turn = Quaternion.Angle(panel.rotation, rotation);
            if (gap > movementThreshold || turn > turnThreshold) following = true;
            if (!following) return;
            // A small dead zone avoids attaching every head movement directly to the panel.
            float amount = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            panel.position = Vector3.Lerp(panel.position, position, amount);
            panel.rotation = Quaternion.Slerp(panel.rotation, rotation, amount);
            if (gap < .02f && turn < 1f) following = false;
        }

        private void DesiredPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 heading = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up);
            if (heading.sqrMagnitude > .001f) lastHeading = heading.normalized;
            rotation = Quaternion.LookRotation(lastHeading, Vector3.up);
            position = playerCamera.transform.position + lastHeading * distance + Vector3.up * verticalOffset;
        }
    }
}
