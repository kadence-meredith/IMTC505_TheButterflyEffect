using UnityEngine;

namespace ButterflyEffect.Prototype
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DisposalBin : MonoBehaviour
    {
        private BoxCollider zone;
        private void Awake() { zone = GetComponent<BoxCollider>(); zone.isTrigger = true; }
        public bool Contains(Vector3 point) { return GetComponent<BoxCollider>().bounds.Contains(point); }
        private void OnTriggerStay(Collider other)
        {
            CigaretteItem item = other.GetComponentInParent<CigaretteItem>();
            if (item != null && !item.IsHeld) item.DisposeSafely();
        }
    }
}
