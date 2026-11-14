using System.Collections.Generic;
using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class FireTarget : MonoBehaviour, IPrototypeResettable
    {
        private static readonly List<FireTarget> activeTargets = new List<FireTarget>();
        public static IReadOnlyList<FireTarget> ActiveTargets => activeTargets;
        public FireSource source;
        public FireProgress progress = new FireProgress();
        public FireCondition startingCondition = FireCondition.Cold;
        public GameObject flames;
        public GameObject embers;
        public float catchRadius = 0.9f;
        public float heatRadius = 1.15f;
        public Vector3 heatOffset = new Vector3(0f, 0.8f, 0f);
        public float emberReigniteDelay = 12f;
        private float emberIdle;
        public bool IsHot => progress.condition == FireCondition.Burning;
        public bool CanRoast(Vector3 point) => IsHot && Vector3.Distance(point, transform.TransformPoint(heatOffset)) < heatRadius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRegistry() { activeTargets.Clear(); }
        private void OnEnable() { if (!activeTargets.Contains(this)) activeTargets.Add(this); }
        private void OnDisable() { activeTargets.Remove(this); }
        private void Start()
        {
            if (startingCondition == FireCondition.Burning) progress.Ignite();
            else { progress.Reset(); progress.condition = startingCondition; }
            Refresh();
        }
        private void Update()
        {
            if (progress.condition != FireCondition.HotEmbers) return;
            emberIdle += Time.deltaTime;
            if (emberIdle >= emberReigniteDelay) Ignite();
        }
        public void Ignite()
        {
            if (progress.condition == FireCondition.Burning) return;
            emberIdle = 0f; progress.Ignite(); Refresh();
        }
        public void LeaveUnlit() { }

        public bool ReceiveWater(Vector3 groundPoint, float amount)
        {
            Vector3 d = groundPoint - transform.position;
            if (new Vector2(d.x, d.z).magnitude > catchRadius || Mathf.Abs(d.y) > 1.4f) return false;
            if (progress.condition == FireCondition.Cold || amount <= 0f) return false;
            FireCondition before = progress.condition;
            emberIdle = 0f; progress.Pour(amount);
            if (before != progress.condition) Refresh();
            return true;
        }

        private void Refresh()
        {
            SetVisual(flames, progress.condition == FireCondition.Burning);
            SetVisual(embers, progress.condition != FireCondition.Cold);
            PrototypeSession session = PrototypeSession.Instance;
            if (session == null) return;
            if (source == FireSource.Campfire) session.state.RecordCampfire(progress.condition);
            if (source == FireSource.CigaretteGrass) session.state.cigaretteGrassBurning = progress.condition != FireCondition.Cold;
            if (source == FireSource.FireworksGrass) session.state.fireworksGrassBurning = progress.condition != FireCondition.Cold;
            session.Changed();
        }

        private static void SetVisual(GameObject visual, bool visible)
        {
            if (visual == null || visual.activeSelf == visible) return;
            visual.SetActive(visible);
            if (!visible) return;
            foreach (ParticleSystem particles in visual.GetComponentsInChildren<ParticleSystem>(true))
            { particles.Clear(true); particles.Play(true); }
        }

        public void ResetPrototype()
        {
            emberIdle = 0f; progress.Reset();
            if (startingCondition == FireCondition.Burning) progress.Ignite();
            else progress.condition = startingCondition;
            Refresh();
        }
    }
}
