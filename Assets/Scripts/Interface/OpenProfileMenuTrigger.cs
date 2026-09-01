using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Interface
{
    /// <summary>
    /// Attached to a Button to open the Profile Menu popup.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class OpenProfileMenuTrigger : MonoBehaviour
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
            if (ProfileMenuController.Instance != null)
            {
                ProfileMenuController.Instance.OpenMenu();
            }
            else
            {
                Debug.LogWarning("[OpenProfileMenuTrigger] No active ProfileMenuController instance found in the scene!");
            }
        }
    }
}
