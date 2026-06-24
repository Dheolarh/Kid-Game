using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using KidGame.Interface.Animations;

namespace KidGame.Interface
{
    public class MenuScreenManager : MonoBehaviour
    {
        [Header("Screen References")]
        [Tooltip("The Splash Screen canvas or GameObject.")]
        [SerializeField] private GameObject splashScreen;

        [Tooltip("The Age Select (Profile) Screen canvas or GameObject.")]
        [SerializeField] private GameObject ageSelectScreen;

        [Tooltip("The Home (Main) Screen canvas or GameObject.")]
        [SerializeField] private GameObject homeScreen;

        [Header("Transition Controller")]
        [Tooltip("Reference to the CurtainTransition component.")]
        [SerializeField] private CurtainTransition curtainTransition;

        [Header("Profile Setup Controller")]
        [Tooltip("Reference to the ProfileScreenController component.")]
        [SerializeField] private ProfileScreenController profileScreenController;

        [Header("Home Screen Controller")]
        [Tooltip("Reference to the HomeScreenIntroController component.")]
        [SerializeField] private HomeScreenIntroController homeScreenIntroController;

        [Header("Splash Flow Settings")]
        [Tooltip("If true, the game automatically transitions to Age Select after a delay.")]
        [SerializeField] private bool autoTransition = true;
        
        [Tooltip("Delay in seconds before automatic transition occurs.")]
        [SerializeField] private float autoTransitionDelay = 2.5f;

        [Tooltip("If true, allows the player to tap/click to skip the splash screen delay.")]
        [SerializeField] private bool tapToSkip = true;

        private bool _isTransitioning = false;
        private static bool _hasCompletedIntro = false;

        private void Start()
        {
            InitializeScreens();
        }

        private void Update()
        {
            // Handle optional tap-to-skip functionality while in Splash screen
            if (tapToSkip && splashScreen != null && splashScreen.activeSelf && !_isTransitioning)
            {
                bool tapped = false;

                if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
                {
                    var phase = Touchscreen.current.touches[0].phase.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        tapped = true;
                    }
                }
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    tapped = true;
                }

                if (tapped)
                {
                    StopAllCoroutines();
                    TriggerSplashToAgeSelectTransition();
                }
            }
        }

        /// <summary>
        /// Sets up the initial state of the screens.
        /// </summary>
        public void InitializeScreens()
        {
            _isTransitioning = false;

            // Skip Splash and Profile screens if intro was already completed
            if (_hasCompletedIntro)
            {
                if (splashScreen != null) splashScreen.SetActive(false);
                if (ageSelectScreen != null) ageSelectScreen.SetActive(false);
                if (homeScreen != null) homeScreen.SetActive(true);
                return;
            }

            // Ensure Splash screen is active and others are inactive at start (first run)
            if (splashScreen != null) splashScreen.SetActive(true);
            if (ageSelectScreen != null) ageSelectScreen.SetActive(false);
            if (homeScreen != null) homeScreen.SetActive(false);

            // Make sure the transition curtains are closed initially
            if (curtainTransition != null)
            {
                curtainTransition.ResetTransitionState();
            }

            // Start auto-transition timer if enabled
            if (autoTransition && splashScreen != null)
            {
                StartCoroutine(AutoTransitionCoroutine());
            }
        }

        private IEnumerator AutoTransitionCoroutine()
        {
            yield return new WaitForSeconds(autoTransitionDelay);
            TriggerSplashToAgeSelectTransition();
        }

        /// <summary>
        /// Starts the curtain draw transition from Splash screen to Age Select (Profile) screen.
        /// </summary>
        public void TriggerSplashToAgeSelectTransition()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            if (curtainTransition == null)
            {
                // Fallback if no transition is configured
                Debug.LogWarning("[MenuScreenManager] CurtainTransition reference is missing. Performing instant switch.");
                if (splashScreen != null) splashScreen.SetActive(false);
                if (ageSelectScreen != null) ageSelectScreen.SetActive(true);
                return;
            }

            // 1. Activate the target screen (Age Select) so it's revealed behind the curtains as they open
            if (ageSelectScreen != null)
            {
                ageSelectScreen.SetActive(true);
            }

            // 2. Play the curtain draw animation
            curtainTransition.PlayTransition(OnSplashToAgeSelectComplete);
        }

        private void OnSplashToAgeSelectComplete()
        {
            // 3. Deactivate the Splash screen once curtains are fully opened to optimize performance
            if (splashScreen != null)
            {
                splashScreen.SetActive(false);
            }
            _isTransitioning = false;

            // Trigger the progressive intro setup animations on the profile screen
            if (profileScreenController != null)
            {
                profileScreenController.PlaySetupIntro();
            }
        }

        /// <summary>
        /// Transitions from Age Select (Profile) screen to the Home (Main) screen.
        /// Can be called by a button event on the Profile screen.
        /// </summary>
        public void TriggerAgeSelectToHomeTransition()
        {
            _hasCompletedIntro = true; // Mark intro completed to skip it on future loads of this scene

            if (ageSelectScreen != null) ageSelectScreen.SetActive(false);
            if (homeScreen != null) homeScreen.SetActive(true);

            // Trigger the intro animations on the home screen
            if (homeScreenIntroController != null)
            {
                homeScreenIntroController.PlayIntro();
            }
        }
    }
}
