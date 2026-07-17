using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Audio
{
    /// <summary>
    /// </summary>
    public class AudioSettingsController : MonoBehaviour
    {
        [Header("UI Sliders (Range 0 - 10 expected)")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider vibrationSlider;

        private void Start()
        {
            InitializeSliders();
        }

        private void OnEnable()
        {
            // Re-sync sliders whenever settings panel is opened
            InitializeSliders();
        }

        private void InitializeSliders()
        {
            if (AudioManager.Instance == null)
            {
                Debug.LogWarning("[AudioSettingsController] AudioManager instance is not ready yet.");
                return;
            }

            // Sync Music Slider
            if (musicSlider != null)
            {
                musicSlider.minValue = 0f;
                musicSlider.maxValue = 10f;
                musicSlider.wholeNumbers = true;
                musicSlider.value = AudioManager.Instance.MusicVolumeSetting;

                musicSlider.onValueChanged.RemoveAllListeners();
                musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            }

            // Sync SFX Slider
            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 10f;
                sfxSlider.wholeNumbers = true;
                sfxSlider.value = AudioManager.Instance.SfxVolumeSetting;

                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }

            // Sync Vibration Slider
            if (vibrationSlider != null)
            {
                vibrationSlider.minValue = 0f;
                vibrationSlider.maxValue = 10f;
                vibrationSlider.wholeNumbers = true;
                vibrationSlider.value = AudioManager.Instance.VibrationSetting;

                vibrationSlider.onValueChanged.RemoveAllListeners();
                vibrationSlider.onValueChanged.AddListener(OnVibrationSliderChanged);
            }
        }

        private void OnMusicSliderChanged(float val)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolumeSetting(Mathf.RoundToInt(val));
            }
        }

        private void OnSfxSliderChanged(float val)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetSfxVolumeSetting(Mathf.RoundToInt(val));
            }
        }

        private void OnVibrationSliderChanged(float val)
        {
            if (AudioManager.Instance != null)
            {
                int intVal = Mathf.RoundToInt(val);
                AudioManager.Instance.SetVibrationSetting(intVal);

                // Play a brief test vibration for immediate feedback if intensity increases (and is not zero)
                if (intVal > 0)
                {
                    AudioManager.Instance.Vibrate();
                }
            }
        }
    }
}
