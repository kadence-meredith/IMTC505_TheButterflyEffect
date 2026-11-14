using System;
using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class CigaretteItem : PrototypeItem
    {
        public GameObject glowingTip;
        public FireTarget grassFire;
        public float ignitionDelay = 1.5f;
        public LayerMask groundLayers = ~0;
        public bool requireMarkedGround;
        public bool IsDisposed { get; private set; }
        private bool hasIgnited;
        private bool hasBeenReleased;
        private float groundTime;
        private DisposalBin[] bins;
        public override string HeldHint => "E: drop | Hold Q and release: throw";

        protected override void Awake()
        {
            base.Awake();
            bins = FindObjectsByType<DisposalBin>(FindObjectsSortMode.None);
        }

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
            hasBeenReleased = true;
            if (TryDispose()) return;
            if (Session != null) { Session.state.RecordCigarette(CigaretteCondition.Dropped); Session.Changed(); }
        }

        private bool TryDispose()
        {
            if (bins == null) bins = FindObjectsByType<DisposalBin>(FindObjectsSortMode.None);
            foreach (DisposalBin bin in bins)
                if (bin != null && bin.isActiveAndEnabled && bin.Contains(transform.position))
                { DisposeSafely(); return true; }
            return false;
        }

        private void Update()
        {
            if (IsHeld || IsDisposed || Body == null || !hasBeenReleased) return;
            if (TryDispose()) return;
            RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 0.15f,
                Vector3.down, 0.43f, groundLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool grounded = false;
            Vector3 point = transform.position;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                // Stop at furniture: never detect the ground through a tabletop or bin.
                grounded = hit.normal.y > 0.35f && (!requireMarkedGround ||
                    hit.collider.GetComponentInParent<CampsiteGround>() != null);
                point = hit.point;
                break;
            }
            if (!grounded || Body.linearVelocity.sqrMagnitude > 0.5f)
            { groundTime = 0f; return; }
            if (Session != null && !Session.state.cigaretteDroppedOutsideBin)
            {
                Session.state.RecordCigarette(CigaretteCondition.Dropped, true);
                Session.Changed();
            }
            if (hasIgnited || grassFire == null) return;
            groundTime += Time.deltaTime;
            if (groundTime < ignitionDelay) return;
            grassFire.transform.position = point;
            grassFire.Ignite();
            hasIgnited = true;
        }

        public void DisposeSafely()
        {
            if (IsDisposed || IsHeld) return;
            IsDisposed = true;
            if (glowingTip != null) glowingTip.SetActive(false);
            if (Session != null)
            {
                Session.state.RecordCigarette(CigaretteCondition.Disposed);
                Session.state.cigaretteDisposals++;
                Session.state.totalActions++;
                Session.Changed();
            }
            gameObject.SetActive(false);
        }

        public override void ResetPrototype()
        {
            base.ResetPrototype();
            IsDisposed = false; hasIgnited = false; hasBeenReleased = false; groundTime = 0f;
            if (glowingTip != null) glowingTip.SetActive(true);
        }
    }
}
