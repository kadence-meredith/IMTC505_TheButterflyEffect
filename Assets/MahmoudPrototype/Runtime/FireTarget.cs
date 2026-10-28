using UnityEngine;

namespace ButterflyEffect.Prototype
{
    public sealed class FireTarget : MonoBehaviour, IPrototypeResettable
    {
        public FireSource source;
        public FireProgress progress = new FireProgress();
        public GameObject flames;
        public GameObject embers;
        public float catchRadius = 0.9f;
        public float heatRadius = 1.15f;
        public Vector3 heatOffset = new Vector3(0f, 0.8f, 0f);
        public float emberReigniteDelay = 12f;
        private float emberIdle;
        public bool IsHot => progress.condition == FireCondition.Burning;
        public bool CanRoast(Vector3 point) => IsHot && Vector3.Distance(point, transform.TransformPoint(heatOffset)) < heatRadius;

        private void Start() { Refresh(); }
        private void Update()
        {
            if (progress.condition != FireCondition.HotEmbers) return;
            emberIdle += Time.deltaTime;
            if (emberIdle >= emberReigniteDelay)
            {
                Ignite();
                if (PrototypeSession.Instance != null) PrototypeSession.Instance.Say("The embers were still hot. The fire has reignited.");
            }
        }
        public void Ignite()
        {
            if (progress.condition == FireCondition.Burning) return;
            emberIdle = 0f; progress.Ignite(); Refresh();
        }

        public void LeaveUnlit()
        {
            PrototypeSession session = PrototypeSession.Instance;
            if (session == null) return;
            session.Say(progress.condition == FireCondition.Cold && !session.state.campfireEverLit
                ? "You respected the fire ban and left the campfire unlit."
                : "The campfire has already been lit. Check that its flames and embers are out before leaving.");
        }

        public bool ReceiveWater(Vector3 groundPoint, float amount)
        {
            Vector3 d = groundPoint - transform.position;
            if (new Vector2(d.x, d.z).magnitude > catchRadius || Mathf.Abs(d.y) > 1.1f) return false;
            if (progress.condition == FireCondition.Cold) return false;
            FireCondition before = progress.condition;
            emberIdle = 0f;
            progress.Pour(amount);
            if (before != progress.condition) Refresh();
            PrototypeSession session = PrototypeSession.Instance;
            if (session != null && before != progress.condition)
                session.Say(progress.condition == FireCondition.HotEmbers
                    ? "The flames are gone, but the embers are still hot. Keep pouring."
                    : "The fire and embers are fully extinguished.");
            return true;
        }

        private void Refresh()
        {
            if (flames != null) flames.SetActive(progress.condition == FireCondition.Burning);
            if (embers != null) embers.SetActive(progress.condition != FireCondition.Cold);
            PrototypeSession session = PrototypeSession.Instance;
            if (session == null) return;
            if (source == FireSource.Campfire) session.state.RecordCampfire(progress.condition);
            if (source == FireSource.CigaretteGrass) session.state.cigaretteGrassBurning = progress.condition != FireCondition.Cold;
            if (source == FireSource.FireworksGrass) session.state.fireworksGrassBurning = progress.condition != FireCondition.Cold;
            session.Changed();
        }

        public void ResetPrototype() { emberIdle = 0f; progress.Reset(); Refresh(); }
    }
}
