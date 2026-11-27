using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace ButterflyEffect.Prototype
{
    [RequireComponent(typeof(XRSimpleInteractable), typeof(PrototypeButton))]
    public sealed class XrButtonBridge : MonoBehaviour, IXRSelectFilter
    {
        private XRSimpleInteractable interactable;
        private PrototypeButton button;
        private void Awake() { interactable = GetComponent<XRSimpleInteractable>(); button = GetComponent<PrototypeButton>(); }
        public bool canProcess => isActiveAndEnabled;
        private void OnEnable()
        {
            if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
            if (button == null) button = GetComponent<PrototypeButton>();
            interactable.selectFilters.Add(this);
            interactable.selectEntered.AddListener(Pressed);
        }
        private void OnDisable()
        {
            interactable.selectFilters.Remove(this);
            interactable.selectEntered.RemoveListener(Pressed);
        }
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable target)
            => PrototypeInteractionPolicy.CanPress(button);
        private void Pressed(SelectEnterEventArgs args) { button.Press(); }
    }
}
