using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Prototype
{
    [DefaultExecutionOrder(-500)]
    public sealed class PrototypeSession : MonoBehaviour
    {
        public EndingRule endingRule = EndingRule.CampsiteFinal;
        public ChoiceState state = new ChoiceState();
        public UnityEvent onStateChanged = new UnityEvent();
        public bool showDebrief;
        public string message = "";
        public static PrototypeSession Instance { get; private set; }
        public int MissedChoices => state.UnsafeStations(endingRule);
        public int CorrectChoices => 3 - MissedChoices;
        public int ActionsPerformed => state.totalActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearInstance() { Instance = null; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Campus_Start" || scene.name == "0_CampusStart_Main" || scene.name == "1_Campsite_Main")
            { state = new ChoiceState(); showDebrief = false; }
            if (scene.name == "1_Campsite_Main") endingRule = EndingRule.CampsiteFinal;
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }
        public void Changed() { onStateChanged.Invoke(); }
        public void Say(string text) { message = text; Changed(); }
        public void ShowDebrief() { showDebrief = !showDebrief; }
        [ContextMenu("Print Current Results")]
        public void PrintCurrentResults()
        { Debug.Log($"Safe choices: {CorrectChoices}/3 | Errors: {MissedChoices}/3 | Actions: {ActionsPerformed}", this); }

        public void ResetRun()
        {
            DesktopPlayer player = FindFirstObjectByType<DesktopPlayer>();
            if (player != null) player.DropHeld(false);
            state = new ChoiceState(); showDebrief = false; message = "";
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is IPrototypeResettable resettable) resettable.ResetPrototype();
            if (player != null)
            {
                player.GoToStation(0);
                player.GrabStartingCigarette();
                // Equipping the starting item is setup, not a player action.
                state.itemsPickedUp = 0; state.totalActions = 0;
            }
            Changed();
        }
    }
}
