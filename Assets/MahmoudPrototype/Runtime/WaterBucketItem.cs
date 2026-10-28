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
        private bool desktopPour;
        private float desktopTiltAngle;
        private FireTarget[] targets;
        public override string HeldHint => "Hold R: tip and pour onto fire   |   F: refill near the blue tank   |   E: drop";
        public override Quaternion DesktopHoldRotation => Quaternion.Euler(holdEuler) * Quaternion.Euler(desktopTiltAngle, 0f, 0f);

        protected override void Awake() { base.Awake(); targets = FindObjectsByType<FireTarget>(FindObjectsSortMode.None); }
        public override void DesktopTilt(bool held) { desktopPour = held; }
        public override void Grabbed(bool xr) { base.Grabbed(xr); desktopTiltAngle = 0f; }

        private void Update()
        {
            if (IsHeld && !HeldInXR)
                desktopTiltAngle = Mathf.MoveTowards(desktopTiltAngle, desktopPour ? 75f : 0f, Time.deltaTime * 240f);
            bool pouring = IsHeld && waterLeft > 0f && Vector3.Angle(transform.up, Vector3.up) > 45f;
            stream.enabled = pouring;
            if (waterSurface != null) waterSurface.gameObject.SetActive(waterLeft > 0.02f);
            if (!pouring) return;
            Vector3 origin = spout.position;
            Vector3 end = origin + Vector3.down * 3f;
            float distance = 3f;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.distance < distance)
                { end = hit.point; distance = hit.distance; }
            stream.SetPosition(0, origin); stream.SetPosition(1, end);
            float amount = Mathf.Min(waterLeft, pourRate * Time.deltaTime);
            waterLeft -= amount;
            foreach (FireTarget target in targets)
                if (target != null && target.ReceiveWater(end, amount)) break;
            if (waterLeft <= 0f && Session != null) Session.Say("The bucket is empty. Refill it beside the blue tank with F.");
        }

        public override void Use()
        {
            if (refillPoint != null && Vector3.Distance(transform.position, refillPoint.position) <= 2.6f)
            { waterLeft = capacity; if (Session != null) Session.Say("Bucket refilled. Tip it over the fire and keep pouring until the embers are cold."); }
            else if (Session != null) Session.Say("Bring the bucket beside the blue tank, then press F to refill it.");
        }

        public override void Released() { base.Released(); desktopPour = false; stream.enabled = false; }
        public override void ResetPrototype()
        {
            base.ResetPrototype(); waterLeft = capacity; desktopPour = false; desktopTiltAngle = 0f; stream.enabled = false;
            if (waterSurface != null) waterSurface.gameObject.SetActive(true);
        }
    }
}
