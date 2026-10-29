using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ButterflyEffect.Prototype
{
    [RequireComponent(typeof(XRSimpleInteractable), typeof(PrototypeButton))]
    public sealed class XrButtonBridge : MonoBehaviour
    {
        private XRSimpleInteractable interactable;
        private PrototypeButton button;
        private void Awake() { interactable = GetComponent<XRSimpleInteractable>(); button = GetComponent<PrototypeButton>(); }
        private void OnEnable() { interactable.selectEntered.AddListener(Pressed); }
        private void OnDisable() { interactable.selectEntered.RemoveListener(Pressed); }
        private void Pressed(SelectEnterEventArgs args) { button.Press(); }
    }
}
