using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KidGame.Audio
{
    /// <summary>
    /// Attach to any UI Button to automatically trigger the button click SFX from AudioManager on click.
    /// Uses IPointerClickHandler so it is immune to button.onClick.RemoveAllListeners().
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonClickSfx : MonoBehaviour, IPointerDownHandler
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Only play SFX if the button exists, is active, and is interactable
            if (_button != null && _button.interactable && _button.gameObject.activeInHierarchy)
            {
                AudioManager.Instance?.PlayButtonClickSfx();
            }
        }
    }
}
