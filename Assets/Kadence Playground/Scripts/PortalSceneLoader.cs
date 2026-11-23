using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Put script on XR Origin Rig
// Put portal tag on portal
public class PortalSceneLoader : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Exact name of the scene to load (must be in Build Settings)")]
    [SerializeField] private string targetSceneName;

    [Tooltip("Tag assigned to your portal trigger object")]
    [SerializeField] private string portalTag = "portal";

    private bool isTransitioning = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTransitioning) return;

        if (other.CompareTag(portalTag))
        {
            isTransitioning = true;
            StartCoroutine(LoadSceneAsyncRoutine());
        }
    }

    private IEnumerator LoadSceneAsyncRoutine()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("PortalSceneLoader: Target Scene Name is empty!");
            yield break;
        }

        // Load in the background so the VR headset doesn't freeze
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
}