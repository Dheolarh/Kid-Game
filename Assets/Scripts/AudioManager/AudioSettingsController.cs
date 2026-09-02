using UnityEngine;
using UnityEngine.UI;
using KidGame.Permissions;

namespace KidGame.Audio
{
    /// <summary>
    /// Controls UI Sliders for Music, SFX, Vibration, and Notification in Settings menu.
    /// Handles notification permission verification, OS app settings navigation, and auto re-sync.
    /// </summary>
    public class AudioSettingsController : MonoBehaviour
    {
        [Header("UI Sliders")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider vibrationSlider;
        [SerializeField] private Slider notificationSlider;

        private bool _isSyncingNotification = false;

        private void Start()
        {
            InitializeSliders();
        }

        private void OnEnable()
        {
            // Re-sync sliders whenever settings panel is opened
            InitializeSliders();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                // When returning from OS App Settings, re-sync notification slider
                SyncNotificationSlider();
            }
        }

        private void InitializeSliders()
        {
            if (AudioManager.Instance != null)
            {
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

            // Sync Notification Slider
            SyncNotificationSlider();
        }

        private void SyncNotificationSlider()
        {
            if (notificationSlider == null) return;

            _isSyncingNotification = true;

            bool isEnabled = DevicePermissionManager.IsNotificationEnabled();
            float targetValue = isEnabled ? notificationSlider.maxValue : notificationSlider.minValue;

            notificationSlider.value = targetValue;

            notificationSlider.onValueChanged.RemoveAllListeners();
            notificationSlider.onValueChanged.AddListener(OnNotificationSliderChanged);

            _isSyncingNotification = false;
        }

        private void OnNotificationSliderChanged(float val)
        {
            if (_isSyncingNotification || notificationSlider == null) return;

            bool wantEnable = val > notificationSlider.minValue;

            bool success = DevicePermissionManager.SetNotificationEnabledSetting(wantEnable);

            // Re-sync slider value based on actual permission result
            _isSyncingNotification = true;
            notificationSlider.value = success ? notificationSlider.maxValue : notificationSlider.minValue;
            _isSyncingNotification = false;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonClickSfx();
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

                if (intVal > 0)
                {
                    AudioManager.Instance.Vibrate();
                }
            }
        }
    }
}
