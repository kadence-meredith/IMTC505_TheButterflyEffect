using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ButterflyEffect.Guidance
{
    /// <summary>Desktop controls for the isolated test scene. Never part of the reusable guide prefab.</summary>
    public sealed class ButterflyGuideDemoPlayer : MonoBehaviour
    {
        public ButterflyPointGuide guide;
        public GuidanceDirector dialogueDirector;
        public Transform view;
        public float walkSpeed = 2.5f;
        public float mouseSensitivity = .12f;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Quaternion initialViewRotation;
        private float pitch;

        private void Awake()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            if (view != null) initialViewRotation = view.localRotation;
        }

        private void Update()
        {
            Vector2 movement = Vector2.zero;
            Vector2 look = Vector2.zero;
            bool restart = false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                restart = keyboard.rKey.wasPressedThisFrame;
            }
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                look = Mouse.current.delta.ReadValue() * mouseSensitivity;
#else
            movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (Input.GetMouseButton(1)) look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2;
            restart = Input.GetKeyDown(KeyCode.R);
#endif
            movement = Vector2.ClampMagnitude(movement, 1);
            transform.position += (transform.right * movement.x + transform.forward * movement.y) * (walkSpeed * Time.deltaTime);
            transform.Rotate(0, look.x, 0, Space.World);
            pitch = Mathf.Clamp(pitch - look.y, -60, 65);
            if (view != null) view.localRotation = initialViewRotation * Quaternion.Euler(pitch, 0, 0);
            if (restart) RestartDemo();
        }

        public void RestartDemo()
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            pitch = 0;
            if (view != null) view.localRotation = initialViewRotation;
            if (guide == null) return;
            guide.ResetToStart();
            if (dialogueDirector != null) dialogueDirector.Restart();
            else guide.StartGuidance();
        }
    }
}
