using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ButterflyEffect.Prototype
{
    // Desktop and XR inputs invoke the same gameplay methods. Requires XRI 3.x.
    [RequireComponent(typeof(XRGrabInteractable), typeof(PrototypeItem))]
    public sealed class XrItemBridge : MonoBehaviour
    {
        private XRGrabInteractable grab;
        private PrototypeItem item;
        private bool desktopOwnsItem;
        private bool previousGrabEnabled;
        private void Awake() { grab = GetComponent<XRGrabInteractable>(); item = GetComponent<PrototypeItem>(); }
        private void OnEnable()
        {
            grab.selectEntered.AddListener(Selected); grab.selectExited.AddListener(Released);
            grab.activated.AddListener(Activated);
        }
        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(Selected); grab.selectExited.RemoveListener(Released);
            grab.activated.RemoveListener(Activated);
        }
        private void Selected(SelectEnterEventArgs args) { item.Grabbed(true); }
        private void Released(SelectExitEventArgs args) { if (item.HeldInXR) item.Released(); }
        private void Activated(ActivateEventArgs args) { item.Use(); }

        // A prop has one pose owner. XRI is temporarily disabled while the desktop player holds it.
        public bool TryAcquireDesktopControl()
        {
            if (desktopOwnsItem) return true;
            if (grab.isSelected) return false;
            previousGrabEnabled = grab.enabled;
            desktopOwnsItem = true;
            grab.enabled = false;
            return true;
        }

        public void ReleaseDesktopControl()
        {
            if (!desktopOwnsItem) return;
            desktopOwnsItem = false;
            grab.enabled = previousGrabEnabled;
        }
    }
}
