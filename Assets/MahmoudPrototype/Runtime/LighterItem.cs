using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class LighterItem : PrototypeItem
    {
        public Transform tip;
        public override string HeldHint => "F: light a nearby campfire   |   E: drop lighter";
        public override void Use()
        {
            foreach (FireTarget fire in FindObjectsByType<FireTarget>(FindObjectsSortMode.None))
                if (fire.source == FireSource.Campfire && Vector3.Distance(tip.position, fire.transform.position + Vector3.up * 0.5f) < 2.1f)
                {
                    fire.Ignite();
                    if (Session != null) Session.Say("Campfire lit. Hold a marshmallow over the heat to roast it.");
                    return;
                }
            if (Session != null) Session.Say("Bring the lighter closer to the campfire, then press F.");
        }
    }
}
