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
        [Min(.1f)] public float enterRadius = .7f;
        public bool IsTransitioning { get; private set; }

        private void Update() { TryEnter(); }

        public bool TryEnter()
        {
            if (!isActiveAndEnabled || user == null || IsTransitioning) return false;
            Vector3 difference = user.position - transform.position;
            difference.y = 0;
            if (difference.sqrMagnitude > enterRadius * enterRadius
                || !Application.CanStreamedLevelBeLoaded(targetSceneName)) return false;
            IsTransitioning = true;
            SceneManager.LoadSceneAsync(targetSceneName);
            return true;
        }
    }
}
