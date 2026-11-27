using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class MarshmallowItem : PrototypeItem
    {
        public Transform roastPoint;
        public Renderer marshmallow;
        public float goldenSeconds = 4f;
        public float burntSeconds = 9f;
        public float cookedSeconds;
        private float nextServingTime;
        private MaterialPropertyBlock properties;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        public bool RequireServingChoice { get; set; }
        public bool IsServingConsumed { get; private set; }
        public int ServingRevision { get; private set; }
        public bool IsGolden => cookedSeconds >= goldenSeconds && cookedSeconds < burntSeconds;
        public override string HeldHint => "F: eat | E: put down";
        protected override void Awake() { base.Awake(); properties = new MaterialPropertyBlock(); }

        private void Update()
        {
            if (marshmallow == null || roastPoint == null) return;
            if (IsServingConsumed)
            {
                if (RequireServingChoice || Time.time < nextServingTime) return;
                StartNewServing();
            }
            if (IsHeld)
                foreach (FireTarget fire in FireTarget.ActiveTargets)
                    if (fire != null && fire.source == FireSource.Campfire && fire.CanRoast(roastPoint.position))
                    { cookedSeconds = Mathf.Min(burntSeconds + 2f, cookedSeconds + Time.deltaTime); break; }
            ApplyColour();
        }

        private void ApplyColour()
        {
            if (marshmallow == null || properties == null) return;
            Color colour = cookedSeconds < goldenSeconds
                ? Color.Lerp(Color.white, new Color(0.86f, 0.54f, 0.19f), cookedSeconds / Mathf.Max(0.01f, goldenSeconds))
                : Color.Lerp(new Color(0.86f, 0.54f, 0.19f), new Color(0.11f, 0.07f, 0.035f),
                    (cookedSeconds - goldenSeconds) / Mathf.Max(0.01f, burntSeconds - goldenSeconds));
            marshmallow.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, colour); properties.SetColor(ColorId, colour);
            marshmallow.SetPropertyBlock(properties);
        }

        public override void Use()
        {
            if (!PrototypeInteractionPolicy.CanUse(this)) return;
            if (IsServingConsumed || marshmallow == null) return;
            IsServingConsumed = true;
            ServingRevision++;
            marshmallow.enabled = false;
            nextServingTime = Time.time + 0.8f;
            if (Session != null)
            {
                Session.state.marshmallowsEaten++; Session.state.totalActions++;
                if (cookedSeconds < goldenSeconds) Session.state.rawMarshmallowsEaten++;
                else if (cookedSeconds >= burntSeconds) Session.state.burntMarshmallowsEaten++;
                else Session.state.goldenMarshmallowsEaten++;
                Session.Changed();
            }
        }

        public bool TryStartNewServing()
        {
            if (!PrototypeInteractionPolicy.CanUse(this) || marshmallow == null) return false;
            StartNewServing();
            return true;
        }

        private void StartNewServing()
        {
            cookedSeconds = 0f;
            nextServingTime = 0f;
            IsServingConsumed = false;
            ServingRevision++;
            marshmallow.enabled = true;
            ApplyColour();
        }
        public override void ResetPrototype()
        {
            base.ResetPrototype(); cookedSeconds = 0f; nextServingTime = 0f;
            IsServingConsumed = false;
            ServingRevision++;
            if (marshmallow != null) marshmallow.enabled = true;
            ApplyColour();
        }
    }
}
