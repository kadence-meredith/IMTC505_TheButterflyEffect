using ButterflyEffect.Prototype;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Guidance
{
    /// <summary>Returns through the existing visible portal using the real campsite result.</summary>
    public sealed class GuidanceReturnPortal : MonoBehaviour
    {
        public Transform user;
        public Transform playerRoot;
        public string[] endingScenes =
        {
            "5_CampusEnd_Main", "2_CampusFire_Main", "3_CampusFire_Main", "4_CampusFire_Main"
        };
        [Min(.1f)] public float enterRadius = .7f;
        [Min(.2f)] public float exitRadius = 1.5f;
        public bool IsArmed { get; private set; }
        public bool IsTransitioning { get; private set; }

        // A new scene spawns beside this portal. First leaving it prevents a bounce
        // straight into an ending before the user has explored the campsite.
        private void Update()
        {
            if (user == null || IsTransitioning) return;
            Vector3 delta = user.position - transform.position;
            delta.y = 0;
            if (delta.magnitude > Mathf.Max(enterRadius + .1f, exitRadius)) IsArmed = true;
            if (IsArmed && delta.magnitude <= enterRadius) TryEnter();
        }

        public void Arm()
        {
            if (user == null) return;
            Vector3 delta = user.position - transform.position;
            delta.y = 0;
            if (delta.magnitude > enterRadius) IsArmed = true;
        }

        public string ResolveEndingScene()
        {
            var session = PrototypeSession.Instance;
            int index = session == null ? 3 : session.state.UnsafeStations(EndingRule.CampsiteFinal);
            return endingScenes != null && index >= 0 && index < endingScenes.Length
                ? endingScenes[index] : string.Empty;
        }

        public bool TryEnter()
        {
            if (IsTransitioning || !IsArmed || user == null || !PrototypeInteractionPolicy.CanReturn()) return false;
            Vector3 delta = user.position - transform.position;
            delta.y = 0;
            if (delta.magnitude > enterRadius) return false;
            string scene = ResolveEndingScene();
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return false;
            if (!PrototypeInteractionPolicy.TryBeginReturn()) return false;
            IsTransitioning = true;
            var session = PrototypeSession.Instance;
            bool captured = session != null && session.hasFinalResult;
            try
            {
                if (session != null && !captured) session.CaptureFinalResults();
                if (SceneManager.LoadSceneAsync(scene) != null) return true;
            }
            catch (System.Exception error) { Debug.LogException(error, this); }
            if (session != null && !captured) session.ClearFinalResults();
            IsTransitioning = false;
            PrototypeInteractionPolicy.CancelReturn();
            return false;
        }
    }
}
