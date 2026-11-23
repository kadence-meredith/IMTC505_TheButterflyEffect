using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace ButterflyEffect.Guidance
{
    /// <summary>Small demo-only view; the movement prefab has no Canvas or narrative dependency.</summary>
    public sealed class ButterflyGuideDemoUI : MonoBehaviour
    {
        public ButterflyPointGuide guide;
        public ButterflyGuideDemoPlayer player;
        public Text dialogue;
        public Text route;
        public Button action;
        public Text actionLabel;

        private void Awake()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("Point demo EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            action.onClick.AddListener(Act);
        }

        private void Update()
        {
            if (guide == null) return;
            bool buttonVisible = false;
            switch (guide.State)
            {
                case ButterflyGuideState.Idle:
                    dialogue.text = "Stay close. I have something to show you.";
                    actionLabel.text = "Begin";
                    buttonVisible = true;
                    break;
                case ButterflyGuideState.Flying:
                    dialogue.text = "Follow me.";
                    break;
                case ButterflyGuideState.WaitingForUser:
                    dialogue.text = "I'll wait here. Come a little closer.";
                    break;
                case ButterflyGuideState.WaitingAtPoint:
                    dialogue.text = "Ready for the next stop?";
                    actionLabel.text = "Continue";
                    buttonVisible = !guide.autoAdvance;
                    break;
                case ButterflyGuideState.Finished:
                    dialogue.text = "We've arrived.";
                    actionLabel.text = "Try again";
                    buttonVisible = true;
                    break;
            }
            action.gameObject.SetActive(buttonVisible);
            route.text = guide.State == ButterflyGuideState.Idle ? "A  >  B" :
                guide.State == ButterflyGuideState.Finished ? "A  >  B   /   ARRIVED" :
                "STOP " + (guide.CurrentPointIndex + 1) + " / " + guide.PointCount;
#if ENABLE_INPUT_SYSTEM
            bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            bool space = Input.GetKeyDown(KeyCode.Space);
#endif
            if (space && buttonVisible) Act();
        }

        private void Act()
        {
            if (guide == null) return;
            switch (guide.State)
            {
                case ButterflyGuideState.Idle: guide.StartGuidance(); break;
                case ButterflyGuideState.WaitingAtPoint: guide.AdvanceToNextPoint(); break;
                case ButterflyGuideState.Finished: player.RestartDemo(); break;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
