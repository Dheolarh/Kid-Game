using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        [SerializeField] private Image pauseButtonOutline;
        [SerializeField] private Image pauseMenuOutline;
        [SerializeField] private Image gameEndPanelBg;
        [SerializeField] private Image gameBackgroundImg;

        [Header("Dialogue UI References")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button dialogueCloseButton;
        [SerializeField] private Animator dialogueMascotAnimator;
        [SerializeField] private SpriteRenderer dialogueMascotSpriteRenderer;

        [Header("Mascot Animation")]
        [SerializeField] private Animator mascotAnimator;
        [SerializeField] private string mascotCorrectTrigger = "";
        [SerializeField] private string mascotWrongTrigger = "IsNoIdea";
        [SerializeField] private string mascotWowedTrigger = "IsHappy";
        [SerializeField] private string mascotVictoryTrigger = "IsWinner";

        [Header("Game End Panel")]
        [SerializeField] private GameObject gameEndPanel;
        [SerializeField] private TMP_Text endTipsText;
        [SerializeField] private Image[] endStars = new Image[3];
        [SerializeField] private Color activeStarColor = Color.yellow;
        [SerializeField] private Color inactiveStarColor = new Color(1f, 1f, 1f, 0.2f);

        // State variables
        private int _currentPageIndex = 0;
        private int _totalMistakes = 0;
        private int _totalCorrectRequired = 0;
        private int _consecutiveRightAnswers = 0;
        private bool _isLevelCompleted = false;
        private int _activeDialogueLineIndex = 0;
        private bool _isCompletionDialogueActive = false;
        private float _lastDialogueActivityTime;
        private bool _hasPlayedInactivityAnimation = false;
        [SerializeField] private float dialogueInactivityTimeout = 8f;

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
            if (dialoguePanel != null && dialoguePanel.activeSelf && !_hasPlayedInactivityAnimation)
            {
                if (Time.time - _lastDialogueActivityTime > dialogueInactivityTimeout)
                {
                    _hasPlayedInactivityAnimation = true;
                    if (dialogueMascotAnimator != null)
                    {
                        dialogueMascotAnimator.SetTrigger("IsHi");
                    }
                    if (dialogueMascotSpriteRenderer != null)
                    {
                        dialogueMascotSpriteRenderer.flipX = true;
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
            return themeColor;
        }

        private void ApplyVisualTheme()
        {
            if (ActiveLevel == null) return;

            Color themeColor = GetActiveThemeColor();
            Sprite bgSprite = ActiveLevel.levelBackgroundSprite;

            // Check for theme preset override for background
            if (themeDatabase != null && !string.IsNullOrEmpty(ActiveLevel.themePresetName))
            {
                var preset = themeDatabase.GetPreset(ActiveLevel.themePresetName);
                if (preset != null)
                {
                    bgSprite = preset.backgroundSprite;
                }
            }

            // Apply theme outline colors
            if (pauseButtonOutline != null) pauseButtonOutline.color = themeColor;
            if (pauseMenuOutline != null) pauseMenuOutline.color = themeColor;
            if (gameEndPanelBg != null) gameEndPanelBg.color = themeColor;

            // Apply background sprite
            if (gameBackgroundImg != null && bgSprite != null)
            {
                gameBackgroundImg.sprite = bgSprite;
            }

            // Find all Outline components in the active scene recursively and color them
            var rootObjs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in rootObjs)
            {
                if (root == null) continue;
                var outlines = root.GetComponentsInChildren<UnityEngine.UI.Outline>(true);
                foreach (var outline in outlines)
                {
                    if (outline != null)
                    {
                        outline.effectColor = themeColor;
                    }
                }
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

            // Deactivate all managers initially
            DeactivateAllGameModes();

            // Configure the specific manager
            ConfigureAndSetupGameMode(page);

            // Dialogue popup check
            _activeDialogueLineIndex = 0;
            if (page.dialogueLines != null && page.dialogueLines.Count > 0)
            {
                if (dialoguePanel != null && dialogueText != null)
                {
                    DisplayActiveDialogueLine(page.dialogueLines[0]);
                    dialoguePanel.SetActive(true);
                }
                else
                {
                    // Fallback if UI not assigned
                    StartActiveGameMode(page.gameType);
                }
            }
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            StartActiveGameMode(page.gameType);


            // Trigger walking transition on the main gameplay mascot when moving to a new page
            if (mascotAnimator != null)
            {
                mascotAnimator.SetTrigger("IsWalking");
            }
        }

        private void OnDialogueCloseButtonClicked()
        {
            if (ActiveLevel == null || _currentPageIndex >= ActiveLevel.pages.Count) return;
            PageData page = ActiveLevel.pages[_currentPageIndex];

            var activeLines = _isCompletionDialogueActive ? page.completionDialogueLines : page.dialogueLines;

            if (activeLines != null && _activeDialogueLineIndex < activeLines.Count - 1)
            {
                _activeDialogueLineIndex++;
                DisplayActiveDialogueLine(activeLines[_activeDialogueLineIndex]);
            }
            else
            {
                if (_isCompletionDialogueActive)
                {
                    if (dialoguePanel != null) dialoguePanel.SetActive(false);
                    ProceedAfterPageCompletion();
                }
                else
                {
                    OnDialogueClosed();
                }
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
                    break;
            }
        }

        private void SetupNextButtons(Button portraitBtn, Button landscapeBtn)
        {
            Color themeColor = GetActiveThemeColor();

            if (portraitBtn != null)
            {
                portraitBtn.onClick.RemoveAllListeners();
                portraitBtn.onClick.AddListener(OnNextClicked);
                StyleNextButton(portraitBtn, themeColor);
            }
            if (landscapeBtn != null)
            {
                landscapeBtn.onClick.RemoveAllListeners();
                landscapeBtn.onClick.AddListener(OnNextClicked);
                StyleNextButton(landscapeBtn, themeColor);
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
            btn.colors = colors;
        }

        public void RegisterCorrectAnswer()
        {
            _consecutiveRightAnswers++;
            if (_consecutiveRightAnswers >= 3)
            {
                _consecutiveRightAnswers = 0;
                TriggerMascotWowed();
            }
            else
            {
                TriggerMascotCorrect();
            }
        }

        public void RegisterMistake()
        {
            _totalMistakes++;
            _consecutiveRightAnswers = 0;
            TriggerMascotWrong();
        }

        private void TriggerMascotCorrect()
        {
            if (mascotAnimator != null && !string.IsNullOrEmpty(mascotCorrectTrigger))
            {
                mascotAnimator.SetTrigger(mascotCorrectTrigger);
            }
        }

        private void TriggerMascotWrong()
        {
            if (mascotAnimator != null && !string.IsNullOrEmpty(mascotWrongTrigger))
            {
                mascotAnimator.SetTrigger(mascotWrongTrigger);
            }
        }

        private void TriggerMascotWowed()
        {
            if (mascotAnimator != null && !string.IsNullOrEmpty(mascotWowedTrigger))
            {
                mascotAnimator.SetTrigger(mascotWowedTrigger);
            }
        }

        private void OnNextClicked()
        {
            if (ActiveLevel == null || _currentPageIndex >= ActiveLevel.pages.Count) return;
            PageData page = ActiveLevel.pages[_currentPageIndex];

            // Completion dialogue check
            _activeDialogueLineIndex = 0;
            _isCompletionDialogueActive = true;
            if (page.completionDialogueLines != null && page.completionDialogueLines.Count > 0)
            {
                if (dialoguePanel != null && dialogueText != null)
                {
                    DisplayActiveDialogueLine(page.completionDialogueLines[0]);
                    dialoguePanel.SetActive(true);
                }
                else
                {
                    ProceedAfterPageCompletion();
                }
            }
            else
            {
                ProceedAfterPageCompletion();
            }
        }

        private void ProceedAfterPageCompletion()
        {
            _isCompletionDialogueActive = false;

            if (dialogueMascotAnimator != null)
            {
                dialogueMascotAnimator.SetTrigger("IsIdle");
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

            // Display End Screen with Stars
            if (gameEndPanel != null)
            {
                gameEndPanel.SetActive(true);

                if (endTipsText != null && ActiveLevel != null)
                {
                    string playerName = PlayerPrefs.GetString("PlayerName", "Kid");
                    endTipsText.text = ActiveLevel.levelEndTip.Replace("{PLAYERNAME}", playerName).Replace("{playername}", playerName);
                }

                for (int i = 0; i < endStars.Length; i++)
                {
                    if (endStars[i] != null)
                    {
                        endStars[i].color = (i < starsEarned) ? activeStarColor : inactiveStarColor;
                    }
                }
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
    }
}
