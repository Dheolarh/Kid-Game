using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Interface
{
    /// <summary>
    /// Attach this script to any Pause Button in the gameplay scene.
    /// When clicked, it will automatically find the active PauseMenuController (Instance)
    /// in the scene and call OpenMenu().
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class OpenPauseMenuTrigger : MonoBehaviour
    {
        private Button _button;

        private void Start()
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.AddListener(TriggerOpen);
            }
        }

        private void TriggerOpen()
        {
            if (PauseMenuController.Instance != null)
            {
                PauseMenuController.Instance.OpenMenu();
            }
            else
            {
                Debug.LogWarning("[OpenPauseMenuTrigger] No active PauseMenuController instance found in the scene! Make sure your Pause Prefab is active/instantiated.");
            }
        }
    }
}
