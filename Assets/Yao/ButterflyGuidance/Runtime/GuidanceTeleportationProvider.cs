using ButterflyEffect.Prototype;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ButterflyEffect.Guidance
{
    /// <summary>Validates a teleport before any rig translation or orientation is queued.</summary>
    public sealed class GuidanceTeleportationProvider : TeleportationProvider
    {
        public XROrigin origin;

        public bool CanQueueTo(TeleportRequest request)
        {
            XROrigin source = origin != null ? origin : mediator != null ? mediator.xrOrigin : null;
            if (source == null) return false;
            Vector3 from = source.Camera != null ? source.Camera.transform.position : source.Origin.transform.position;
            from.y = source.Origin.transform.position.y;
            return PrototypeInteractionPolicy.CanMove(from, request.destinationPosition);
        }

        public override bool QueueTeleportRequest(TeleportRequest request)
        {
            return CanQueueTo(request) && base.QueueTeleportRequest(request);
        }

        protected override void Update()
        {
            // A station can change while a delayed teleport is pending.
            if (validRequest && !CanQueueTo(currentRequest))
            {
                validRequest = false;
                TryEndLocomotion();
                return;
            }
            base.Update();
        }

        protected override void OnDisable()
        {
            validRequest = false;
            TryEndLocomotion();
            base.OnDisable();
        }
    }
}
