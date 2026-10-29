using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class FlamePulse : MonoBehaviour
    {
        private Vector3 original;
        private void Awake() { original = transform.localScale; }
        private void Update() { transform.localScale = original * (1f + Mathf.Sin(Time.time * 12f + transform.position.x) * 0.09f); }
    }
}
