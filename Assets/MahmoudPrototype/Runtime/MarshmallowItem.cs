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
        public bool IsGolden => cookedSeconds >= goldenSeconds && cookedSeconds < burntSeconds;
        public override string HeldHint => "F: eat | E: put down";
        protected override void Awake() { base.Awake(); properties = new MaterialPropertyBlock(); }

        private void Update()
        {
            if (marshmallow == null || roastPoint == null) return;
            if (nextServingTime > 0f)
            {
                if (Time.time < nextServingTime) return;
                nextServingTime = 0f; cookedSeconds = 0f; marshmallow.enabled = true;
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
            if (nextServingTime > 0f || marshmallow == null) return;
            // The player decides when to eat. No readiness gate, text, timer, or progress bar.
            if (Session != null)
            {
                Session.state.marshmallowsEaten++; Session.state.totalActions++;
                if (cookedSeconds < goldenSeconds) Session.state.rawMarshmallowsEaten++;
                else if (cookedSeconds >= burntSeconds) Session.state.burntMarshmallowsEaten++;
                else Session.state.goldenMarshmallowsEaten++;
                Session.Changed();
            }
            marshmallow.enabled = false; nextServingTime = Time.time + 0.8f;
        }
        public override void ResetPrototype()
        {
            base.ResetPrototype(); cookedSeconds = 0f; nextServingTime = 0f;
            if (marshmallow != null) marshmallow.enabled = true;
            ApplyColour();
        }
    }
}
