using UnityEngine;

namespace ButterflyEffect.Prototype
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DisposalBin : MonoBehaviour
    {
        private BoxCollider zone;
        private void Awake() { zone = GetComponent<BoxCollider>(); zone.isTrigger = true; }

        public bool Contains(Vector3 point)
        {
            if (zone == null) zone = GetComponent<BoxCollider>();
            Vector3 local = zone.transform.InverseTransformPoint(point) - zone.center;
            Vector3 half = zone.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private void OnTriggerEnter(Collider other) { Check(other); }
        private void OnTriggerStay(Collider other) { Check(other); }
        private void Check(Collider other)
        {
            CigaretteItem item = other.GetComponentInParent<CigaretteItem>();
            if (item != null && !item.IsHeld) item.DisposeSafely();
        }
    }
}
