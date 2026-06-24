using UnityEngine;
using UnityEngine.SceneManagement;
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

        [Header("Animation Settings")]
        [SerializeField] private float transitionDuration = 0.8f;
        [SerializeField] private Ease transitionEase = Ease.InOutCubic;

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
            StartCoroutine(LoadSceneCoroutine(sceneName));
        }

        private IEnumerator LoadSceneCoroutine(string sceneName)
        {
            // 1. Close curtains
            bool isClosed = false;
            CloseCurtains(() => isClosed = true);
            yield return new WaitUntil(() => isClosed);

            // 2. Load scene in background
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // 3. Wait one frame to let scene initialize
            yield return new WaitForEndOfFrame();

            // 4. Open curtains to reveal the new scene
            bool isOpened = false;
            OpenCurtains(() => isOpened = true);
            yield return new WaitUntil(() => isOpened);
        }

        private void OnDestroy()
        {
            if (leftCurtain != null) leftCurtain.DOKill();
            if (rightCurtain != null) rightCurtain.DOKill();
        }
    }
}
