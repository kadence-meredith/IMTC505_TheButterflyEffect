using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("The exact name of the scene you want to load.")]
    [SerializeField] private string targetSceneName;

    [Header("Collision Settings")]
    [Tooltip("If checked, it uses OnTriggerEnter. If unchecked, it uses OnCollisionEnter.")]
    [SerializeField] private bool useTrigger = true;

    [Tooltip("Optional: Only change scenes if the object has this specific tag (e.g., 'Player'). Leave empty to trigger on ANY object.")]
    [SerializeField] private string targetTag = "Player";

    // Used if the Collider has "Is Trigger" CHECKED
    private void OnTriggerEnter(Collider other)
    {
        if (useTrigger)
        {
            CheckAndLoadScene(other.gameObject);
        }
    }

    // Used if the Collider has "Is Trigger" UNCHECKED
    private void OnCollisionEnter(Collision collision)
    {
        if (!useTrigger)
        {
            CheckAndLoadScene(collision.gameObject);
        }
    }

    private void CheckAndLoadScene(GameObject hitObject)
    {
        Debug.Log($"Touched object: {hitObject.name} with tag: {hitObject.tag}");
        // If a tag is specified, make sure the hitting object matches it
        if (!string.IsNullOrEmpty(targetTag))
        {
            if (hitObject.CompareTag(targetTag))
            {
                LoadTargetScene();
            }
        }
        else
        {
            // If no tag is specified, anyone can trigger the scene change
            LoadTargetScene();
        }
    }

    private void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogError($"[SceneChangerOnTouch] Target Scene Name is empty on {gameObject.name}!");
        }
    }
}
