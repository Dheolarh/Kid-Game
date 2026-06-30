using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace KidGame.Interface
{
    [ExecuteAlways]
    public class LevelSelectManager : MonoBehaviour
    {
        [Header("Scroll References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewportRt;
        [SerializeField] private RectTransform contentRt;

        [Header("Prefabs")]
        [SerializeField] private GameObject levelButtonPrefab;

        [Header("Navigation Buttons")]
        [SerializeField] private Button leftButton;
        [SerializeField] private Button rightButton;
        [SerializeField] private TMP_Text pageIndicatorText;

        [Header("Dynamic Database Settings")]
        [SerializeField] private LevelDatabase levelDatabase; // Drop your LevelDatabase asset here!
        [SerializeField] private int levelsPerPage = 9;
        [SerializeField] private float transitionDuration = 0.4f;
        [SerializeField] private Ease transitionEase = Ease.OutQuad;

        [Header("Grid Layout Settings for Pages")]
        [SerializeField] private Vector2 cellSize = new Vector2(100f, 100f);
        [SerializeField] private Vector2 spacing = new Vector2(30f, 30f);
        [SerializeField] private int gridConstraintCount = 3; // 3 columns

        private int _currentPage = 0;
        private int _totalPages = 1;
        private Tween _scrollTween;
        private Vector2 _lastViewportSize;

        private void Start()
        {
            InitializeLevelSelect();
        }

        private void Update()
        {
            // Dynamically handle screen resolution/viewport size changes
            if (viewportRt != null && viewportRt.rect.size != _lastViewportSize)
            {
                _lastViewportSize = viewportRt.rect.size;
                UpdatePageSizes();
            }
        }

        public void InitializeLevelSelect()
        {
            if (scrollRect == null || contentRt == null || viewportRt == null)
            {
                Debug.LogError("[LevelSelectManager] Missing critical scroll references!");
                return;
            }

            // Clean up existing page containers cleanly
            for (int i = contentRt.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRt.GetChild(i);
                child.SetParent(null);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            if (levelDatabase == null || levelDatabase.allLevels.Count == 0)
            {
                Debug.LogWarning("[LevelSelectManager] LevelDatabase is empty or unassigned.");
                return;
            }

            int totalLevels = levelDatabase.allLevels.Count;
            _totalPages = Mathf.CeilToInt((float)totalLevels / levelsPerPage);
            if (_totalPages < 1) _totalPages = 1;

            ConfigureContentLayout();

            // Spawn dynamic pages and buttons based on Database entry counts
            int spawnedLevels = 0;
            for (int p = 0; p < _totalPages; p++)
            {
                GameObject pageGo = new GameObject($"Page_{p + 1}", typeof(RectTransform));
                RectTransform pageRt = pageGo.GetComponent<RectTransform>();
                pageRt.SetParent(contentRt, false);

                // Page setup
                LayoutElement le = pageGo.AddComponent<LayoutElement>();
                le.preferredWidth = viewportRt.rect.width;
                le.preferredHeight = viewportRt.rect.height;

                GridLayoutGroup grid = pageGo.AddComponent<GridLayoutGroup>();
                grid.cellSize = cellSize;
                grid.spacing = spacing;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.UpperCenter;
                grid.padding = new RectOffset(20, 20, 30, 30);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = gridConstraintCount;

                int levelsOnThisPage = Mathf.Min(levelsPerPage, totalLevels - spawnedLevels);
                for (int i = 0; i < levelsOnThisPage; i++)
                {
                    LevelData data = levelDatabase.allLevels[spawnedLevels];
                    spawnedLevels++;

                    if (levelButtonPrefab != null)
                    {
                        GameObject btnGo = Instantiate(levelButtonPrefab, pageRt);
                        btnGo.name = $"Level_{spawnedLevels} - {data.levelName}";

                        // 1. Update text label dynamically
                        TMP_Text label = btnGo.GetComponentInChildren<TMP_Text>();
                        if (label != null)
                        {
                            label.text = spawnedLevels.ToString();
                        }

                        // 2. Setup progression state (unlocked state check using level number)
                        bool isUnlocked = PlayerPrefs.GetInt($"Level_Unlocked_{data.levelNumber}", (data.isUnlockedByDefault || spawnedLevels == 1) ? 1 : 0) == 1;

                        // 3. Style level button and stars
                        var levelUI = btnGo.GetComponent<LevelButtonUI>();
                        int starsCount = PlayerPrefs.GetInt($"Level_Stars_{data.levelNumber}", 0);
                        if (levelUI != null)
                        {
                            if (levelUI.buttonBackground != null)
                            {
                                Color normalColor = data.levelThemeColor;
                                // Create a faded/desaturated version of the theme color for locked buttons
                                Color fadedColor = new Color(
                                    Mathf.Lerp(normalColor.r, 0.5f, 0.5f),
                                    Mathf.Lerp(normalColor.g, 0.5f, 0.5f),
                                    Mathf.Lerp(normalColor.b, 0.5f, 0.5f),
                                    0.5f
                                );
                                levelUI.buttonBackground.color = isUnlocked ? normalColor : fadedColor;
                            }

                            for (int s = 0; s < levelUI.stars.Length; s++)
                            {
                                if (levelUI.stars[s] != null)
                                {
                                    levelUI.stars[s].color = (s < starsCount) ? levelUI.activeStarColor : levelUI.inactiveStarColor;
                                }
                            }
                        }

                        Button btn = btnGo.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.interactable = isUnlocked;

                            if (Application.isPlaying)
                            {
                                int currentLevelIndex = spawnedLevels;
                                btn.onClick.AddListener(() => OnLevelSelected(data, currentLevelIndex));
                            }
                        }
                    }
                }
            }

            if (leftButton != null && Application.isPlaying)
            {
                leftButton.onClick.RemoveAllListeners();
                leftButton.onClick.AddListener(PreviousPage);
            }

            if (rightButton != null && Application.isPlaying)
            {
                rightButton.onClick.RemoveAllListeners();
                rightButton.onClick.AddListener(NextPage);
            }

            Canvas.ForceUpdateCanvases();
            _currentPage = 0;
            scrollRect.horizontalNormalizedPosition = 0f;

            UpdateNavigationUI();
            UpdatePageSizes();
        }

        private void ConfigureContentLayout()
        {
            // Configure ScrollRect
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            // Ensure Content has HorizontalLayoutGroup and ContentSizeFitter configured correctly
            HorizontalLayoutGroup hlg = contentRt.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = contentRt.gameObject.AddComponent<HorizontalLayoutGroup>();

            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.spacing = 0f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            ContentSizeFitter csf = contentRt.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = contentRt.gameObject.AddComponent<ContentSizeFitter>();

            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private void UpdatePageSizes()
        {
            if (contentRt == null || viewportRt == null) return;

            float w = viewportRt.rect.width;
            float h = viewportRt.rect.height;

            // Calculate dynamic cell size based on viewport dimensions
            int cols = gridConstraintCount;
            int rows = Mathf.CeilToInt((float)levelsPerPage / cols);
            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;

            // Dynamic padding & spacing that scales with viewport height to maximize button space on short/tablet screens
            float paddingTop = Mathf.Clamp(h * 0.08f, 10f, 35f);
            float paddingBottom = Mathf.Clamp(h * 0.08f, 10f, 35f);
            float paddingLeft = Mathf.Clamp(w * 0.06f, 10f, 30f);
            float paddingRight = Mathf.Clamp(w * 0.06f, 10f, 30f);

            float dynamicSpacingX = Mathf.Clamp(w * 0.06f, 10f, 25f);
            float dynamicSpacingY = Mathf.Clamp(h * 0.06f, 10f, 25f);

            float availW = w - paddingLeft - paddingRight - dynamicSpacingX * (cols - 1);
            float cellW = cols > 0 ? (availW / cols) : 90f;

            float availH = h - paddingTop - paddingBottom - dynamicSpacingY * (rows - 1);
            float cellH = rows > 0 ? (availH / rows) : 90f;

            // Maintain perfectly square buttons scaled to fit the whiteboard area, extending minimum clamp down to 35f for short screens
            float optimalSize = Mathf.Min(cellW, cellH);
            optimalSize = Mathf.Clamp(optimalSize, 35f, 150f);

            Vector2 dynamicCellSize = new Vector2(optimalSize, optimalSize);

            foreach (Transform child in contentRt)
            {
                LayoutElement le = child.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.preferredWidth = w;
                    le.preferredHeight = h;
                }

                GridLayoutGroup grid = child.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    grid.padding = new RectOffset((int)paddingLeft, (int)paddingRight, (int)paddingTop, (int)paddingBottom);
                    grid.cellSize = dynamicCellSize;
                    grid.spacing = new Vector2(dynamicSpacingX, dynamicSpacingY);
                }
            }

            // Dynamically scale and position the bottom navigation panel components relative to screen size
            float navBtnSize = Mathf.Clamp(optimalSize * 0.65f, 50f, 75f); // Scale nav buttons proportionally to level buttons
            float navOffset = Mathf.Clamp(w * 0.32f, 95f, 150f);            // Space out arrows based on board width

            if (leftButton != null)
            {
                RectTransform leftRt = leftButton.GetComponent<RectTransform>();
                if (leftRt != null)
                {
                    leftRt.sizeDelta = new Vector2(navBtnSize, navBtnSize);
                    leftRt.anchoredPosition = new Vector2(-navOffset, 15f);
                }
            }

            if (rightButton != null)
            {
                RectTransform rightRt = rightButton.GetComponent<RectTransform>();
                if (rightRt != null)
                {
                    rightRt.sizeDelta = new Vector2(navBtnSize, navBtnSize);
                    rightRt.anchoredPosition = new Vector2(navOffset, 15f);
                }
            }

            if (pageIndicatorText != null)
            {
                RectTransform indicatorRt = pageIndicatorText.GetComponent<RectTransform>();
                if (indicatorRt != null)
                {
                    // Slightly adjust height and vertical position to align with larger/smaller arrows
                    indicatorRt.anchoredPosition = new Vector2(0f, 15f + (navBtnSize - 35f) * 0.5f);
                    pageIndicatorText.fontSize = Mathf.Clamp(optimalSize * 0.28f, 18f, 26f); // Scale font size proportionally
                }
            }

            // Update ScrollRect positions smoothly on size change
            if (Application.isPlaying)
            {
                ScrollToPage(_currentPage);
            }
            else
            {
                Canvas.ForceUpdateCanvases();
                float targetNormalizedPos = _totalPages > 1 ? (float)_currentPage / (_totalPages - 1) : 0f;
                scrollRect.horizontalNormalizedPosition = targetNormalizedPos;
            }
        }

        public void NextPage()
        {
            if (_currentPage < _totalPages - 1)
            {
                ScrollToPage(_currentPage + 1);
            }
        }

        public void PreviousPage()
        {
            if (_currentPage > 0)
            {
                ScrollToPage(_currentPage - 1);
            }
        }

        private void ScrollToPage(int pageIndex)
        {
            _currentPage = Mathf.Clamp(pageIndex, 0, _totalPages - 1);

            float targetNormalizedPos = _totalPages > 1 ? (float)_currentPage / (_totalPages - 1) : 0f;

            // Kill any active tween to prevent conflicts
            if (_scrollTween != null && _scrollTween.IsActive())
            {
                _scrollTween.Kill();
            }

            // Smoothly tween the horizontal normalized position
            _scrollTween = scrollRect.DONormalizedPos(new Vector2(targetNormalizedPos, 0f), transitionDuration)
                .SetEase(transitionEase)
                .OnComplete(UpdateNavigationUI);

            UpdateNavigationUI();
        }

        private void UpdateNavigationUI()
        {
            if (leftButton != null)
            {
                leftButton.interactable = _currentPage > 0;
            }

            if (rightButton != null)
            {
                rightButton.interactable = _currentPage < _totalPages - 1;
            }

            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = $"{_currentPage + 1}/{_totalPages}";
            }
        }

        private void OnLevelSelected(LevelData data, int levelIndex)
        {
            Debug.Log($"[LevelSelectManager] Loading scriptable Level: {data.levelName} (Lesson {levelIndex})");
            
            // Assign active level details so GameFlowManager can access it
            KidGame.Interface.GameFlowManager.ActiveLevel = data;

            // All dynamic scriptable levels run inside the unified "Game" scene
            string sceneToLoad = "Game";

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadLevelWithTransition(
                    sceneToLoad,
                    "LESSON " + levelIndex,
                    data.levelName,
                    data.levelSubtitle
                );
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
            }
        }
    }
}
