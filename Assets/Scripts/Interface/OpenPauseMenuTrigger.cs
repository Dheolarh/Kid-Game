using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Interface
{

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
