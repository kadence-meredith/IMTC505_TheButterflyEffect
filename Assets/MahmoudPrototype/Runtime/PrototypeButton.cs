using UnityEngine;
using UnityEngine.Events;

namespace ButterflyEffect.Prototype
{
    public sealed class PrototypeButton : MonoBehaviour
    {
        public string prompt = "F: interact";
        public UnityEvent onPressed = new UnityEvent();
        private float nextPress;
        public void Press()
        {
            if (Time.time < nextPress) return;
            nextPress = Time.time + 0.3f;
            onPressed.Invoke();
        }
    }
}
