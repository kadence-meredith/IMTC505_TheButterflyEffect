using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class CigaretteItem : PrototypeItem
    {
        public GameObject glowingTip;
        public FireTarget grassFire;
        public float ignitionDelay = 4f;
        public LayerMask groundLayers;
        private bool hasIgnited;
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
                Session.state.RecordCigarette(CigaretteCondition.Dropped);
                Session.Say("The cigarette has been released.");
            }
        }

        private void Update()
{
    if (IsHeld || IsDisposed || Body == null)
        return;

    foreach (DisposalBin bin in
        FindObjectsByType<DisposalBin>(FindObjectsSortMode.None))
    {
        if (bin.Contains(transform.position))
        {
            DisposeSafely();
            return;
        }
    }

    bool onGround = Physics.Raycast(
        transform.position + Vector3.up * 0.12f,
        Vector3.down,
        out RaycastHit hit,
        0.35f,
        groundLayers,
        QueryTriggerInteraction.Ignore
    );

    if (!onGround || Body.linearVelocity.sqrMagnitude > 0.3f)
    {
        groundTime = 0f;
        return;
    }

    if (Session != null &&
        !Session.state.cigaretteDroppedOutsideBin)
    {
        Session.state.RecordCigarette(
            CigaretteCondition.Dropped, true
        );
        Session.Changed();
    }

    if (hasIgnited || grassFire == null)
        return;

    groundTime += Time.deltaTime;

    if (groundTime < ignitionDelay)
        return;

    grassFire.transform.position = hit.point;
    grassFire.Ignite();
    hasIgnited = true;

    if (Session != null)
        Session.Say("The cigarette started a small fire.");
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
        {   hasIgnited = false;
            base.ResetPrototype(); IsDisposed = false; groundTime = 0f;
            if (glowingTip != null) glowingTip.SetActive(true);
        }
    }
}
