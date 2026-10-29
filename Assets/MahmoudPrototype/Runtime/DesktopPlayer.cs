using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ButterflyEffect.Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DesktopPlayer : MonoBehaviour
    {
        public Camera view;
        public LineRenderer throwGuide;
        public CigaretteItem startingCigarette;
        public Transform[] stationSpawns;
        public float moveSpeed = 3.3f;
        public float lookSensitivity = 0.11f;
        public float reach = 3.5f;
        [HideInInspector] public bool inputEnabled = true;
        public PrototypeItem Held { get; private set; }
        public string TargetHint { get; private set; }
        private CharacterController motor;
        private float pitch;
        private float verticalSpeed;
        private bool aimingThrow;
        private bool isLooking;
        private float nextTargetScan;
        private PrototypeItem targetItem;
        private PrototypeButton targetButton;
        private bool heldGravity;
        private bool heldCollisions;
        private bool heldKinematic;
        private RigidbodyInterpolation heldInterpolation;

        private void Awake() { motor = GetComponent<CharacterController>(); }
        private void Start() { GrabStartingCigarette(); }
        private void LateUpdate() { RefreshHeldPose(); }

        private void Update()
        {
            if (!inputEnabled) return;
            Keyboard keys = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keys == null || mouse == null || view == null) return;
            if (keys.escapeKey.wasPressedThisFrame) EndLook();
            if (mouse.rightButton.wasPressedThisFrame) { isLooking = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (mouse.rightButton.wasReleasedThisFrame) EndLook();
            if (isLooking)
            {
                Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
                transform.Rotate(0f, delta.x, 0f);
                pitch = Mathf.Clamp(pitch - delta.y, -78f, 78f);
                view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            Vector3 move = new Vector3((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0), 0,
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            move = transform.TransformDirection(Vector3.ClampMagnitude(move, 1f)) * moveSpeed;
            if (motor.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            motor.Move((move + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (transform.position.y < -8f) GoToStation(0);

            ScanTarget();
            if (keys.eKey.wasPressedThisFrame)
            {
                if (Held != null) DropHeld(false);
                else if (targetItem != null) Grab(targetItem);
            }
            if (keys.fKey.wasPressedThisFrame)
            {
                if (Held != null) Held.Use();
                else if (targetButton != null) targetButton.Press();
            }
            if (Held != null)
            {
                Held.DesktopTilt(keys.rKey.isPressed);
                if (keys.qKey.wasPressedThisFrame) aimingThrow = true;
                if (aimingThrow && keys.qKey.wasReleasedThisFrame) DropHeld(true);
            }
            else aimingThrow = false;
            DrawThrowGuide();
            if (keys.digit1Key.wasPressedThisFrame) GoToStation(0);
            if (keys.digit2Key.wasPressedThisFrame) GoToStation(1);
            if (keys.digit3Key.wasPressedThisFrame) GoToStation(2);
            if (keys.digit4Key.wasPressedThisFrame) GoToStation(3);
            if (keys.backspaceKey.wasPressedThisFrame && PrototypeSession.Instance != null) PrototypeSession.Instance.ResetRun();
            if (keys.tabKey.wasPressedThisFrame && PrototypeSession.Instance != null) PrototypeSession.Instance.ShowDebrief();
        }

        private void ScanTarget()
        {
            if (Time.time < nextTargetScan) return;
            nextTargetScan = Time.time + 0.06f;
            targetItem = null; targetButton = null; TargetHint = "";
            RaycastHit[] hits = Physics.RaycastAll(view.transform.position, view.transform.forward, reach, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (Held != null && hit.transform.IsChildOf(Held.transform)) continue;
                targetItem = hit.collider.GetComponentInParent<PrototypeItem>();
                targetButton = hit.collider.GetComponentInParent<PrototypeButton>();
                if (targetItem != null) TargetHint = targetItem.Hint;
                else if (targetButton != null) TargetHint = targetButton.prompt;
                break;
            }
            // Small cigarette and lighter shapes remain easy to target without selecting through walls.
            if (targetItem == null && targetButton == null && Held == null)
            {
                RaycastHit[] assisted = Physics.SphereCastAll(view.transform.position, 0.085f, view.transform.forward,
                    reach, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(assisted, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit candidate in assisted)
                {
                    PrototypeItem item = candidate.collider.GetComponentInParent<PrototypeItem>();
                    if (item == null || item.IsHeld) continue;
                    Vector3 direction = candidate.collider.bounds.center - view.transform.position;
                    if (Physics.Raycast(view.transform.position, direction.normalized, out RaycastHit sight,
                        direction.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                        && sight.collider.GetComponentInParent<PrototypeItem>() == item)
                    { targetItem = item; TargetHint = item.Hint; break; }
                }
            }
        }

        public void GrabStartingCigarette()
        {
            if (startingCigarette != null && !startingCigarette.IsDisposed && startingCigarette.gameObject.activeInHierarchy) Grab(startingCigarette);
        }

        public void Grab(PrototypeItem item)
        {
            if (Held != null || item == null || item.IsHeld || view == null || !item.gameObject.activeInHierarchy) return;
            XrItemBridge bridge = item.GetComponent<XrItemBridge>();
            if (bridge != null && !bridge.TryAcquireDesktopControl()) return;
            Held = item;
            heldGravity = item.Body.useGravity;
            heldCollisions = item.Body.detectCollisions;
            heldKinematic = item.Body.isKinematic;
            heldInterpolation = item.Body.interpolation;
            if (!item.Body.isKinematic)
            { item.Body.linearVelocity = Vector3.zero; item.Body.angularVelocity = Vector3.zero; }
            item.Body.interpolation = RigidbodyInterpolation.None;
            item.Body.isKinematic = true;
            item.Body.useGravity = false;
            item.Body.detectCollisions = false;
            item.Grabbed(false);
            RefreshHeldPose();
        }

        public void RefreshHeldPose()
        {
            if (Held == null || view == null) return;
            if (!Held.gameObject.activeInHierarchy) { DropHeld(false); return; }
            Vector3 position = view.transform.TransformPoint(Held.holdOffset);
            Quaternion rotation = view.transform.rotation * Held.DesktopHoldRotation;
            // No interpolated, parented Rigidbody: explicitly synchronize the physics and visible poses.
            Held.Body.position = position;
            Held.Body.rotation = rotation;
            Held.transform.SetPositionAndRotation(position, rotation);
        }

        public void DropHeld(bool throwing)
        {
            if (Held == null) return;
            PrototypeItem item = Held;
            if (view != null)
            {
                Vector3 position = view.transform.TransformPoint(item.holdOffset);
                Quaternion rotation = view.transform.rotation * item.DesktopHoldRotation;
                item.Body.position = position; item.Body.rotation = rotation;
                item.transform.SetPositionAndRotation(position, rotation);
            }
            Held = null; aimingThrow = false;
            item.DesktopTilt(false);
            item.Body.isKinematic = heldKinematic;
            item.Body.useGravity = heldGravity;
            item.Body.detectCollisions = heldCollisions;
            item.Body.interpolation = heldInterpolation;
            if (!item.Body.isKinematic)
            {
                item.Body.linearVelocity = throwing && view != null ? ThrowVelocity() : Vector3.zero;
                item.Body.angularVelocity = throwing && view != null ? view.transform.right * 2.5f : Vector3.zero;
            }
            Physics.SyncTransforms();
            item.Released();
            XrItemBridge bridge = item.GetComponent<XrItemBridge>();
            if (bridge != null) bridge.ReleaseDesktopControl();
        }

        private Vector3 ThrowVelocity() => view.transform.forward * 6f + Vector3.up * 1.5f;
        private void DrawThrowGuide()
        {
            if (throwGuide == null) return;
            throwGuide.enabled = aimingThrow && Held != null;
            if (!throwGuide.enabled) return;
            Vector3 start = Held.transform.position;
            Vector3 velocity = ThrowVelocity();
            Vector3 last = start;
            int count = 0;
            throwGuide.positionCount = 36;
            for (int i = 0; i < 36; i++)
            {
                float t = i * 0.06f;
                Vector3 next = start + velocity * t + Physics.gravity * (0.5f * t * t);
                if (i > 0 && Physics.Raycast(last, (next - last).normalized, out RaycastHit hit, Vector3.Distance(last, next), ~0, QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(Held.transform))
                { throwGuide.SetPosition(count++, hit.point); break; }
                throwGuide.SetPosition(count++, next); last = next;
            }
            throwGuide.positionCount = count;
        }

        public void GoToStation(int index)
        {
            if (stationSpawns == null || index < 0 || index >= stationSpawns.Length || stationSpawns[index] == null) return;
            Transform spawn = stationSpawns[index];
            motor.enabled = false; transform.SetPositionAndRotation(spawn.position, spawn.rotation); motor.enabled = true;
            verticalSpeed = 0f; pitch = 8f; view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            RefreshHeldPose();
            if (throwGuide != null) throwGuide.enabled = false;
        }

        private void EndLook() { isLooking = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnDisable() { DropHeld(false); EndLook(); }
    }
}
