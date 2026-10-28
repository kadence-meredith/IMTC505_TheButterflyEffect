using UnityEngine;

namespace ButterflyEffect.Prototype
{
    [RequireComponent(typeof(Rigidbody))]
    public class PrototypeItem : MonoBehaviour, IPrototypeResettable
    {
        public string itemName = "Object";
        public Vector3 holdOffset = new Vector3(0.32f, -0.3f, 0.75f);
        public Vector3 holdEuler;
        public bool IsHeld { get; private set; }
        public bool HeldInXR { get; private set; }
        public Rigidbody Body { get; private set; }
        protected PrototypeSession Session => PrototypeSession.Instance;
        protected Vector3 initialPosition;
        protected Quaternion initialRotation;
        protected Transform initialParent;
        private bool initialKinematic;
        private bool initialGravity;
        private bool initialCollisions;
        private RigidbodyInterpolation initialInterpolation;
        public virtual string Hint => "E: pick up " + itemName;
        public virtual string HeldHint => "E: drop   |   Hold Q: aim throw, release Q: throw";
        public virtual Quaternion DesktopHoldRotation => Quaternion.Euler(holdEuler);

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialParent = transform.parent;
            initialKinematic = Body.isKinematic;
            initialGravity = Body.useGravity;
            initialCollisions = Body.detectCollisions;
            initialInterpolation = Body.interpolation;
        }

        public virtual void Grabbed(bool xr)
        {
            IsHeld = true;
            HeldInXR = xr;
        }

        public virtual void Released()
        {
            IsHeld = false;
            HeldInXR = false;
        }

        public virtual void Use() { }
        public virtual void DesktopTilt(bool held) { }

        public virtual void ResetPrototype()
        {
            XrItemBridge bridge = GetComponent<XrItemBridge>();
            if (bridge != null) bridge.ReleaseDesktopControl();
            IsHeld = false;
            HeldInXR = false;
            gameObject.SetActive(true);
            transform.SetParent(initialParent, true);
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.None;
            Body.position = initialPosition;
            Body.rotation = initialRotation;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = initialKinematic;
            Body.useGravity = initialGravity;
            Body.detectCollisions = initialCollisions;
            Body.interpolation = initialInterpolation;
        }
    }
}
