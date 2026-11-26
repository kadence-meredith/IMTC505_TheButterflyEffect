using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class WaterBucketItem : PrototypeItem
    {
        public Transform spout;
        public Transform refillPoint;
        public Transform waterSurface;
        public LineRenderer stream;
        public float capacity = 7f;
        public float waterLeft = 7f;
        public float pourRate = 1.4f;
        [Tooltip("Distance from the full water surface to the bottom of this bucket.")]
        [SerializeField, Min(0f)] private float surfaceTravel;
        private float fullSurfaceHeight;
        private bool desktopPour;
        private bool wasPouring;
        private float desktopTiltAngle;
        public override string HeldHint => "Hold R: pour | F beside water barrel: refill | E: drop";
        public override Quaternion DesktopHoldRotation => Quaternion.Euler(holdEuler) * Quaternion.Euler(desktopTiltAngle, 0f, 0f);
        public override void DesktopTilt(bool held) { desktopPour = held; }
        public override void Grabbed(bool xr) { base.Grabbed(xr); desktopTiltAngle = 0f; }

        protected override void Awake()
        {
            base.Awake();
            if (waterSurface != null) fullSurfaceHeight = waterSurface.localPosition.y;
        }

        private void LateUpdate() => RefreshWaterSurface();

        private void RefreshWaterSurface()
        {
            if (waterSurface == null) return;
            if (surfaceTravel > 0f)
            {
                float fill = capacity > 0f ? Mathf.Clamp01(waterLeft / capacity) : 0f;
                Vector3 position = waterSurface.localPosition;
                float height = fullSurfaceHeight - surfaceTravel * (1f - fill);
                if (position.y != height)
                {
                    position.y = height;
                    waterSurface.localPosition = position;
                }
            }
            waterSurface.gameObject.SetActive(waterLeft > 0.02f);
        }

        private void Update()
        {
            if (IsHeld && !HeldInXR)
                desktopTiltAngle = Mathf.MoveTowards(desktopTiltAngle, desktopPour ? 75f : 0f, Time.deltaTime * 240f);
            bool pouring = IsHeld && waterLeft > 0f && spout != null && Vector3.Angle(transform.up, Vector3.up) > 45f;
            if (stream != null) stream.enabled = pouring;
            if (pouring && !wasPouring && Session != null)
            { Session.state.poursStarted++; Session.state.totalActions++; Session.Changed(); }
            wasPouring = pouring;
            if (!pouring) return;
            Vector3 origin = spout.position;
            Vector3 end = origin + Vector3.down * 3f;
            float distance = 3f;
            bool hasHit = false;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<DesktopPlayer>() != null) continue;
                if (hit.distance < distance) { end = hit.point; distance = hit.distance; hasHit = true; }
            }
            if (stream != null) { stream.SetPosition(0, origin); stream.SetPosition(1, end); }
            float amount = Mathf.Min(waterLeft, pourRate * Time.deltaTime);
            waterLeft = Mathf.Max(0f, waterLeft - amount);
            if (!hasHit) return;
            // Query the registry each pour so a newly created or enabled fire also receives water.
            foreach (FireTarget target in FireTarget.ActiveTargets)
                if (target != null && target.ReceiveWater(end, amount)) break;
        }

        public override void Use()
        {
            if (refillPoint == null || Vector3.Distance(transform.position, refillPoint.position) > 1.8f || waterLeft >= capacity - 0.01f) return;
            waterLeft = capacity;
            RefreshWaterSurface();
            if (Session != null) { Session.state.bucketRefills++; Session.state.totalActions++; Session.Changed(); }
        }
        public override void Released()
        { base.Released(); desktopPour = false; wasPouring = false; if (stream != null) stream.enabled = false; }
        public override void ResetPrototype()
        {
            base.ResetPrototype(); waterLeft = capacity; desktopPour = false; wasPouring = false; desktopTiltAngle = 0f;
            if (stream != null) stream.enabled = false;
            RefreshWaterSurface();
        }
    }
}
