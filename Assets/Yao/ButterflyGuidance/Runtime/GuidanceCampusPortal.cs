using ButterflyEffect.Prototype;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Guidance
{
    /// <summary>Desktop entry through the group's visible campus portal.</summary>
    public sealed class GuidanceCampusPortal : MonoBehaviour
    {
        public Transform user;
        public Transform playerRoot;
        public string targetSceneName = "1_Campsite_Main";
        public bool silentComparison;
        [Min(.1f)] public float enterRadius = .7f;
        public bool IsTransitioning { get; private set; }

        private void Update() { TryEnter(); }

        public bool TryEnter()
        {
            if (!isActiveAndEnabled || user == null || IsTransitioning) return false;
            Vector3 difference = user.position - transform.position;
            difference.y = 0;
            if (!PrototypeInteractionPolicy.CanReturn()) return false;
            if (difference.sqrMagnitude > enterRadius * enterRadius
                || !Application.CanStreamedLevelBeLoaded(targetSceneName)) return false;
            if (!PrototypeInteractionPolicy.TryBeginReturn()) return false;
            IsTransitioning = true;
            GuidanceStoryFlow.SilentEndingVisit = silentComparison;
            try
            {
                if (SceneManager.LoadSceneAsync(targetSceneName) != null) return true;
            }
            catch (System.Exception error) { Debug.LogException(error, this); }
            IsTransitioning = false;
            GuidanceStoryFlow.SilentEndingVisit = false;
            PrototypeInteractionPolicy.CancelReturn();
            return false;
        }
    }
}
