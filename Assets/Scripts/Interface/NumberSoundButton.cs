using UnityEngine;
using UnityEngine.UI;
using KidGame.Audio;
using KidGame.Mechanics.Tracing;

namespace KidGame.Interface
{
    
    [RequireComponent(typeof(Button))]
    public class NumberSoundButton : MonoBehaviour
    {
        [Tooltip("Optional AudioSource attached to this sound button. If left null, uses component on this GameObject.")]
        [SerializeField] private AudioSource audioSource;

        private Button _button;

        /// <summary>
        /// Explicit value to play, set by GameFlowManager from PageData.soundButtonValue.
        /// When empty, the button falls back to the original auto-detect behaviour used by the
        /// Tracing game (which is intentionally left untouched).
        /// </summary>
        private string _overrideValue = "";

        public void SetOverrideValue(string value)
        {
            _overrideValue = string.IsNullOrEmpty(value) ? "" : value.Trim().Trim('\'', '"');
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (_button != null)
            {
                _button.onClick.RemoveListener(PlayActiveNumberSound);
                _button.onClick.AddListener(PlayActiveNumberSound);
            }
        }

        public void PlayActiveNumberSound()
        {
            // Explicit level-author value wins when present (Sound Button setting on the page).
            // PlayNumberVoice already routes letters to PlayLetterVoice, so one call covers both.
            string numberStr = !string.IsNullOrEmpty(_overrideValue)
                ? _overrideValue
                : GetActiveNumberToTrace();

            if (string.IsNullOrEmpty(numberStr))
            {
                Debug.LogWarning("[NumberSoundButton] No active number found for current level.");
                return;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayNumberVoice(numberStr, audioSource);
            }
            else
            {
                // Fallback direct load if AudioManager instance not present
                string cleanNum = numberStr.Trim().Trim('\'', '"');
                AudioClip clip = Resources.Load<AudioClip>($"Audio/1 - 50/{cleanNum}");
                if (clip != null && audioSource != null)
                {
                    audioSource.PlayOneShot(clip);
                }
            }
        }

        private string GetActiveNumberToTrace()
        {
            // 1. Check active TracingModeManager in scene
            var tracingManager = Object.FindFirstObjectByType<TracingModeManager>();
            if (tracingManager != null && tracingManager.ValuesToTrace != null && tracingManager.ValuesToTrace.Count > 0)
            {
                return tracingManager.ValuesToTrace[0].Trim().Trim('\'', '"');
            }

            // 2. Check GameFlowManager ActiveLevel page
            if (GameFlowManager.Instance != null && GameFlowManager.ActiveLevel != null)
            {
                var level = GameFlowManager.ActiveLevel;
                int pageIdx = GameFlowManager.Instance.CurrentPageIndex;
                if (pageIdx >= 0 && pageIdx < level.pages.Count)
                {
                    var page = level.pages[pageIdx];
                    if (page.tracingValuesToTrace != null && page.tracingValuesToTrace.Count > 0)
                    {
                        return page.tracingValuesToTrace[0].Trim().Trim('\'', '"');
                    }
                }
            }

            return "";
        }
    }
}
