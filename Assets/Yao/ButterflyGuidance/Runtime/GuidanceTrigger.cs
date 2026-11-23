using UnityEngine;
using UnityEngine.Events;

namespace ButterflyEffect.Guidance
{
    /// <summary>Connect a teammate's trigger volume, UI button, or XR UnityEvent to a library line.</summary>
    public sealed class GuidanceTrigger : MonoBehaviour
    {
        public GuidanceDirector director;
        public string dialogueId = "campus.butterfly";
        public bool playOnce = true;
        public string requiredTag = "Player";
        public UnityEvent onPlayed = new UnityEvent();
        private bool played;

        public void Play()
        {
            if (director == null || (playOnce && played)) return;
            director.Show(dialogueId);
            played = true;
            onPlayed.Invoke();
        }
        public void ResetTrigger() { played = false; }
        private void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag)) Play();
        }
    }
}
