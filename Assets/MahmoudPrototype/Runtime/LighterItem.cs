using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class LighterItem : PrototypeItem
    {
        public Transform tip;
        public override string HeldHint => "F: light campfire | E: drop";
        public override void Use()
        {
            Vector3 position = tip != null ? tip.position : transform.position;
            foreach (FireTarget fire in FireTarget.ActiveTargets)
            {
                if (fire == null || fire.source != FireSource.Campfire || fire.IsHot) continue;
                if (Vector3.Distance(position, fire.transform.TransformPoint(fire.heatOffset)) > 2.1f) continue;
                fire.Ignite();
                if (Session != null)
                { Session.state.firesLit++; Session.state.totalActions++; Session.Changed(); }
                return;
            }
        }
    }
}
