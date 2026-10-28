using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class CigaretteItem : PrototypeItem
    {
        public GameObject glowingTip;
        public FireTarget grassFire;
        public float ignitionDelay = 4f;
        public bool IsDisposed { get; private set; }
        private float groundTime;
        public override string HeldHint => "E: drop   |   Release inside the blue bin to dispose   |   Hold Q: aim throw";

        public override void Grabbed(bool xr)
        {
            base.Grabbed(xr);
            groundTime = 0f;
            if (Session != null) { Session.state.RecordCigarette(CigaretteCondition.Held); Session.Changed(); }
        }

        public override void Released()
        {
            base.Released();
            if (IsDisposed) return;
            foreach (DisposalBin bin in FindObjectsByType<DisposalBin>(FindObjectsSortMode.None))
                if (bin.Contains(transform.position)) { DisposeSafely(); return; }
            if (Session != null)
            {
                Session.state.RecordCigarette(CigaretteCondition.Dropped, true);
                Session.Say("The lit cigarette is on the ground. You can still pick it up and put it in the bin.");
            }
        }

        private void Update()
        {
            if (IsHeld || IsDisposed || Body == null || grassFire == null) return;
            Vector3 difference = transform.position - grassFire.transform.position;
            if (new Vector2(difference.x, difference.z).magnitude < grassFire.catchRadius
                && Mathf.Abs(difference.y) < 0.45f && Body.linearVelocity.sqrMagnitude < 0.3f)
            {
                groundTime += Time.deltaTime;
                if (groundTime >= ignitionDelay && grassFire.progress.condition == FireCondition.Cold)
                {
                    grassFire.Ignite();
                    if (Session != null) Session.Say("The cigarette ignited the dry grass. Picking it up will not put the grass fire out.");
                }
            }
            else groundTime = 0f;
        }

        public void DisposeSafely()
        {
            if (IsDisposed || IsHeld) return;
            IsDisposed = true;
            if (glowingTip != null) glowingTip.SetActive(false);
            if (Session != null)
            {
                Session.state.RecordCigarette(CigaretteCondition.Disposed);
                Session.Say("Cigarette safely disposed of. Your hand is free.");
            }
            gameObject.SetActive(false);
        }

        public override void ResetPrototype()
        {
            base.ResetPrototype(); IsDisposed = false; groundTime = 0f;
            if (glowingTip != null) glowingTip.SetActive(true);
        }
    }
}
