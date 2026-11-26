using UnityEngine;

namespace ButterflyEffect.Guidance
{
    [DisallowMultipleComponent]
    public sealed class LowPolyButterflyWings : MonoBehaviour
    {
        public Transform leftWing;
        public Transform rightWing;
        [Min(.1f)] public float flapFrequency = 2.6f;
        [Range(0f, 80f)] public float flapAmplitude = 42f;
        [Range(0f, 60f)] public float restingAngle = 24f;

        public bool IsConfigured => leftWing != null && rightWing != null && leftWing != rightWing;

        private void Update()
        {
            Pose(Time.time);
        }

        public void Pose(float elapsed)
        {
            if (!IsConfigured) return;
            float angle = restingAngle + Mathf.Sin(elapsed * flapFrequency * Mathf.PI * 2f) * flapAmplitude;
            leftWing.localRotation = Quaternion.AngleAxis(-angle, Vector3.forward);
            rightWing.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}
