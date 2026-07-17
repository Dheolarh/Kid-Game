using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KidGame.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Audio Clips")]
        [Tooltip("BGM played during the Splash and Registration screen.")]
        [SerializeField] private AudioClip registrationBgm;
        
        [Tooltip("BGM played in the Main Menu / Level Selection screen.")]
        [SerializeField] private AudioClip mainMenuBgm;

        [Tooltip("A list of gameplay BGM tracks. One will be chosen at random when a level gameplay starts.")]
        [SerializeField] private List<AudioClip> gameplayPlaylist = new List<AudioClip>();

        [Header("SFX Clips")]
        [SerializeField] private AudioClip dialoguePopSfx;
        [SerializeField] private AudioClip countObjectSfx;
        [SerializeField] private AudioClip buttonClickSfx;
        [SerializeField] private AudioClip answerDropSfx;


        [Header("Volume Configuration")]
        [Range(0f, 1f)] [SerializeField] private float registrationVolume = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float mainMenuVolume = 0.25f;
        [Range(0f, 1f)] [SerializeField] private float gameplayVolume = 0.08f; // Reduced from 0.25f to be much softer
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

        [Header("Fade Settings")]
        [SerializeField] private float transitionFadeDuration = 1.0f;

        private Coroutine _fadeCoroutine;
        private AudioClip _lastGameBgmPlayed;



        // Settings Settings (Range 0 - 10)
        public int MusicVolumeSetting { get; private set; }
        public int SfxVolumeSetting { get; private set; }
        public int VibrationSetting { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Load Settings (Range 0-10, Default to 8 for good preset)
            MusicVolumeSetting = PlayerPrefs.GetInt("Setting_MusicVolume", 8);
            SfxVolumeSetting = PlayerPrefs.GetInt("Setting_SfxVolume", 8);
            VibrationSetting = PlayerPrefs.GetInt("Setting_Vibration", 5); // Default vibration in middle

            if (bgmSource == null)
            {
                bgmSource = GetComponent<AudioSource>();
                if (bgmSource == null)
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (sfxSource == null)
            {
                var sources = GetComponents<AudioSource>();
                if (sources.Length > 1)
                {
                    sfxSource = sources[1];
                }
                else
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                }
            }

            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;

            // Apply initial BGM volume if a clip is already playing or pre-set
            UpdateActiveBgmVolume();
        }

        // ── Settings API ──────────────────────────────────────────────────────────

        public void SetMusicVolumeSetting(int val)
        {
            MusicVolumeSetting = Mathf.Clamp(val, 0, 10);
            PlayerPrefs.SetInt("Setting_MusicVolume", MusicVolumeSetting);
            PlayerPrefs.Save();
            UpdateActiveBgmVolume();
        }

        public void SetSfxVolumeSetting(int val)
        {
            SfxVolumeSetting = Mathf.Clamp(val, 0, 10);
            PlayerPrefs.SetInt("Setting_SfxVolume", SfxVolumeSetting);
            PlayerPrefs.Save();
        }

        public void SetVibrationSetting(int val)
        {
            VibrationSetting = Mathf.Clamp(val, 0, 10);
            PlayerPrefs.SetInt("Setting_Vibration", VibrationSetting);
            PlayerPrefs.Save();
        }

        private void UpdateActiveBgmVolume()
        {
            if (bgmSource == null) return;
            
            // Scaled BGM volume based on active track preset and global slider Setting
            float currentPresetVolume = GetCurrentBgmPresetVolume();
            bgmSource.volume = currentPresetVolume * (MusicVolumeSetting / 10f);
        }

        private float GetCurrentBgmPresetVolume()
        {
            if (bgmSource.clip == registrationBgm) return registrationVolume;
            if (bgmSource.clip == mainMenuBgm) return mainMenuVolume;
            return gameplayVolume;
        }

        /// <summary>
        /// Triggers device vibration scaled by intensity setting.
        /// </summary>
        public void Vibrate()
        {
            if (VibrationSetting <= 0) return;

            // Only trigger vibration if on mobile platform
#if UNITY_ANDROID || UNITY_IOS
            // Handheld.Vibrate() is a standard Unity call.
            // On some platforms we can customize it or trigger it directly.
            // If the vibration setting is low, we might skip or do normal vibration depending on device capabilities.
            if (Application.isPlaying)
            {
                Handheld.Vibrate();
            }
#else
            Debug.Log($"[AudioManager] Vibrate triggered (Intensity: {VibrationSetting}/10)");
#endif
        }



        /// <summary>
        /// Plays the registration screen BGM at low volume.
        /// </summary>
        public void PlayRegistrationBgm()
        {
            TransitionBgm(registrationBgm, registrationVolume);
        }

        /// <summary>
        /// Transition BGM to main menu volume. If it's a different track, it will crossfade.
        /// </summary>
        public void PlayMainMenuBgm()
        {
            TransitionBgm(mainMenuBgm, mainMenuVolume);
        }

        /// <summary>
        /// Transitions to a random BGM from the gameplay playlist.
        /// </summary>
        public void PlayRandomGameplayBgm()
        {
            if (gameplayPlaylist == null || gameplayPlaylist.Count == 0)
            {
                Debug.LogWarning("[AudioManager] Gameplay playlist is empty. Cannot transition BGM.");
                return;
            }

            AudioClip selectedClip = null;
            if (gameplayPlaylist.Count == 1)
            {
                selectedClip = gameplayPlaylist[0];
            }
            else
            {
                // Select a random track, ensuring it's not the same as the last played one if possible
                List<AudioClip> options = new List<AudioClip>(gameplayPlaylist);
                if (_lastGameBgmPlayed != null && options.Contains(_lastGameBgmPlayed))
                {
                    options.Remove(_lastGameBgmPlayed);
                }
                selectedClip = options[Random.Range(0, options.Count)];
            }

            Debug.Log($"[AudioManager] Transitioning to gameplay BGM: {selectedClip.name} at volume {gameplayVolume}");
            _lastGameBgmPlayed = selectedClip;
            TransitionBgm(selectedClip, gameplayVolume);
        }


        /// <summary>
        /// Handles smooth transition (fade-out/fade-in or volume adjustments) of background music.
        /// </summary>
        public void TransitionBgm(AudioClip newClip, float targetVolume)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeToBgmRoutine(newClip, targetVolume));
        }

        private IEnumerator FadeToBgmRoutine(AudioClip newClip, float targetVolume)
        {
            float musicScalar = MusicVolumeSetting / 10f;
            float finalTargetVolume = targetVolume * musicScalar;

            // If the clip is the same and already playing, just smoothly adjust the volume
            if (bgmSource.clip == newClip && bgmSource.isPlaying)
            {
                float startVol = bgmSource.volume;
                float elapsed = 0f;
                while (elapsed < transitionFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    bgmSource.volume = Mathf.Lerp(startVol, finalTargetVolume, elapsed / transitionFadeDuration);
                    yield return null;
                }
                bgmSource.volume = finalTargetVolume;
                yield break;
            }

            // Fade out current BGM if playing
            if (bgmSource.isPlaying && bgmSource.volume > 0)
            {
                float startVol = bgmSource.volume;
                float elapsed = 0f;
                while (elapsed < transitionFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / transitionFadeDuration);
                    yield return null;
                }
            }

            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.volume = 0f;

            if (newClip != null)
            {
                bgmSource.Play();

                // Fade in new BGM
                float elapsed = 0f;
                while (elapsed < transitionFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    bgmSource.volume = Mathf.Lerp(0f, finalTargetVolume, elapsed / transitionFadeDuration);
                    yield return null;
                }
                bgmSource.volume = finalTargetVolume;
            }
        }

        /// <summary>
        /// Plays the dialogue popup SFX once.
        /// </summary>
        public void PlayDialoguePopSfx()
        {
            if (sfxSource != null && dialoguePopSfx != null)
            {
                float finalVol = sfxVolume * (SfxVolumeSetting / 10f);
                sfxSource.PlayOneShot(dialoguePopSfx, finalVol);
            }
        }

        /// <summary>
        /// Plays the count object tap SFX once.
        /// </summary>
        public void PlayCountObjectSfx()
        {
            if (sfxSource != null && countObjectSfx != null)
            {
                float finalVol = sfxVolume * (SfxVolumeSetting / 10f);
                sfxSource.PlayOneShot(countObjectSfx, finalVol);
            }
        }

        /// <summary>
        /// Plays the generic button click SFX once.
        /// </summary>
        public void PlayButtonClickSfx()
        {
            if (sfxSource != null && buttonClickSfx != null)
            {
                float finalVol = sfxVolume * (SfxVolumeSetting / 10f);
                sfxSource.PlayOneShot(buttonClickSfx, finalVol);
            }
        }

        /// <summary>
        /// Plays the card/object drop success SFX once.
        /// </summary>
        public void PlayAnswerDropSfx()
        {
            if (sfxSource != null && answerDropSfx != null)
            {
                float finalVol = sfxVolume * (SfxVolumeSetting / 10f);
                sfxSource.PlayOneShot(answerDropSfx, finalVol);
            }
        }

    }
}

