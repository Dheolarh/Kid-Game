using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Interface
{
    /// <summary>
    /// Reusable script to trigger a curtain scene transition on button click.
    /// Ideal for Back buttons, Level buttons, or navigation triggers.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [Tooltip("The name of the scene to load with a curtain transition.")]
        [SerializeField] private string targetSceneName = "Main";

        private Button _button;

        private void Start()
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.AddListener(OnButtonClicked);
            }
        }

        private void OnButtonClicked()
        {
            // Prevent double-clicks
            if (_button != null)
            {
                _button.interactable = false;
            }

            // Load the scene via the Transition Manager
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadSceneWithTransition(targetSceneName);
            }
            else
            {
                Debug.LogWarning($"[SceneTransitionTrigger] SceneTransitionManager not found. Falling back to direct load of scene: {targetSceneName}");
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
            }
        }
    }
}
