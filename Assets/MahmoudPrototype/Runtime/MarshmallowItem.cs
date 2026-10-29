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
        public override string HeldHint => nextServingTime > 0f ? "Preparing another marshmallow..."
            : cookedSeconds >= burntSeconds ? "Burnt! F: eat it and try another   |   E: drop"
            : IsGolden ? "Golden! F: take a bite   |   Keep roasting to see it burn"
            : "Hold the marshmallow tip above a lit campfire. F: take a bite when ready";

        protected override void Awake() { base.Awake(); properties = new MaterialPropertyBlock(); }
        private void Update()
        {
            if (nextServingTime > 0f)
            {
                if (Time.time >= nextServingTime) { nextServingTime = 0f; cookedSeconds = 0f; marshmallow.enabled = true; }
                else return;
            }
            foreach (FireTarget fire in FindObjectsByType<FireTarget>(FindObjectsSortMode.None))
                if (fire.source == FireSource.Campfire && fire.CanRoast(roastPoint.position))
                { cookedSeconds = Mathf.Min(burntSeconds + 2f, cookedSeconds + Time.deltaTime); break; }
            Color colour = cookedSeconds < goldenSeconds
                ? Color.Lerp(Color.white, new Color(0.86f, 0.54f, 0.19f), cookedSeconds / goldenSeconds)
                : Color.Lerp(new Color(0.86f, 0.54f, 0.19f), new Color(0.11f, 0.07f, 0.035f),
                    (cookedSeconds - goldenSeconds) / (burntSeconds - goldenSeconds));
            marshmallow.GetPropertyBlock(properties); properties.SetColor(BaseColor, colour); properties.SetColor(ColorId, colour);
            marshmallow.SetPropertyBlock(properties);
        }

        public override void Use()
        {
            if (nextServingTime > 0f) return;
            if (cookedSeconds < goldenSeconds)
            { if (Session != null) Session.Say("Almost golden. Hold the marshmallow tip over the fire a little longer."); return; }
            if (Session != null)
            {
                Session.state.marshmallowsEaten++; Session.Changed();
                Session.Say(cookedSeconds >= burntSeconds ? "A little burnt. Try roasting another one."
                    : "That was good. One more? Your next marshmallow is ready in a moment.");
            }
            marshmallow.enabled = false; nextServingTime = Time.time + 0.8f;
        }

        public override void ResetPrototype()
        {
            base.ResetPrototype(); cookedSeconds = 0f; nextServingTime = 0f; marshmallow.enabled = true;
            properties.Clear(); properties.SetColor(BaseColor, Color.white); properties.SetColor(ColorId, Color.white);
            marshmallow.SetPropertyBlock(properties);
        }
    }
}
