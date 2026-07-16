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
        [Range(0f, 1f)] [SerializeField] private float gameplayVolume = 0.25f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;

        [Header("Fade Settings")]
        [SerializeField] private float transitionFadeDuration = 1.0f;

        private Coroutine _fadeCoroutine;
        private AudioClip _lastGameBgmPlayed;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

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
                // Try to find a second AudioSource on the game object, or add one
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
                Debug.LogWarning("[AudioManager] Gameplay playlist is empty.");
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
            // If the clip is the same and already playing, just smoothly adjust the volume
            if (bgmSource.clip == newClip && bgmSource.isPlaying)
            {
                float startVol = bgmSource.volume;
                float elapsed = 0f;
                while (elapsed < transitionFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    bgmSource.volume = Mathf.Lerp(startVol, targetVolume, elapsed / transitionFadeDuration);
                    yield return null;
                }
                bgmSource.volume = targetVolume;
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
                    bgmSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / transitionFadeDuration);
                    yield return null;
                }
                bgmSource.volume = targetVolume;
            }
        }

        /// <summary>
        /// Plays the dialogue popup SFX once.
        /// </summary>
        public void PlayDialoguePopSfx()
        {
            if (sfxSource != null && dialoguePopSfx != null)
            {
                sfxSource.PlayOneShot(dialoguePopSfx, sfxVolume);
            }
        }

        /// <summary>
        /// Plays the count object tap SFX once.
        /// </summary>
        public void PlayCountObjectSfx()
        {
            if (sfxSource != null && countObjectSfx != null)
            {
                sfxSource.PlayOneShot(countObjectSfx, sfxVolume);
            }
        }

        /// <summary>
        /// Plays the generic button click SFX once.
        /// </summary>
        public void PlayButtonClickSfx()
        {
            if (sfxSource != null && buttonClickSfx != null)
            {
                sfxSource.PlayOneShot(buttonClickSfx, sfxVolume);
            }
        }

        /// <summary>
        /// Plays the card/object drop success SFX once.
        /// </summary>
        public void PlayAnswerDropSfx()
        {
            if (sfxSource != null && answerDropSfx != null)
            {
                sfxSource.PlayOneShot(answerDropSfx, sfxVolume);
            }
        }
    }
}

