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
        private const float MaxDrop = 3f;
        private const int ArcSegments = 5;
        private readonly Vector3[] pourPath = new Vector3[ArcSegments + 2];
        internal Camera DesktopView { private get; set; }
        public override string HeldHint => "Hold R: pour | F beside water barrel: refill | E: set down on level ground";
        public override Quaternion DesktopHoldRotation => Quaternion.Euler(holdEuler) * Quaternion.Euler(desktopTiltAngle, 0f, 0f);
        public override void DesktopTilt(bool held) { desktopPour = held; }
        public override void Grabbed(bool xr) { base.Grabbed(xr); desktopTiltAngle = 0f; }

        protected override void Awake()
        {
            base.Awake();
            if (waterSurface != null) fullSurfaceHeight = waterSurface.localPosition.y;
        }

        private void LateUpdate()
        {
            UpdatePouring();
            RefreshWaterSurface();
        }

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
        }

        private void UpdatePouring()
        {
            bool pouring = IsHeld && waterLeft > 0f && spout != null
                && (HeldInXR ? Vector3.Angle(transform.up, Vector3.up) > 45f : desktopPour);
            if (stream != null) stream.enabled = pouring;
            if (pouring && !wasPouring && Session != null)
            { Session.state.poursStarted++; Session.state.totalActions++; Session.Changed(); }
            wasPouring = pouring;
            if (!pouring) return;
            Collider surface = TracePour(out int count);
            Vector3 end = pourPath[count - 1];
            if (stream != null)
            {
                stream.positionCount = count;
                for (int i = 0; i < count; i++) stream.SetPosition(i, pourPath[i]);
            }
            float amount = Mathf.Min(waterLeft, pourRate * Time.deltaTime);
            waterLeft = Mathf.Max(0f, waterLeft - amount);
            if (surface == null) return;
            if (surface is TerrainCollider ground)
                WetGrass.WaterGround(ground, end, amount);
            // Query the registry each pour so a newly created or enabled fire also receives water.
            foreach (FireTarget target in FireTarget.ActiveTargets)
                if (target != null && target.ReceiveWater(end, amount)) break;
        }

        private Collider TracePour(out int count)
        {
            Vector3 origin = spout.position;
            pourPath[0] = origin;
            count = 1;
            Vector3 sideways = Vector3.zero;
            float drop = MaxDrop;
            if (!HeldInXR && DesktopView != null)
            {
                Transform eye = DesktopView.transform;
                if (FirstSurface(eye.position, eye.forward, 3.5f, out RaycastHit aim) && aim.point.y < origin.y - 0.05f)
                {
                    Vector3 delta = aim.point - origin;
                    sideways = Vector3.ClampMagnitude(new Vector3(delta.x, 0f, delta.z), 0.95f);
                    drop = Mathf.Min(MaxDrop, -delta.y + 0.02f);
                }
            }

            int steps = sideways.sqrMagnitude > 0.0001f ? ArcSegments : 1;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 next = origin + sideways * t + Vector3.down * (drop * t * t);
                if (AppendSegment(next, ref count, out Collider hit)) return hit;
            }
            // A low ledge can end before the intended landing spot. Continue down to the ground.
            if (drop < MaxDrop && AppendSegment(origin + sideways + Vector3.down * MaxDrop, ref count, out Collider below))
                return below;
            return null;
        }

        private bool AppendSegment(Vector3 next, ref int count, out Collider collider)
        {
            Vector3 start = pourPath[count - 1];
            Vector3 segment = next - start;
            bool hit = FirstSurface(start, segment.normalized, segment.magnitude, out RaycastHit contact);
            pourPath[count++] = hit ? contact.point : next;
            collider = hit ? contact.collider : null;
            return hit;
        }

        private bool FirstSurface(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            if (!Physics.Raycast(origin, direction, out nearest, distance, ~0, QueryTriggerInteraction.Ignore))
                return false;
            if (IsExternalSurface(nearest)) return true;

            nearest = default;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsExternalSurface(hit) && (nearest.collider == null || hit.distance < nearest.distance))
                    nearest = hit;
            }
            return nearest.collider != null;
        }

        private bool IsExternalSurface(RaycastHit hit) =>
            !hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<DesktopPlayer>() == null;

        public override void Use()
        {
            if (refillPoint == null || Vector3.Distance(transform.position, refillPoint.position) > 1.8f || waterLeft >= capacity - 0.01f) return;
            waterLeft = capacity;
            RefreshWaterSurface();
            if (Session != null) { Session.state.bucketRefills++; Session.state.totalActions++; Session.Changed(); }
        }

        public bool TryGetPlacementPose(Transform player, out Pose pose)
        {
            pose = default;
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return false;

            Quaternion upright = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            Vector3 scale = transform.lossyScale;
            Vector3 halfSize = Vector3.Scale(box.size, scale) * 0.5f;
            Vector3 centerOffset = upright * Vector3.Scale(box.center, scale);
            Vector3 forward = upright * Vector3.forward;
            Vector3 right = upright * Vector3.right;
            Vector3 start = player.position + Vector3.up * 0.55f;

            // Beside a prop such as the refill barrel, try the ground to either side.
            foreach (Vector2 offset in PlacementOffsets)
            {
                Vector3 probe = start + forward * offset.y + right * offset.x;
                if (Physics.Linecast(start, probe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                if (!TryGetSupportHeight(probe, out float floor)) continue;

                bool supported = true;
                float highest = floor;
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = probe + 0.9f * (x * right * halfSize.x + z * forward * halfSize.z);
                    if (!TryGetSupportHeight(corner, out float height) || Mathf.Abs(height - floor) > 0.035f)
                        supported = false;
                    else
                        highest = Mathf.Max(highest, height);
                }
                if (!supported) continue;

                Vector3 center = new Vector3(probe.x, highest + halfSize.y + 0.01f, probe.z);
                if (Physics.CheckBox(center, halfSize, upright, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                pose = new Pose(center - centerOffset, upright);
                return true;
            }
            return false;
        }

        private static readonly Vector2[] PlacementOffsets =
        {
            new Vector2(0f, 1.05f), new Vector2(0.5f, 1.05f), new Vector2(-0.5f, 1.05f),
            new Vector2(0.8f, 0.4f), new Vector2(-0.8f, 0.4f),
            new Vector2(0.8f, 0f), new Vector2(-0.8f, 0f)
        };

        private static bool TryGetSupportHeight(Vector3 probe, out float height)
        {
            height = 0f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 1.4f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.99f || hit.collider.attachedRigidbody != null) return false;
            height = hit.point.y;
            return true;
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
