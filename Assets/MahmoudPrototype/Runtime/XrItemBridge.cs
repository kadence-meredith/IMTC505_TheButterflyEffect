using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace ButterflyEffect.Prototype
{
    // Desktop and XR inputs invoke the same gameplay methods. Requires XRI 3.x.
    [RequireComponent(typeof(XRGrabInteractable), typeof(PrototypeItem))]
    public sealed class XrItemBridge : MonoBehaviour, IXRSelectFilter
    {
        private XRGrabInteractable grab;
        private PrototypeItem item;
        private bool desktopOwnsItem;
        private bool previousGrabEnabled;
        public bool canProcess => isActiveAndEnabled;
        private void Awake() { grab = GetComponent<XRGrabInteractable>(); item = GetComponent<PrototypeItem>(); }
        private void OnEnable()
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            if (item == null) item = GetComponent<PrototypeItem>();
            grab.selectFilters.Add(this);
            grab.selectEntered.AddListener(Selected); grab.selectExited.AddListener(Released);
            grab.activated.AddListener(Activated);
        }
        private void OnDisable()
        {
            grab.selectFilters.Remove(this);
            grab.selectEntered.RemoveListener(Selected); grab.selectExited.RemoveListener(Released);
            grab.activated.RemoveListener(Activated);
        }
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            // Keep an existing hold, while still checking a new interactor or a new pickup.
            if (grab != null && interactor != null)
                foreach (var selected in grab.interactorsSelecting)
                    if (ReferenceEquals(selected, interactor)) return true;
            return PrototypeInteractionPolicy.CanGrab(item);
        }

        private void Selected(SelectEnterEventArgs args)
        {
            if (PrototypeInteractionPolicy.CanGrab(item)) item.Grabbed(true);
        }
        private void Released(SelectExitEventArgs args) { if (item.HeldInXR) item.Released(); }
        private void Activated(ActivateEventArgs args) { if (PrototypeInteractionPolicy.CanUse(item)) item.Use(); }

        // A prop has one pose owner. XRI is temporarily disabled while the desktop player holds it.
        public bool TryAcquireDesktopControl()
        {
            if (!PrototypeInteractionPolicy.CanGrab(item)) return false;
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
