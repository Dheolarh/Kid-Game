using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using KidGame.Mechanics.Counting;
using KidGame.Mechanics.Addition;
using KidGame.Mechanics.Comparison;
using KidGame.Mechanics.Matching;
using KidGame.Mechanics.NumberRecall;
using KidGame.Mechanics.Tracing;

namespace KidGame.Interface
{
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        public static LevelData ActiveLevel { get; set; }

        [Header("Fallback Settings (For Editor Testing)")]
        [SerializeField] private LevelDatabase levelDatabase;
        [SerializeField] private ThemeDatabase themeDatabase;
        [SerializeField] private int testLevelIndex = 0;

        [Header("Game Mode GameObjects")]
        [SerializeField] private GameObject countingGameGo;
        [SerializeField] private GameObject additionGameGo;
        [SerializeField] private GameObject comparisonGameGo;
        [SerializeField] private GameObject matchingGameGo;
        [SerializeField] private GameObject recallGameGo;
        [SerializeField] private GameObject tracingGameGo;

        [Header("UI Visual Styling References")]
        [SerializeField] private Image gameBackgroundImg;
        [SerializeField] private Image[] themeImages;
        [SerializeField] private GameObject[] themeOutlines;

        [Header("Always Interactable Next Buttons (Fallback)")]
        [SerializeField] private Button[] alwaysInteractableButtons;

        private Button _activePortraitNextButton;
        private Button _activeLandscapeNextButton;

        [Header("Tracing Game Answer Grids")]
        [SerializeField] private GameObject portraitTracingAnswerGrid;
        [SerializeField] private GameObject landscapeTracingAnswerGrid;

        [Header("Dialogue UI References")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private GameObject dialogueMessageBox;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button dialogueCloseButton;
        [SerializeField] private Animator dialogueMascotAnimator;
        [SerializeField] private SpriteRenderer dialogueMascotSpriteRenderer;

        [Header("Mascot Animation")]
        [SerializeField] private Animator mascotAnimator;
        [SerializeField] private string mascotCorrectTrigger = "";
        [SerializeField] private string mascotWrongTrigger = "isNoIdea";
        [SerializeField] private string mascotWowedTrigger = "isHappy";
        [SerializeField] private string mascotVictoryTrigger = "isWinner";

        [Header("Game End Panel")]
        [SerializeField] private GameObject gameEndPanel;
        [SerializeField] private RectTransform endPanelBackground;     // Brown background panel
        [SerializeField] private TMP_Text lessonCompleteText;           // "LESSON COMPLETE!" text
        [SerializeField] private RectTransform endSphere;                // White sphere/dome shape
        [SerializeField] private GameObject endMascotObject;            // End mascot object
        [SerializeField] private Animator endMascotAnimator;            // Mascot animator (isWinner loop)
        [SerializeField] private TMP_Text greatJobText;                 // "Great Job!" text
        [SerializeField] private GameObject endConfettiObject;          // Confetti object
        [SerializeField] private Animator endConfettiAnimator;          // Confetti sprite animator
        [SerializeField] private TMP_Text endTipsText;
        [SerializeField] private Image[] endStars = new Image[3];
        [SerializeField] private Color activeStarColor = Color.yellow;
        [SerializeField] private Color inactiveStarColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private Button endHomeButton;                  // Home button
        [SerializeField] private Button endNextButton;                  // Next button
        [SerializeField] private Image endNextButtonBg;                 // Next button bg image (tinted next level color)
        [SerializeField] private GameObject endNoNextLevelPanel;        // Panel shown when no next level exists

        // State variables
        private int _currentPageIndex = 0;
        private int _totalMistakes = 0;
        private int _totalCorrectRequired = 0;
        private int _consecutiveRightAnswers = 0;
        private bool _isLevelCompleted = false;

        private Coroutine _dialogueCoroutine;
        private Coroutine _typewriterCoroutine;
        private bool _isTyping = false;
        private string _currentFullText = "";
        private bool _skipDialogueDelay = false;
        private bool _hasTriggeredCompletionDialogueForCurrentPage = false;
        private int _activeDialogueLineIndex = 0;
        private bool _isCompletionDialogueActive = false;
        private float _lastDialogueActivityTime;
        private bool _hasPlayedInactivityAnimation = false;
        [SerializeField] private float dialogueInactivityTimeout = 8f;

        private float _lastGameplayActivityTime;
        private bool _hasPlayedGameplayInactivity = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // If playing directly in the editor, load a fallback test level from database
            if (ActiveLevel == null && levelDatabase != null && levelDatabase.allLevels.Count > testLevelIndex)
            {
                ActiveLevel = levelDatabase.allLevels[testLevelIndex];
            }

            if (ActiveLevel == null)
            {
                Debug.LogWarning("[GameFlowManager] No active LevelData found. Please load from Level Select screen.");
                return;
            }

            InitializeLevel();
        }

        private void Update()
        {
            // Hide the main gameplay mascot when dialogue shows
            if (mascotAnimator != null)
            {
                bool shouldShowMascot = dialoguePanel != null && !dialoguePanel.activeSelf;
                if (mascotAnimator.gameObject.activeSelf != shouldShowMascot)
                {
                    mascotAnimator.gameObject.SetActive(shouldShowMascot);
                }
            }

            if (dialoguePanel != null && dialoguePanel.activeSelf && !_hasPlayedInactivityAnimation)
            {
                if (Time.time - _lastDialogueActivityTime > dialogueInactivityTimeout)
                {
                    _hasPlayedInactivityAnimation = true;
                    if (dialogueMascotAnimator != null)
                    {
                        dialogueMascotAnimator.SetTrigger("isHi");
                    }
                    if (dialogueMascotSpriteRenderer != null)
                    {
                        dialogueMascotSpriteRenderer.flipX = false;
                    }
                }
            }

            // Force next buttons to be always interactable so they don't fade to zero opacity
            if (_activePortraitNextButton != null && !_activePortraitNextButton.interactable)
            {
                _activePortraitNextButton.interactable = true;
            }
            if (_activeLandscapeNextButton != null && !_activeLandscapeNextButton.interactable)
            {
                _activeLandscapeNextButton.interactable = true;
            }
            if (alwaysInteractableButtons != null)
            {
                foreach (var btn in alwaysInteractableButtons)
                {
                    if (btn != null && !btn.interactable)
                    {
                        btn.interactable = true;
                    }
                }
            }

            // Automatically trigger completion dialogue the moment the round is finished
            if (!_hasTriggeredCompletionDialogueForCurrentPage && IsCurrentRoundCompleted())
            {
                if (ActiveLevel != null && _currentPageIndex < ActiveLevel.pages.Count)
                {
                    PageData page = ActiveLevel.pages[_currentPageIndex];
                    _hasTriggeredCompletionDialogueForCurrentPage = true;

                    _isCompletionDialogueActive = true;
                    if (page.completionDialogueLines != null && page.completionDialogueLines.Count > 0)
                    {
                        StartDialogueSequence(page.completionDialogueLines, () => {
                            ProceedAfterPageCompletion();
                        });
                    }
                    else
                    {
                        ProceedAfterPageCompletion();
                    }
                }
            }

            // Gameplay inactivity check (2 minutes = 120 seconds) triggers isHi with flipX
            if (dialoguePanel != null && !dialoguePanel.activeSelf && !_hasPlayedGameplayInactivity)
            {
                if (Time.time - _lastGameplayActivityTime > 120f)
                {
                    _hasPlayedGameplayInactivity = true;
                    if (mascotAnimator != null)
                    {
                        mascotAnimator.SetTrigger("isHi");
                        var sr = mascotAnimator.GetComponent<SpriteRenderer>();
                        if (sr != null) sr.flipX = false;
                    }
                }
            }
        }

        private void InitializeLevel()
        {
            _currentPageIndex = 0;
            _totalMistakes = 0;
            _totalCorrectRequired = 0;
            _consecutiveRightAnswers = 0;
            _isLevelCompleted = false;
            _lastGameplayActivityTime = Time.time;
            _hasPlayedGameplayInactivity = false;

            // Apply theme styling
            ApplyVisualTheme();

            if (dialogueCloseButton != null)
            {
                dialogueCloseButton.onClick.RemoveAllListeners();
                dialogueCloseButton.onClick.AddListener(OnDialogueCloseButtonClicked);
            }

            if (gameEndPanel != null)
            {
                gameEndPanel.SetActive(false);
            }

            // Calculate total correct actions required across all pages
            foreach (var page in ActiveLevel.pages)
            {
                if (page == null) continue;
                switch (page.gameType)
                {
                    case GameType.Counting:
                        _totalCorrectRequired += page.countingSlotCount;
                        break;
                    case GameType.Addition:
                        _totalCorrectRequired += page.additionSlotCount;
                        break;
                    case GameType.Comparison:
                        _totalCorrectRequired += page.comparisonSlotCount;
                        break;
                    case GameType.Matching:
                        _totalCorrectRequired += page.matchingSlotCount;
                        break;
                    case GameType.Recall:
                        _totalCorrectRequired += page.recallSlotCount;
                        break;
                    case GameType.Tracing:
                        // Tracing mode (excluding spell mode) has no mistakes/drops, so we treat it specially
                        if (page.tracingSpellModeActive)
                        {
                            // In spelling mode, each letter of the spelled word requires a drop
                            _totalCorrectRequired += GetSpellingLettersCount(page);
                        }
                        break;
                }
            }

            LoadPage(0);
        }

        private Color GetActiveThemeColor()
        {
            if (ActiveLevel == null) return Color.white;

            Color themeColor = ActiveLevel.levelThemeColor;
            if (themeDatabase != null && !string.IsNullOrEmpty(ActiveLevel.themePresetName))
            {
                var preset = themeDatabase.GetPreset(ActiveLevel.themePresetName);
                if (preset != null)
                {
                    themeColor = preset.themeColor;
                }
            }
            themeColor.a = 1f; // Force alpha to 1.0 so theme colors are never transparent
            return themeColor;
        }

        private void ApplyVisualTheme()
        {
            if (ActiveLevel == null)
            {
                Debug.LogWarning("[GameFlowManager] ApplyVisualTheme aborted: ActiveLevel is null!");
                return;
            }

            Color themeColor = GetActiveThemeColor();
            themeColor.a = 1f; // Force alpha to 1.0 (fully visible) so theme colors aren't invisible
            Sprite bgSprite = ActiveLevel.levelBackgroundSprite;

            Debug.Log($"[GameFlowManager] ApplyVisualTheme: Styling {ActiveLevel.levelName} with color {themeColor}. Images: {(themeImages != null ? themeImages.Length : 0)}, Outlines: {(themeOutlines != null ? themeOutlines.Length : 0)}");

            // Check for theme preset override for background
            if (themeDatabase != null && !string.IsNullOrEmpty(ActiveLevel.themePresetName))
            {
                var preset = themeDatabase.GetPreset(ActiveLevel.themePresetName);
                if (preset != null)
                {
                    bgSprite = preset.backgroundSprite;
                }
            }

            // Apply theme colors to configured images
            if (themeImages != null)
            {
                foreach (var img in themeImages)
                {
                    if (img != null) img.color = themeColor;
                }
            }

            // Apply theme colors to configured outlines
            if (themeOutlines != null)
            {
                foreach (var go in themeOutlines)
                {
                    if (go != null)
                    {
                        var outlines = go.GetComponents<UnityEngine.UI.Outline>();
                        foreach (var outline in outlines)
                        {
                            if (outline != null) outline.effectColor = themeColor;
                        }
                    }
                }
            }

            // Apply background sprite
            if (gameBackgroundImg != null && bgSprite != null)
            {
                gameBackgroundImg.sprite = bgSprite;
            }

            // Customize transition curtains color
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.SetCurtainColor(themeColor);
            }
        }

        private void LoadPage(int index)
        {
            if (ActiveLevel == null || index < 0 || index >= ActiveLevel.pages.Count) return;

            _currentPageIndex = index;
            PageData page = ActiveLevel.pages[index];

            _hasTriggeredCompletionDialogueForCurrentPage = false;
            _isCompletionDialogueActive = false;

            // Deactivate all managers initially
            DeactivateAllGameModes();

            // Configure the specific manager
            ConfigureAndSetupGameMode(page);

            // Start active game mode immediately so it is visible in the background
            StartActiveGameMode(page.gameType);

            // Dialogue popup check
            if (page.dialogueLines != null && page.dialogueLines.Count > 0)
            {
                StartDialogueSequence(page.dialogueLines, null);
            }
            else
            {
                if (dialoguePanel != null) dialoguePanel.SetActive(false);
            }

            _lastGameplayActivityTime = Time.time;
            _hasPlayedGameplayInactivity = false;

            if (mascotAnimator != null)
            {
                mascotAnimator.SetTrigger("isIdle");
                var sr = mascotAnimator.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = true;
            }
        }

        private void OnDialogueCloseButtonClicked()
        {
            if (_isTyping)
            {
                // Instantly complete typing
                if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
                if (dialogueText != null) dialogueText.text = _currentFullText;
                _isTyping = false;
            }
            else
            {
                // Skip the delay and advance
                _skipDialogueDelay = true;
            }
        }

        private void DisplayActiveDialogueLine(DialogueLine line)
        {
            if (line == null) return;

            _lastDialogueActivityTime = Time.time;
            _hasPlayedInactivityAnimation = false;

            if (dialogueText != null)
            {
                string playerName = PlayerPrefs.GetString("PlayerName", "Kid");
                string processedLine = line.text.Replace("{PLAYERNAME}", playerName).Replace("{playername}", playerName);
                dialogueText.text = processedLine;
            }

            if (dialogueMascotAnimator != null && !string.IsNullOrEmpty(line.mascotAnimationTrigger))
            {
                dialogueMascotAnimator.SetTrigger(line.mascotAnimationTrigger);
            }

            if (dialogueMascotSpriteRenderer != null)
            {
                // Flip X if the trigger is "IsTalking"
                dialogueMascotSpriteRenderer.flipX = (!string.IsNullOrEmpty(line.mascotAnimationTrigger) && line.mascotAnimationTrigger == "IsTalking");
            }
        }

        private void OnDialogueClosed()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);

            if (dialogueMascotAnimator != null)
            {
                dialogueMascotAnimator.SetTrigger("IsIdle");
            }
            if (dialogueMascotSpriteRenderer != null)
            {
                dialogueMascotSpriteRenderer.flipX = false;
            }

            if (ActiveLevel != null && _currentPageIndex < ActiveLevel.pages.Count)
            {
                StartActiveGameMode(ActiveLevel.pages[_currentPageIndex].gameType);
            }
        }

        private void DeactivateAllGameModes()
        {
            if (countingGameGo) countingGameGo.SetActive(false);
            if (additionGameGo) additionGameGo.SetActive(false);
            if (comparisonGameGo) comparisonGameGo.SetActive(false);
            if (matchingGameGo) matchingGameGo.SetActive(false);
            if (recallGameGo) recallGameGo.SetActive(false);
            if (tracingGameGo) tracingGameGo.SetActive(false);
        }

        private void StartActiveGameMode(GameType type)
        {
            GameObject targetGo = GetGameObjectForType(type);
            if (targetGo != null)
            {
                targetGo.SetActive(true);
            }
        }

        private GameObject GetGameObjectForType(GameType type)
        {
            switch (type)
            {
                case GameType.Counting:   return countingGameGo;
                case GameType.Addition:   return additionGameGo;
                case GameType.Comparison: return comparisonGameGo;
                case GameType.Matching:   return matchingGameGo;
                case GameType.Recall:     return recallGameGo;
                case GameType.Tracing:    return tracingGameGo;
                default:                  return null;
            }
        }

        private void ConfigureAndSetupGameMode(PageData page)
        {
            switch (page.gameType)
            {
                case GameType.Counting:
                    var counting = countingGameGo?.GetComponent<CountingGameManager>();
                    if (counting != null)
                    {
                        counting.Configure(page.countingSlotCount, page.countingMinCount, page.countingMaxCount, page.countingDiceMode, page.countingFingerMode, page.countingActiveThemeName);
                        SetupNextButtons(counting.PortraitNextButton, counting.LandscapeNextButton);
                    }
                    break;

                case GameType.Addition:
                    var addition = additionGameGo?.GetComponent<AdditionGameManager>();
                    if (addition != null)
                    {
                        addition.Configure(page.additionSlotCount, page.additionMinPerGrid, page.additionMaxPerGrid, page.additionDiceMode, page.additionFingerMode, page.additionCountAddMode, page.additionNumbersOnlyMode, page.additionMinOperandCount, page.additionMaxOperandCount, page.additionMinNumberValue, page.additionMaxNumberValue, page.additionActiveThemeName);
                        SetupNextButtons(addition.PortraitNextButton, addition.LandscapeNextButton);
                    }
                    break;

                case GameType.Comparison:
                    var comparison = comparisonGameGo?.GetComponent<ComparisonGameManager>();
                    if (comparison != null)
                    {
                        comparison.Configure(page.comparisonSlotCount, page.comparisonMinVal, page.comparisonMaxVal, page.comparisonMixAdditionEquations, page.comparisonNumbersOnlyMode, page.comparisonActiveThemeName);
                        SetupNextButtons(comparison.PortraitNextButton, comparison.LandscapeNextButton);
                    }
                    break;

                case GameType.Matching:
                    var matching = matchingGameGo?.GetComponent<MatchGameManager>();
                    if (matching != null)
                    {
                        matching.Configure(page.matchingLeftVariant, page.matchingRightVariant, page.matchingSlotCount, page.matchingMinVal, page.matchingMaxVal, page.matchingShuffleLeftColumn);
                        SetupNextButtons(matching.PortraitNextButton, matching.LandscapeNextButton);
                    }
                    break;

                case GameType.Recall:
                    var recall = recallGameGo?.GetComponent<NumberRecallGameManager>();
                    if (recall != null)
                    {
                        recall.Configure(page.recallSlotCount, page.recallMinSequenceLength, page.recallMaxSequenceLength, page.recallMinStartValue, page.recallMaxStartValue, page.recallStep, page.recallCountBackwards, page.recallMinConsecutiveRevealed, page.recallMaxConsecutiveRevealed, page.recallMinConsecutiveHidden, page.recallMaxConsecutiveHidden);
                        SetupNextButtons(recall.PortraitNextButton, recall.LandscapeNextButton);
                    }
                    break;

                case GameType.Tracing:
                    var tracing = tracingGameGo?.GetComponent<TracingModeManager>();
                    if (tracing != null)
                    {
                        tracing.Configure(page.tracingSpellModeActive, page.tracingValuesToTrace, page.tracingCustomSpawnCount);
                        SetupNextButtons(tracing.PortraitContinueButton, tracing.LandscapeContinueButton);
                    }

                    // Set answer grid opacity based on spell mode
                    float alphaVal = page.tracingSpellModeActive ? 1f : 0f;
                    SetAnswerGridOpacity(portraitTracingAnswerGrid, alphaVal);
                    SetAnswerGridOpacity(landscapeTracingAnswerGrid, alphaVal);
                    break;
            }
        }

        private void SetupNextButtons(Button portraitBtn, Button landscapeBtn)
        {
            Color themeColor = GetActiveThemeColor();

            _activePortraitNextButton = portraitBtn;
            _activeLandscapeNextButton = landscapeBtn;

            if (portraitBtn != null)
            {
                portraitBtn.onClick.RemoveAllListeners();
                portraitBtn.onClick.AddListener(OnNextClicked);
                StyleNextButton(portraitBtn, themeColor);
                portraitBtn.interactable = true;
            }
            if (landscapeBtn != null)
            {
                landscapeBtn.onClick.RemoveAllListeners();
                landscapeBtn.onClick.AddListener(OnNextClicked);
                StyleNextButton(landscapeBtn, themeColor);
                landscapeBtn.interactable = true;
            }
        }

        private void StyleNextButton(Button btn, Color themeColor)
        {
            if (btn == null) return;

            // If the button has an Image, tint it with the theme color
            if (btn.image != null)
            {
                btn.image.color = themeColor;
            }

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.8f); // Increased opacity (80%) so uninteractable buttons aren't transparent
            btn.colors = colors;
        }

        public void RegisterCorrectAnswer()
        {
            _consecutiveRightAnswers++;
            _lastGameplayActivityTime = Time.time;
            _hasPlayedGameplayInactivity = false;
            if (mascotAnimator != null)
            {
                mascotAnimator.SetTrigger("isIdle");
                var sr = mascotAnimator.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = true;
            }
        }

        public void RegisterMistake()
        {
            _totalMistakes++;
            _consecutiveRightAnswers = 0;
            _lastGameplayActivityTime = Time.time;
            _hasPlayedGameplayInactivity = false;
            if (mascotAnimator != null)
            {
                var sr = mascotAnimator.GetComponent<SpriteRenderer>();
                if (sr != null) sr.flipX = true;
            }
            TriggerMascotWrong();
        }

        private void TriggerMascotWrong()
        {
            if (mascotAnimator != null)
            {
                mascotAnimator.SetTrigger("isNoIdea");
            }
        }

        private void OnNextClicked()
        {
            if (ActiveLevel == null || _currentPageIndex >= ActiveLevel.pages.Count) return;

            // If completion dialogue is already active, make Next button act as skip/dismiss
            if (_isCompletionDialogueActive)
            {
                OnDialogueCloseButtonClicked();
                return;
            }

            // Check if the current round's task is completed
            if (!IsCurrentRoundCompleted())
            {
                TriggerMascotWrong();

                // Show the dialogue panel with the warning message (lasts 3 seconds or closes on click)
                if (dialoguePanel != null && dialogueText != null)
                {
                    List<DialogueLine> warningLine = new List<DialogueLine> {
                        new DialogueLine { text = "Let's complete this task", mascotAnimationTrigger = "IsNoIdea" }
                    };
                    StartDialogueSequence(warningLine, null);
                }
                return;
            }

            // Fallback: if completed but completion dialogue hasn't triggered yet
            if (!_hasTriggeredCompletionDialogueForCurrentPage)
            {
                PageData page = ActiveLevel.pages[_currentPageIndex];
                _hasTriggeredCompletionDialogueForCurrentPage = true;
                _isCompletionDialogueActive = true;
                if (page.completionDialogueLines != null && page.completionDialogueLines.Count > 0)
                {
                    StartDialogueSequence(page.completionDialogueLines, () => {
                        ProceedAfterPageCompletion();
                    });
                }
                else
                {
                    ProceedAfterPageCompletion();
                }
            }
        }

        private void ProceedAfterPageCompletion()
        {
            _isCompletionDialogueActive = false;

            if (dialogueMascotAnimator != null)
            {
                dialogueMascotAnimator.SetTrigger("isIdle");
            }
            if (dialogueMascotSpriteRenderer != null)
            {
                dialogueMascotSpriteRenderer.flipX = false;
            }

            if (ActiveLevel != null && _currentPageIndex < ActiveLevel.pages.Count - 1)
            {
                LoadPage(_currentPageIndex + 1);
            }
            else
            {
                CompleteLevel();
            }
        }

        private void CompleteLevel()
        {
            if (_isLevelCompleted) return;
            _isLevelCompleted = true;

            DeactivateAllGameModes();

            // Calculate final performance percentage
            // Tracing levels without spelling have totalCorrectRequired = 0, so they get 3 stars (100%) by default.
            float percent = 100f;
            if (_totalCorrectRequired > 0)
            {
                percent = ((float)_totalCorrectRequired / (_totalCorrectRequired + _totalMistakes)) * 100f;
            }

            int starsEarned = 1;
            if (percent >= 80f) starsEarned = 3;
            else if (percent >= 50f) starsEarned = 2;

            Debug.Log($"[GameFlowManager] Level Completed! Score: {percent:F1}%, Mistakes: {_totalMistakes}, Stars: {starsEarned}");

            // Trigger winner victory animation on level complete
            if (mascotAnimator != null && !string.IsNullOrEmpty(mascotVictoryTrigger))
            {
                mascotAnimator.SetTrigger(mascotVictoryTrigger);
            }

            // Save performance to PlayerPrefs
            if (ActiveLevel != null)
            {
                int currentHighStars = PlayerPrefs.GetInt($"Level_Stars_{ActiveLevel.levelNumber}", 0);
                if (starsEarned > currentHighStars)
                {
                    PlayerPrefs.SetInt($"Level_Stars_{ActiveLevel.levelNumber}", starsEarned);
                }

                // Unlock the next level
                int nextLevelNumber = ActiveLevel.levelNumber + 1;
                PlayerPrefs.SetInt($"Level_Unlocked_{nextLevelNumber}", 1);
                PlayerPrefs.Save();
            }

            // Play animated end panel sequence
            if (gameEndPanel != null)
            {
                gameEndPanel.SetActive(true);
                StartCoroutine(PlayEndPanelSequence(starsEarned));
            }
        }

        private IEnumerator PlayEndPanelSequence(int starsEarned)
        {
            // ── Prep: hide all elements before animating ──────────────────────────────
            if (endPanelBackground != null) endPanelBackground.localScale = Vector3.zero;
            if (lessonCompleteText != null) lessonCompleteText.transform.localScale = Vector3.zero;

            // Store sphere's home position, then push it 300px below so it can slide up
            Vector2 sphereHomePos = Vector2.zero;
            if (endSphere != null)
            {
                sphereHomePos = endSphere.anchoredPosition;
                endSphere.anchoredPosition = new Vector2(sphereHomePos.x, sphereHomePos.y - 300f);
            }

            if (greatJobText != null) greatJobText.transform.localScale = Vector3.zero;

            if (endStars != null)
            {
                foreach (var star in endStars)
                {
                    if (star != null)
                    {
                        star.color = inactiveStarColor;
                        star.transform.localScale = Vector3.zero;
                    }
                }
            }

            if (endConfettiAnimator != null) endConfettiAnimator.gameObject.SetActive(false);
            if (endTipsText != null) endTipsText.text = "";
            if (endHomeButton != null) endHomeButton.transform.localScale = Vector3.zero;
            if (endNextButton != null) endNextButton.transform.localScale = Vector3.zero;

            yield return null; // let layout settle

            // ── Step 1: Background pops in ────────────────────────────────────────────
            if (endPanelBackground != null)
                endPanelBackground.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(0.25f);

            // ── Step 2: "LESSON COMPLETE!" pops in ────────────────────────────────────
            if (lessonCompleteText != null)
                lessonCompleteText.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(0.3f);

            // ── Step 3: Sphere slides up to its home position ─────────────────────────
            if (endSphere != null)
                endSphere.DOAnchorPosY(sphereHomePos.y, 0.5f).SetEase(Ease.OutCubic);
            yield return new WaitForSeconds(0.35f);

            // ── Step 4: Mascot isWinner trigger + "Great Job!" pop in ─────────────────
            if (endMascotAnimator != null)
                endMascotObject.SetActive(true);
                endMascotAnimator.SetTrigger("isWinner");
            if (greatJobText != null)
                greatJobText.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(0.5f);

            // ── Step 5: Stars animate in one by one ───────────────────────────────────
            for (int i = 0; i < endStars.Length; i++)
            {
                if (endStars[i] == null) continue;

                Image starImg = endStars[i]; // local capture to avoid closure bug
                bool isEarned = (i < starsEarned);

                if (isEarned)
                {
                    // Smash-in: scale punch from 0→1.3→1 and color flash to gold
                    starImg.color = Color.white;
                    starImg.transform.localScale = Vector3.zero;
                    starImg.transform.DOScale(1.3f, 0.18f).SetEase(Ease.OutCubic)
                        .OnComplete(() => starImg.transform.DOScale(1f, 0.15f).SetEase(Ease.InCubic));
                    starImg.DOColor(activeStarColor, 0.2f);
                    yield return new WaitForSeconds(0.28f);
                }
                else
                {
                    // Unearned: quietly fade in at dim white, no fanfare
                    starImg.color = inactiveStarColor;
                    starImg.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
                    yield return new WaitForSeconds(0.15f);
                }
            }
            yield return new WaitForSeconds(0.2f);

            // ── Step 6: Confetti blast ────────────────────────────────────────────────
            if (endConfettiAnimator != null)
                endConfettiObject.SetActive(true);
                endConfettiAnimator.gameObject.SetActive(true); // entry animation plays automatically
            yield return new WaitForSeconds(0.3f);

            // ── Step 7: Tip text fast typewriter ──────────────────────────────────────
            if (endTipsText != null && ActiveLevel != null)
            {
                string playerName = PlayerPrefs.GetString("SingleWordName", "Kid");
                string tipText = ActiveLevel.levelEndTip
                    .Replace("{PLAYERNAME}", playerName)
                    .Replace("{playername}", playerName);
                yield return StartCoroutine(TypewriteEndTip(endTipsText, tipText, 0.02f));
            }
            yield return new WaitForSeconds(0.2f);

            // ── Step 8: Home & Next buttons pop in together ───────────────────────────
            // Tint Next button with next level's theme color
            LevelData nextLevel = GetNextLevel();
            if (endNextButtonBg != null && nextLevel != null)
            {
                Color nextColor = nextLevel.levelThemeColor;
                if (themeDatabase != null && !string.IsNullOrEmpty(nextLevel.themePresetName))
                {
                    var preset = themeDatabase.GetPreset(nextLevel.themePresetName);
                    if (preset != null) nextColor = preset.themeColor;
                }
                nextColor.a = 1f;
                endNextButtonBg.color = nextColor;
            }

            if (endHomeButton != null)
            {
                endHomeButton.onClick.RemoveAllListeners();
                endHomeButton.onClick.AddListener(OnEndHomeClicked);
                endHomeButton.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
            if (endNextButton != null)
            {
                endNextButton.onClick.RemoveAllListeners();
                endNextButton.onClick.AddListener(OnEndNextClicked);
                endNextButton.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetDelay(0.05f);
            }
        }

        private IEnumerator TypewriteEndTip(TMP_Text textComponent, string text, float charDelay)
        {
            textComponent.text = "";
            for (int i = 0; i <= text.Length; i++)
            {
                textComponent.text = text.Substring(0, i);
                yield return new WaitForSeconds(charDelay);
            }
        }

        private LevelData GetNextLevel()
        {
            if (ActiveLevel == null || levelDatabase == null) return null;
            int nextNumber = ActiveLevel.levelNumber + 1;
            return levelDatabase.allLevels.Find(l => l != null && l.levelNumber == nextNumber);
        }

        private void OnEndHomeClicked()
        {
            if (SceneTransitionManager.Instance != null)
            {
                // "Level" is the Level Select scene
                SceneTransitionManager.Instance.LoadSceneWithTransition("Level");
            }
        }

        private void OnEndNextClicked()
        {
            LevelData nextLevel = GetNextLevel();
            if (nextLevel == null)
            {
                // No next level — show the placeholder panel (to be configured later)
                if (endNoNextLevelPanel != null)
                    endNoNextLevelPanel.SetActive(true);
                return;
            }

            // Determine next level's theme color
            Color nextColor = nextLevel.levelThemeColor;
            if (themeDatabase != null && !string.IsNullOrEmpty(nextLevel.themePresetName))
            {
                var preset = themeDatabase.GetPreset(nextLevel.themePresetName);
                if (preset != null) nextColor = preset.themeColor;
            }
            nextColor.a = 1f;

            // Set next level as active and transition with its curtain color
            GameFlowManager.ActiveLevel = nextLevel;

            if (SceneTransitionManager.Instance != null)
            {
                string sceneToLoad = string.IsNullOrEmpty(nextLevel.sceneToLoad) ? "Game" : nextLevel.sceneToLoad;
                SceneTransitionManager.Instance.SetCurtainColor(nextColor);
                SceneTransitionManager.Instance.LoadLevelWithTransition(
                    sceneToLoad,
                    $"Level {nextLevel.levelNumber}",
                    nextLevel.levelName,
                    nextLevel.levelSubtitle,
                    nextColor
                );
            }
        }

        private int GetSpellingLettersCount(PageData page)
        {
            if (page.tracingValuesToTrace.Count == 0) return 0;
            
            // Build the spelled words sequence
            List<string> spellingParts = new List<string>();
            for (int i = 0; i < Mathf.Min(page.tracingValuesToTrace.Count, page.tracingCustomSpawnCount); i++)
            {
                string entry = page.tracingValuesToTrace[i].Trim();
                if (IsNumericExpression(entry))
                {
                    spellingParts.Add(NumberToWords(int.Parse(entry)).ToUpper());
                }
            }
            string spellingTarget = string.Join(" ", spellingParts);
            
            int letterCount = 0;
            foreach (char c in spellingTarget)
            {
                if (c != ' ' && c != ',')
                {
                    letterCount++;
                }
            }
            return letterCount;
        }

        private bool IsNumericExpression(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return false;
            foreach (char c in str)
            {
                if (c != ' ' && c != ',' && (c < '0' || c > '9'))
                    return false;
            }
            return true;
        }

        private string NumberToWords(int number)
        {
            if (number == 0) return "zero";
            if (number < 0) return "minus " + NumberToWords(Mathf.Abs(number));

            string words = "";

            if ((number / 1000000) > 0)
            {
                words += NumberToWords(number / 1000000) + " million ";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += NumberToWords(number / 1000) + " thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += NumberToWords(number / 100) + " hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "") words += "and ";

                var unitsMap = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
                var tensMap = new[] { "zero", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

                if (number < 20)
                    words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += " " + unitsMap[number % 10];
                }
            }

            return words.Trim();
        }

        private bool IsCurrentRoundCompleted()
        {
            if (ActiveLevel == null || _currentPageIndex >= ActiveLevel.pages.Count) return true;
            PageData page = ActiveLevel.pages[_currentPageIndex];

            switch (page.gameType)
            {
                case GameType.Counting:
                    var counting = countingGameGo?.GetComponent<CountingGameManager>();
                    if (counting != null)
                    {
                        int answered = GetPrivateField<int>(counting, "_answeredCount");
                        int total = GetPrivateListCount(counting, "_slots");
                        if (total == 0) return false; // Still initializing slots!
                        return answered >= total;
                    }
                    break;

                case GameType.Addition:
                    var addition = additionGameGo?.GetComponent<AdditionGameManager>();
                    if (addition != null)
                    {
                        int answered = GetPrivateField<int>(addition, "_answeredCount");
                        int total = GetPrivateListCount(addition, "_slots");
                        if (total == 0) return false;
                        return answered >= total;
                    }
                    break;

                case GameType.Comparison:
                    var comparison = comparisonGameGo?.GetComponent<ComparisonGameManager>();
                    if (comparison != null)
                    {
                        int answered = GetPrivateField<int>(comparison, "_answeredCount");
                        int total = GetPrivateListCount(comparison, "_slots");
                        if (total == 0) return false;
                        return answered >= total;
                    }
                    break;

                case GameType.Matching:
                    var matching = matchingGameGo?.GetComponent<MatchGameManager>();
                    if (matching != null)
                    {
                        int connections = GetPrivateListCount(matching, "_connections");
                        int total = GetPrivateListCount(matching, "_slots");
                        if (total == 0) return false;
                        return connections >= total;
                    }
                    break;

                case GameType.Recall:
                    var recall = recallGameGo?.GetComponent<NumberRecallGameManager>();
                    if (recall != null)
                    {
                        int answered = GetPrivateField<int>(recall, "_answeredCount");
                        int total = GetPrivateListCount(recall, "_slots");
                        if (total == 0) return false;
                        return answered >= total;
                    }
                    break;

                case GameType.Tracing:
                    var tracing = tracingGameGo?.GetComponent<TracingModeManager>();
                    if (tracing != null)
                    {
                        bool isSpellMode = GetPrivateField<bool>(tracing, "spellModeActive");
                        if (isSpellMode)
                        {
                            var zones = InvokePrivateMethod<System.Collections.IEnumerable>(tracing, "GetActiveSpellingZones") as System.Collections.ICollection;
                            if (zones == null || zones.Count == 0) return false; // Still initializing spelling zones!
                            bool allCorrect = true;
                            foreach (var zone in zones)
                            {
                                bool isAnswered = GetProperty<bool>(zone, "IsAnswered");
                                if (!isAnswered) { allCorrect = false; break; }
                            }
                            return allCorrect;
                        }
                        else
                        {
                            return InvokePrivateMethod<bool>(tracing, "IsAllTracingComplete");
                        }
                    }
                    break;
            }
            return true;
        }

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            if (obj == null) return default;
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(obj);
            }
            return default;
        }

        private int GetPrivateListCount(object obj, string fieldName)
        {
            if (obj == null) return 0;
            var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var list = field.GetValue(obj) as System.Collections.ICollection;
                if (list != null) return list.Count;
            }
            return 0;
        }

        private T InvokePrivateMethod<T>(object obj, string methodName)
        {
            if (obj == null) return default;
            var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                return (T)method.Invoke(obj, null);
            }
            return default;
        }

        private T GetProperty<T>(object obj, string propertyName)
        {
            if (obj == null) return default;
            var prop = obj.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prop != null)
            {
                return (T)prop.GetValue(obj);
            }
            return default;
        }

        private void SetAnswerGridOpacity(GameObject gridGo, float alpha)
        {
            if (gridGo == null) return;
            var cg = gridGo.GetComponent<CanvasGroup>();
            if (cg == null) cg = gridGo.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
            cg.blocksRaycasts = (alpha > 0.1f);
        }

        private void StartDialogueSequence(List<DialogueLine> lines, System.Action onSequenceComplete)
        {
            if (lines == null || lines.Count == 0)
            {
                onSequenceComplete?.Invoke();
                return;
            }

            if (_dialogueCoroutine != null) StopCoroutine(_dialogueCoroutine);
            _dialogueCoroutine = StartCoroutine(DialogueSequenceRoutine(lines, onSequenceComplete));
        }

        private IEnumerator DialogueSequenceRoutine(List<DialogueLine> lines, System.Action onSequenceComplete)
        {
            // 0. Wait until the scene transition curtains are fully open/inactive before showing the dialogue
            if (SceneTransitionManager.Instance != null)
            {
                while (SceneTransitionManager.Instance.IsTransitioning)
                {
                    yield return null;
                }
            }

            if (dialoguePanel != null)
            {
                dialoguePanel.transform.DOKill();
                dialoguePanel.transform.localScale = Vector3.zero;
                dialoguePanel.SetActive(true);
                dialoguePanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                _activeDialogueLineIndex = i;
                _skipDialogueDelay = false;

                // 1. Set up mascot animation and flip
                if (dialogueMascotAnimator != null)
                {
                    if (!string.IsNullOrEmpty(line.mascotAnimationTrigger))
                    {
                        string triggerName = line.mascotAnimationTrigger;
                        if (triggerName.Equals("Hi", System.StringComparison.OrdinalIgnoreCase))
                        {
                            triggerName = "isHi";
                        }
                        else if (triggerName.Equals("Idle", System.StringComparison.OrdinalIgnoreCase))
                        {
                            triggerName = "isIdle";
                        }
                        dialogueMascotAnimator.SetTrigger(triggerName);
                    }
                    else
                    {
                        dialogueMascotAnimator.SetTrigger("isIdle");
                    }
                }

                if (dialogueMascotSpriteRenderer != null)
                {
                    dialogueMascotSpriteRenderer.flipX = false;
                }

                // Pop up only the message bubble/box on every new line
                if (dialogueMessageBox != null)
                {
                    dialogueMessageBox.transform.DOKill();
                    dialogueMessageBox.transform.localScale = Vector3.zero;
                    dialogueMessageBox.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
                }

                // 2. Format and typewrite text
                string playerName = PlayerPrefs.GetString("PlayerName", "Kid");
                _currentFullText = line.text.Replace("{PLAYERNAME}", playerName).Replace("{playername}", playerName);

                _isTyping = true;
                if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = StartCoroutine(TypewriteText(_currentFullText));

                // Wait until typing is finished
                yield return new WaitUntil(() => !_isTyping);

                // 3. Wait for the required duration (10 seconds for all lines)
                float delay = 10.0f;
                float elapsed = 0f;
                while (elapsed < delay && !_skipDialogueDelay)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            // Close dialogue with scale down tween
            if (dialoguePanel != null)
            {
                bool isClosed = false;
                dialoguePanel.transform.DOKill();
                dialoguePanel.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).OnComplete(() => {
                    dialoguePanel.SetActive(false);
                    isClosed = true;
                });
                yield return new WaitUntil(() => isClosed);
            }

            // Trigger isIdle on closed
            if (dialogueMascotAnimator != null)
            {
                dialogueMascotAnimator.SetTrigger("isIdle");
            }
            if (dialogueMascotSpriteRenderer != null)
            {
                dialogueMascotSpriteRenderer.flipX = false;
            }

            onSequenceComplete?.Invoke();
        }

        private IEnumerator TypewriteText(string text)
        {
            if (dialogueText != null)
            {
                dialogueText.text = "";
                for (int i = 0; i <= text.Length; i++)
                {
                    dialogueText.text = text.Substring(0, i);
                    yield return new WaitForSeconds(0.03f); // Slower, more readable typing speed
                }
            }
            _isTyping = false;
        }
    }
}
