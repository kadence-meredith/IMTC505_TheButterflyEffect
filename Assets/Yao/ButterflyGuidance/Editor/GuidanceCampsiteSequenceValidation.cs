using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ButterflyEffect.Guidance.Editor
{
    public static class GuidanceCampsiteSequenceValidation
    {
        [MenuItem("Tools/Butterfly Effect/Validate Guided Exploration")]
        public static void Validate() { Run(); }

        public static void Run()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            try
            {
                GuidanceStrictSequenceValidation.Validate();
                report.passed = true;
                report.summary = "Guided exploration passed: automatic timed introductions before Begin, ordered stations, narration before interaction, "
                    + "explicit physical decisions, roasting completion, safe and unsafe outcomes, shared VR/desktop gates, "
                    + "and a completed-route return boundary.";
                UnityEngine.Debug.Log(report.summary);
            }
            catch (Exception error)
            {
                report.failure = error.ToString();
                throw;
            }
            finally
            {
                WriteReport(report, "BUTTERFLY_SEQUENCE_REPORT", "ButterflyCampsiteSequenceValidation.json");
            }
        }

        [MenuItem("Tools/Butterfly Effect/Validate All Guidance")]
        public static void RunAll()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            try
            {
                Validate();
                GuidanceMainSceneValidation.Validate();
                GuidancePlayerModeValidation.Validate();
                report.passed = true;
                report.summary = "Guided exploration, main-scene integration, and desktop/XR mode regressions passed.";
                UnityEngine.Debug.Log(report.summary);
            }
            catch (Exception error)
            {
                report.failure = error.ToString();
                throw;
            }
            finally
            {
                WriteReport(report, "BUTTERFLY_ALL_GUIDANCE_REPORT", "ButterflyAllGuidanceValidation.json");
            }
        }

        public static void OpenCampsiteForPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before opening the campsite.");
            EditorSceneManager.OpenScene("Assets/Main Scenes/1_Campsite_Main.unity", OpenSceneMode.Single);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        private static void WriteReport(Report report, string variable, string fallback)
        {
            string path = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(path)) path = Path.Combine(Path.GetTempPath(), fallback);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, UnityEngine.JsonUtility.ToJson(report, true));
        }

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string summary;
            public string failure;
            public string utc;
        }
    }
}
