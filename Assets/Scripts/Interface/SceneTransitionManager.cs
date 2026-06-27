using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using System.Collections;

namespace KidGame.Interface
{

    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Transition Elements")]
        [Tooltip("The canvas containing the transition overlays. Enabled/Disabled dynamically to save draw calls.")]
        [SerializeField] private Canvas transitionCanvas;

        [Tooltip("Left curtain. Anchored Left-Half, Pivot (1, 0.5).")]
        [SerializeField] private RectTransform leftCurtain;

        [Tooltip("Right curtain. Anchored Right-Half, Pivot (0, 0.5).")]
        [SerializeField] private RectTransform rightCurtain;

        [Header("Loading Screen Elements")]
        [Tooltip("The CanvasGroup holding the loading Lottie and texts.")]
        [SerializeField] private CanvasGroup loadingContentGroup;

        [Tooltip("The TextMeshPro text component displaying the lesson index/number.")]
        [SerializeField] private TMP_Text lessonNumberText;

        [Tooltip("The TextMeshPro text component displaying the lesson title.")]
        [SerializeField] private TMP_Text lessonTitleText;

        [Header("Animation Settings")]
        [SerializeField] private float transitionDuration = 0.8f;
        [SerializeField] private Ease transitionEase = Ease.InOutCubic;

        [Tooltip("The minimum time (in seconds) that the curtains must remain fully closed during a transition.")]
        [SerializeField] private float minimumClosedDuration = 1.5f;

        private bool _isTransitioning = false;

        private void Awake()
        {
            // Singleton Implementation
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null); // Ensure it's root so DontDestroyOnLoad works
                DontDestroyOnLoad(gameObject);
                
                // Ensure canvas is enabled for the opening sequence
                if (transitionCanvas != null)
                {
                    transitionCanvas.enabled = true;
                }
                
                // Force closed state immediately in Awake so loader elements are hidden from frame 0
                ForceClosedState();
            }
            else
            {
                // If another instance already exists in the newly loaded scene, destroy this duplicate
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // If this is the active instance (e.g. at startup or direct scene play in editor),
            // ensure the curtains open to reveal the scene.
            if (Instance == this)
            {
                // Force closed state first so it opens cleanly
                ForceClosedState();
                OpenCurtains();
            }
        }

        /// <summary>
        /// Instantly places the curtains in the closed (center-meeting) state.
        /// </summary>
        private void ForceClosedState()
        {
            if (leftCurtain != null)
            {
                leftCurtain.anchoredPosition = new Vector2(0f, leftCurtain.anchoredPosition.y);
            }
            if (rightCurtain != null)
            {
                rightCurtain.anchoredPosition = new Vector2(0f, rightCurtain.anchoredPosition.y);
            }
            if (transitionCanvas != null)
            {
                transitionCanvas.enabled = true;
            }
            if (loadingContentGroup != null)
            {
                loadingContentGroup.alpha = 0f;
                loadingContentGroup.blocksRaycasts = false;
                loadingContentGroup.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Animates curtains to slide closed (meet in the middle).
        /// </summary>
        public void CloseCurtains(System.Action onComplete = null)
        {
            if (leftCurtain == null || rightCurtain == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (transitionCanvas != null)
            {
                transitionCanvas.enabled = true;
            }

            leftCurtain.DOKill();
            rightCurtain.DOKill();

            Sequence seq = DOTween.Sequence();
            seq.Append(leftCurtain.DOAnchorPosX(0f, transitionDuration).SetEase(transitionEase));
            seq.Join(rightCurtain.DOAnchorPosX(0f, transitionDuration).SetEase(transitionEase));
            seq.OnComplete(() => onComplete?.Invoke());
        }

        /// <summary>
        /// Animates curtains to slide open (slide off-screen).
        /// </summary>
        public void OpenCurtains(System.Action onComplete = null)
        {
            if (leftCurtain == null || rightCurtain == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (transitionCanvas != null)
            {
                transitionCanvas.enabled = true;
            }

            // Target positions: slide completely off-screen left and right
            float leftTargetX = -leftCurtain.rect.width;
            float rightTargetX = rightCurtain.rect.width;

            leftCurtain.DOKill();
            rightCurtain.DOKill();

            Sequence seq = DOTween.Sequence();
            seq.Append(leftCurtain.DOAnchorPosX(leftTargetX, transitionDuration).SetEase(transitionEase));
            seq.Join(rightCurtain.DOAnchorPosX(rightTargetX, transitionDuration).SetEase(transitionEase));
            
            seq.OnComplete(() =>
            {
                // Disable canvas to prevent blocking UI interactions
                if (transitionCanvas != null)
                {
                    transitionCanvas.enabled = false;
                }
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Starts transition by closing curtains, loading the new scene asynchronously,
        /// and opening them back up.
        /// </summary>
        /// <param name="sceneName">Target scene name.</param>
        public void LoadSceneWithTransition(string sceneName)
        {
            if (_isTransitioning) return;
            StartCoroutine(LoadSceneCoroutine(sceneName));
        }

        private IEnumerator LoadSceneCoroutine(string sceneName)
        {
            _isTransitioning = true;

            // Ensure loading screen content is hidden for simple scene transitions
            if (loadingContentGroup != null)
            {
                loadingContentGroup.alpha = 0f;
                loadingContentGroup.blocksRaycasts = false;
                loadingContentGroup.gameObject.SetActive(false);
            }

            // 1. Close curtains
            bool isClosed = false;
            CloseCurtains(() => isClosed = true);
            yield return new WaitUntil(() => isClosed);

            // 2. Load scene in background
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            if (asyncLoad != null)
            {
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }
            else
            {
                Debug.LogError($"[SceneTransitionManager] Scene '{sceneName}' could not be loaded because it has not been added to the Build Settings.");
            }

            // 3. Wait one frame to let scene initialize
            yield return new WaitForEndOfFrame();

            // 4. Open curtains to reveal the new scene
            bool isOpened = false;
            OpenCurtains(() => isOpened = true);
            yield return new WaitUntil(() => isOpened);

            _isTransitioning = false;
        }

        /// <summary>
        /// Starts transition by closing curtains, fading in the loader and level text,
        /// loading the gameplay level, fading the loader out, and opening curtains.
        /// </summary>
        public void LoadLevelWithTransition(string sceneName, string lessonNumber, string lessonTitle)
        {
            if (_isTransitioning) return;
            StartCoroutine(LoadLevelCoroutine(sceneName, lessonNumber, lessonTitle));
        }

        private IEnumerator LoadLevelCoroutine(string sceneName, string lessonNumber, string lessonTitle)
        {
            _isTransitioning = true;

            // 0. Update text values and ensure loading content starts hidden
            if (lessonNumberText != null) lessonNumberText.text = lessonNumber;
            if (lessonTitleText != null) lessonTitleText.text = lessonTitle;
            if (loadingContentGroup != null)
            {
                loadingContentGroup.alpha = 0f;
                loadingContentGroup.blocksRaycasts = false;
                loadingContentGroup.gameObject.SetActive(false);
            }

            // 1. Close curtains
            bool isClosed = false;
            CloseCurtains(() => isClosed = true);
            yield return new WaitUntil(() => isClosed);

            // Record the time when curtains finished closing
            float curtainsClosedTime = Time.time;

            // 2. Fade in loading elements (Lottie & Texts)
            if (loadingContentGroup != null)
            {
                loadingContentGroup.gameObject.SetActive(true);
                // Ensure all child objects (like Loader and Lesson Title) are also active
                foreach (Transform child in loadingContentGroup.transform)
                {
                    child.gameObject.SetActive(true);
                }
                bool isFadeInComplete = false;
                loadingContentGroup.DOFade(1f, 0.4f).OnComplete(() => isFadeInComplete = true);
                yield return new WaitUntil(() => isFadeInComplete);
            }

            // 3. Load level scene in background asynchronously
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            if (asyncLoad != null)
            {
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }
            else
            {
                Debug.LogError($"[SceneTransitionManager] Scene '{sceneName}' could not be loaded because it has not been added to the Build Settings.");
            }

            // 4. Ensure we stay closed for at least the minimum duration
            float elapsedClosedTime = Time.time - curtainsClosedTime;
            float remainingTime = minimumClosedDuration - elapsedClosedTime;
            if (remainingTime > 0f)
            {
                yield return new WaitForSeconds(remainingTime);
            }
            
            yield return new WaitForEndOfFrame();

            // 5. Fade out loading elements (Lottie & Texts) completely
            if (loadingContentGroup != null)
            {
                bool isFadeOutComplete = false;
                loadingContentGroup.DOFade(0f, 0.4f).OnComplete(() =>
                {
                    loadingContentGroup.gameObject.SetActive(false);
                    isFadeOutComplete = true;
                });
                yield return new WaitUntil(() => isFadeOutComplete);
            }

            // 6. Open curtains to reveal the newly loaded level
            bool isOpened = false;
            OpenCurtains(() => isOpened = true);
            yield return new WaitUntil(() => isOpened);

            _isTransitioning = false;
        }

        private void OnDestroy()
        {
            if (leftCurtain != null) leftCurtain.DOKill();
            if (rightCurtain != null) rightCurtain.DOKill();
        }
    }
}
