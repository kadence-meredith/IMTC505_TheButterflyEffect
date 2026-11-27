using System.Collections.Generic;
using ButterflyEffect.Prototype;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ButterflyEffect.Guidance
{
    /// <summary>Constrains controller locomotion while leaving tracked head and hand poses untouched.</summary>
    [DisallowMultipleComponent]
    public sealed class GuidanceLocomotionConstraint : MonoBehaviour
    {
        public XROrigin origin;
        private readonly List<MovementBinding> movement = new List<MovementBinding>();
        private readonly List<TeleportBinding> teleport = new List<TeleportBinding>();
        private readonly List<AreaBinding> areas = new List<AreaBinding>();

        private sealed class MovementBinding
        {
            public ContinuousMoveProvider provider;
            public XROriginMovement original;
            public PolicyMovement replacement;
        }

        private sealed class TeleportBinding
        {
            public TeleportationProvider original;
            public GuidanceTeleportationProvider replacement;
            public bool wasEnabled;
        }

        private sealed class AreaBinding
        {
            public BaseTeleportationInteractable area;
            public TeleportationProvider original;
            public GuidanceTeleportationProvider replacement;
        }

        private sealed class PolicyMovement : XROriginMovement
        {
            private readonly XROriginMovement original;
            public PolicyMovement(XROriginMovement source)
            {
                original = source ?? new XROriginMovement();
                forceUnconstrained = original.forceUnconstrained;
            }

            public override void Apply(XRMovableBody body)
            {
                if (body == null) return;
                Vector3 from = body.bodyPositionEvaluator != null
                    ? body.GetBodyGroundWorldPosition() : body.originTransform.position;
                Vector3 requested = motion;
                if (!PrototypeInteractionPolicy.CanMove(from, from + requested))
                    requested = new Vector3(0f, Finite(requested.y) ? requested.y : 0f, 0f);
                Vector3 previous = original.motion;
                bool previousForce = original.forceUnconstrained;
                try
                {
                    original.motion = requested;
                    original.forceUnconstrained = forceUnconstrained;
                    original.Apply(body);
                }
                finally
                {
                    original.motion = previous;
                    original.forceUnconstrained = previousForce;
                }
            }

            private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public void Configure(XROrigin source)
        {
            if (origin != source)
            {
                RestoreProviders();
                origin = source;
            }
            if (isActiveAndEnabled) RefreshProviders();
        }

        public void RefreshProviders()
        {
            if (origin == null) return;
            foreach (var provider in origin.GetComponentsInChildren<ContinuousMoveProvider>(true))
            {
                if (movement.Exists(binding => binding.provider == provider)) continue;
                var original = provider.transformation;
                var replacement = new PolicyMovement(original);
                movement.Add(new MovementBinding { provider = provider, original = original, replacement = replacement });
                provider.transformation = replacement;
            }

            foreach (var provider in origin.GetComponentsInChildren<TeleportationProvider>(true))
            {
                if (provider is GuidanceTeleportationProvider || teleport.Exists(binding => binding.original == provider)) continue;
                var holder = new GameObject("Guidance teleport permission");
                holder.SetActive(false);
                holder.transform.SetParent(provider.transform, false);
                var replacement = holder.AddComponent<GuidanceTeleportationProvider>();
                replacement.origin = origin;
                replacement.mediator = provider.mediator;
                replacement.transformationPriority = provider.transformationPriority;
                replacement.delayTime = provider.delayTime;
                replacement.upTransformation = provider.upTransformation;
                replacement.forwardTransformation = provider.forwardTransformation;
                replacement.positionTransformation = provider.positionTransformation;
                teleport.Add(new TeleportBinding
                { original = provider, replacement = replacement, wasEnabled = provider.enabled });
                provider.enabled = false;
                holder.SetActive(teleport[teleport.Count - 1].wasEnabled);
            }

            foreach (var area in FindObjectsByType<BaseTeleportationInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (area.gameObject.scene != origin.gameObject.scene || areas.Exists(binding => binding.area == area)) continue;
                TeleportationProvider current = area.teleportationProvider;
                TeleportBinding target = teleport.Find(binding => binding.original == current);
                if (target == null && current == null && teleport.Count > 0) target = teleport[0];
                if (target == null) continue;
                areas.Add(new AreaBinding { area = area, original = current, replacement = target.replacement });
                area.teleportationProvider = target.replacement;
            }
        }

        private void OnEnable() { RefreshProviders(); }
        private void OnDisable() { RestoreProviders(); }

        private void RestoreProviders()
        {
            foreach (var binding in areas)
                if (binding.area != null && binding.area.teleportationProvider == binding.replacement)
                    binding.area.teleportationProvider = binding.original;
            areas.Clear();
            foreach (var binding in movement)
                if (binding.provider != null && ReferenceEquals(binding.provider.transformation, binding.replacement))
                    binding.provider.transformation = binding.original;
            movement.Clear();
            foreach (var binding in teleport)
            {
                if (binding.replacement != null)
                {
                    binding.replacement.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(binding.replacement.gameObject);
                    else DestroyImmediate(binding.replacement.gameObject);
                }
                if (binding.original != null) binding.original.enabled = binding.wasEnabled;
            }
            teleport.Clear();
        }
    }
}
