using UnityEngine;
using UnityEngine.Events;

namespace ButterflyEffect.Prototype
{
    public sealed class PrototypeSession : MonoBehaviour
    {
        [Tooltip("OriginalPitch retains unsafe actions even after cleanup. RemainingHazards evaluates what is left behind.")]
        public EndingRule endingRule = EndingRule.OriginalPitch;
        public ChoiceState state = new ChoiceState();
        public UnityEvent onStateChanged = new UnityEvent();
        public bool showDebrief;
        public string message = "You will need a free hand for that marshmallow. Dispose of the cigarette, or drop it.";
        public static PrototypeSession Instance { get; private set; }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public void Changed() { onStateChanged.Invoke(); }
        public void Say(string text) { message = text; Changed(); }
        public void ShowDebrief() { showDebrief = !showDebrief; }

        public void ResetRun()
        {
            DesktopPlayer player = FindFirstObjectByType<DesktopPlayer>();
            if (player != null) player.DropHeld(false);
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
                if (behaviour is IPrototypeResettable resettable) resettable.ResetPrototype();
            state = new ChoiceState();
            showDebrief = false;
            if (player != null) { player.GoToStation(0); player.GrabStartingCigarette(); }
            Say("New run. Keep holding the cigarette, drop it, or dispose of it in the blue bin.");
        }
    }
}
