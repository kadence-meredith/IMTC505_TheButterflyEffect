using UnityEngine;
using UnityEngine.SceneManagement;

namespace ButterflyEffect.Prototype
{
    public sealed class CampusReturnPortal : MonoBehaviour
    {
        public string healthyCampus = "5_CampusEnd_Main";
        public string partialCampus = "2_CampusFire_Main";
        public string heavyCampus = "3_CampusFire_Main";
        public string destroyedCampus = "4_CampusFire_Main";

        private bool loading;
        public bool CanReturnNow => !loading && !PrototypeInteractionPolicy.ReturnInProgress
            && PrototypeInteractionPolicy.CanReturn();

        private void OnTriggerEnter(Collider other)
        {
            // A thrown or held item must not activate the portal.
            if (other.GetComponentInParent<PrototypeItem>() != null)
                return;

            // Allows a collider on the player or its children.
            for (Transform current = other.transform;
                 current != null;
                 current = current.parent)
            {
                if (current.CompareTag("Player"))
                {
                    ReturnToCampus();
                    return;
                }
            }
        }

        [ContextMenu("Return To Campus")]
        public void ReturnToCampus()
        {
            if (!Application.isPlaying || !CanReturnNow)
                return;

            PrototypeSession session = PrototypeSession.Instance;

            if (session == null)
            {
                Debug.LogError(
                    "No GameSession with PrototypeSession was found.",
                    this);
                return;
            }

            int errors = session.hasFinalResult
                ? session.finalMissedChoices
                : session.state.UnsafeStations(
                    EndingRule.CampsiteFinal);

            string[] destinations =
            {
                healthyCampus,
                partialCampus,
                heavyCampus,
                destroyedCampus
            };

            if (errors < 0 || errors >= destinations.Length)
            {
                Debug.LogError(
                    "The safety error count must be between 0 and 3.",
                    this);
                return;
            }

            string destination = destinations[errors];

            if (string.IsNullOrWhiteSpace(destination) ||
                !Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError(
                    "Add and enable this scene in the active Build Profile: "
                    + destination,
                    this);
                return;
            }

            bool alreadyCaptured = session.hasFinalResult;
            if (!PrototypeInteractionPolicy.TryBeginReturn()) return;
            loading = true;

            try
            {
                session.CaptureFinalResults();

                Debug.Log(
                    $"Safe choices: {session.CorrectChoices}/3 | "
                    + $"Errors: {session.MissedChoices}/3 | "
                    + $"Actions: {session.ActionsPerformed} | "
                    + $"Loading: {destination}");

                AsyncOperation operation =
                    SceneManager.LoadSceneAsync(
                        destination,
                        LoadSceneMode.Single);

                if (operation == null)
                {
                    throw new System.InvalidOperationException(
                        "The scene load could not start.");
                }
            }
            catch (System.Exception exception)
            {
                loading = false;
                PrototypeInteractionPolicy.CancelReturn();

                if (!alreadyCaptured)
                    session.ClearFinalResults();

                Debug.LogException(exception, this);
            }
        }
    }
}
