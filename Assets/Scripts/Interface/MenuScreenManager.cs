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
        private static bool s_isFirstAppLaunch = true;
        private static bool _hasCompletedIntro = false;

        private void Awake()
        {
            OrientationManager.LockToPortrait();
            if (profileScreenController == null)
            {
                profileScreenController = FindComponentEvenInactive<ProfileScreenController>();
            }
            if (homeScreenIntroController == null)
            {
                homeScreenIntroController = FindComponentEvenInactive<HomeScreenIntroController>();
            }
            if (curtainTransition == null)
            {
                curtainTransition = FindComponentEvenInactive<CurtainTransition>();
            }

            // Auto-assign Screen GameObjects based on resolved controllers
            if (ageSelectScreen == null && profileScreenController != null)
            {
                ageSelectScreen = profileScreenController.gameObject;
            }
            if (splashScreen == null)
            {
                var splash = GameObject.Find("Splash");
                if (splash != null)
                {
                    splashScreen = splash;
                }
            }

            // If returning from another scene during the session, immediately disable splash in Awake before rendering
            if (!s_isFirstAppLaunch)
            {
                if (splashScreen != null) splashScreen.SetActive(false);
                if (ageSelectScreen != null) ageSelectScreen.SetActive(false);
                if (homeScreen != null) homeScreen.SetActive(true);
            }
        }

        public static void MarkFirstLaunchComplete()
        {
            s_isFirstAppLaunch = false;
            _hasCompletedIntro = true;
        }

        private void Start()
        {
            if (profileScreenController == null)
            {
                profileScreenController = FindComponentEvenInactive<ProfileScreenController>();
                Debug.Log($"[MenuScreenManager] Start double-check. profileScreenController assigned? {profileScreenController != null}");
            }
            if (ageSelectScreen == null && profileScreenController != null)
            {
                ageSelectScreen = profileScreenController.gameObject;
            }
            if (homeScreen == null && homeScreenIntroController != null)
            {
                homeScreen = homeScreenIntroController.gameObject;
            }
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
                    if (_hasCompletedIntro || PlayerPrefs.GetInt("HasCompletedProfile", 0) == 1)
                    {
                        TriggerReturningUserSplashTransition();
                    }
                    else
                    {
                        TriggerSplashToAgeSelectTransition();
                    }
                }
            }
        }

        /// <summary>
        /// Sets up the initial state of the screens.
        /// </summary>
        public void InitializeScreens()
        {
            _isTransitioning = false;

            // If intro was already completed, check if this is initial app boot or returning from another scene
            if (_hasCompletedIntro || PlayerPrefs.GetInt("HasCompletedProfile", 0) == 1)
            {
                _hasCompletedIntro = true;

                if (s_isFirstAppLaunch)
                {
                    s_isFirstAppLaunch = false;

                    if (splashScreen != null) splashScreen.SetActive(true);
                    if (ageSelectScreen != null) ageSelectScreen.SetActive(false);
                    if (homeScreen != null) homeScreen.SetActive(false);

                    if (curtainTransition != null)
                    {
                        curtainTransition.ResetTransitionState();
                    }

                    if (autoTransition && splashScreen != null)
                    {
                        StartCoroutine(ReturningUserSplashCoroutine());
                    }
                    else
                    {
                        TriggerReturningUserSplashTransition();
                    }
                }
                else
                {
                    // Returning to Main scene from Level or Game scene -> Go directly to Home screen without splash!
                    if (splashScreen != null) splashScreen.SetActive(false);
                    if (ageSelectScreen != null) ageSelectScreen.SetActive(false);
                    if (homeScreen != null) homeScreen.SetActive(true);

                    KidGame.Audio.AudioManager.Instance?.PlayMainMenuBgm();

                    var controller = StreakPanelController.Instance;
                    if (controller == null)
                    {
                        controller = FindObjectOfType<StreakPanelController>(true);
                    }
                    if (controller != null)
                    {
                        controller.gameObject.SetActive(true);
                        controller.CheckAndShowStreak();
                    }
                }
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
                _isTransitioning = false;
                if (profileScreenController != null)
                {
                    profileScreenController.PlaySetupIntro();
                }
                return;
            }

            // 1. Activate the target screen (Age Select) and its parents/siblings
            if (ageSelectScreen != null)
            {
                ageSelectScreen.SetActive(true);
            }
            if (profileScreenController != null)
            {
                profileScreenController.gameObject.SetActive(true);
                if (profileScreenController.transform.parent != null)
                {
                    profileScreenController.transform.parent.gameObject.SetActive(true);
                }
            }

            // 2. Play the curtain draw animation
            curtainTransition.PlayTransition(OnSplashToAgeSelectComplete);
        }

        private IEnumerator ReturningUserSplashCoroutine()
        {
            yield return new WaitForSeconds(autoTransitionDelay);
            TriggerReturningUserSplashTransition();
        }

        public void TriggerReturningUserSplashTransition()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            if (homeScreen != null) homeScreen.SetActive(true);

            if (curtainTransition == null)
            {
                OnReturningUserSplashComplete();
                return;
            }

            curtainTransition.PlayTransition(OnReturningUserSplashComplete);
        }

        private void OnReturningUserSplashComplete()
        {
            if (splashScreen != null) splashScreen.SetActive(false);
            _isTransitioning = false;
            KidGame.Audio.AudioManager.Instance?.PlayMainMenuBgm();

            var controller = StreakPanelController.Instance;
            if (controller == null)
            {
                controller = FindObjectOfType<StreakPanelController>(true);
            }
            if (controller != null)
            {
                controller.gameObject.SetActive(true);
                controller.CheckAndShowStreak();
            }
        }

        private void OnSplashToAgeSelectComplete()
        {
            Debug.Log("[MenuScreenManager] OnSplashToAgeSelectComplete triggered.");
            s_isFirstAppLaunch = false;
            // 3. Deactivate the Splash screen once curtains are fully opened to optimize performance
            if (splashScreen != null)
            {
                splashScreen.SetActive(false);
                Debug.Log("[MenuScreenManager] Set splashScreen active = false.");
            }
            _isTransitioning = false;

            // Play Registration BGM now that the splash screen curtains are fully open and registration screen shows
            KidGame.Audio.AudioManager.Instance?.PlayRegistrationBgm();

            // Check Privacy Policy acceptance before starting profile setup intro
            var policyController = PrivacyPolicyPanelController.Instance;
            if (policyController == null)
            {
                policyController = FindComponentEvenInactive<PrivacyPolicyPanelController>();
            }

            if (policyController != null && !policyController.HasAcceptedPolicy)
            {
                Debug.Log("[MenuScreenManager] Opening Privacy Policy Panel immediately after splash screen curtain closes.");
                policyController.OpenPanel();
            }
            else
            {
                // Trigger the progressive intro setup animations on the profile screen directly if policy already accepted
                if (profileScreenController != null)
                {
                    Debug.Log("[MenuScreenManager] Invoking profileScreenController.PlaySetupIntro().");
                    profileScreenController.PlaySetupIntro();
                }
                else
                {
                    Debug.LogError("[MenuScreenManager] profileScreenController is null inside OnSplashToAgeSelectComplete!");
                }
            }
        }

        /// <summary>
        /// Transitions from Age Select (Profile) screen to the Home (Main) screen.
        /// Can be called by a button event on the Profile screen.
        /// </summary>
        public void TriggerAgeSelectToHomeTransition()
        {
            _hasCompletedIntro = true; // Mark intro completed to skip it on future loads of this scene

            Debug.Log($"[MenuScreenManager] TriggerAgeSelectToHomeTransition called. ageSelectScreen: {ageSelectScreen}, homeScreen: {homeScreen}");

            // 1. Deactivate the assigned ageSelectScreen (e.g. Background if misassigned)
            if (ageSelectScreen != null)
            {
                ageSelectScreen.SetActive(false);
                Debug.Log($"[MenuScreenManager] Set ageSelectScreen ({ageSelectScreen.name}) inactive.");
            }

            // 2. Deactivate the Greetings container itself
            if (profileScreenController != null)
            {
                profileScreenController.gameObject.SetActive(false);
                Debug.Log($"[MenuScreenManager] Set profileScreenController.gameObject ({profileScreenController.gameObject.name}) inactive.");

                // 3. Deactivate the root Profile canvas parent
                if (profileScreenController.transform.parent != null)
                {
                    profileScreenController.transform.parent.gameObject.SetActive(false);
                    Debug.Log($"[MenuScreenManager] Set profile parent ({profileScreenController.transform.parent.name}) inactive.");
                }
            }

            if (homeScreen != null)
            {
                homeScreen.SetActive(true);
            }

            // Play Main Menu BGM (higher volume)
            KidGame.Audio.AudioManager.Instance?.PlayMainMenuBgm();

            // Trigger the intro animations on the home screen
            if (homeScreenIntroController != null)
            {
                homeScreenIntroController.PlayIntro();

            }
        }

        private T FindComponentEvenInactive<T>() where T : Component
        {
            T comp = FindObjectOfType<T>(true);
            if (comp != null) return comp;

            T[] components = Resources.FindObjectsOfTypeAll<T>();
            foreach (var c in components)
            {
                if (c != null && c.gameObject != null && !c.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy))
                {
                    return c;
                }
            }
            return null;
        }
    }
}
