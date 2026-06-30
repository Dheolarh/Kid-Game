using System.Collections.Generic;
using UnityEngine;

namespace KidGame.Interface
{
    public enum GameType
    {
        Counting,
        Addition,
        Comparison,
        Matching,
        Recall,
        Tracing
    }

    [System.Serializable]
    public class DialogueLine
    {
        [TextArea(2, 4)]
        public string text;
        [Tooltip("Optional animator trigger parameter for dialogue mascot.")]
        public string mascotAnimationTrigger;
    }

    [System.Serializable]
    public class PageData
    {
        [Tooltip("The game mode/type to play on this page.")]
        public GameType gameType = GameType.Counting;

        [Tooltip("Optional list of dialogue popup speech lines at the start of this page.")]
        public List<DialogueLine> dialogueLines = new List<DialogueLine>();

        [Tooltip("Optional list of dialogue popup speech lines shown on page task completion.")]
        public List<DialogueLine> completionDialogueLines = new List<DialogueLine>();

        [Header("Counting Game Settings")]
        public int countingSlotCount = 5;
        public int countingMinCount = 1;
        public int countingMaxCount = 12;
        public bool countingDiceMode = false;
        public bool countingFingerMode = false;
        [Tooltip("The active theme name to restrict object spawning. Leave empty to use all themes.")]
        public string countingActiveThemeName;

        [Header("Addition Game Settings")]
        public int additionSlotCount = 5;
        public int additionMinPerGrid = 1;
        public int additionMaxPerGrid = 12;
        public bool additionDiceMode = false;
        public bool additionFingerMode = false;
        public bool additionCountAddMode = false;
        public bool additionNumbersOnlyMode = false;
        [Range(2, 5)] public int additionMinOperandCount = 2;
        [Range(2, 5)] public int additionMaxOperandCount = 3;
        public int additionMinNumberValue = 1;
        public int additionMaxNumberValue = 50;
        [Tooltip("The active theme name to restrict object spawning. Leave empty to use all themes.")]
        public string additionActiveThemeName;

        [Header("Comparison Game Settings")]
        public int comparisonSlotCount = 3;
        public int comparisonMinVal = 1;
        public int comparisonMaxVal = 10;
        public bool comparisonMixAdditionEquations = true;
        public bool comparisonNumbersOnlyMode = false;
        [Tooltip("The active theme name to restrict object spawning. Leave empty to use all themes.")]
        public string comparisonActiveThemeName;

        [Header("Matching Game Settings")]
        public KidGame.Mechanics.Matching.MatchVariant matchingLeftVariant = KidGame.Mechanics.Matching.MatchVariant.Number;
        public KidGame.Mechanics.Matching.MatchVariant matchingRightVariant = KidGame.Mechanics.Matching.MatchVariant.Word;
        public int matchingSlotCount = 5;
        public int matchingMinVal = 1;
        public int matchingMaxVal = 10;
        public bool matchingShuffleLeftColumn = false;

        [Header("Recall Game Settings")]
        public int recallSlotCount = 4;
        public int recallMinSequenceLength = 5;
        public int recallMaxSequenceLength = 10;
        public int recallMinStartValue = 1;
        public int recallMaxStartValue = 20;
        public int recallStep = 1;
        public bool recallCountBackwards = false;
        public int recallMinConsecutiveRevealed = 1;
        public int recallMaxConsecutiveRevealed = 2;
        public int recallMinConsecutiveHidden = 1;
        public int recallMaxConsecutiveHidden = 2;

        [Header("Tracing Game Settings")]
        public bool tracingSpellModeActive = false;
        public List<string> tracingValuesToTrace = new List<string>();
        public int tracingCustomSpawnCount = 1;
    }

    [CreateAssetMenu(fileName = "NewLevelData", menuName = "Level Select/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Level Information")]
        public int levelNumber;
        public string levelName;
        public string levelSubtitle;
        [TextArea(2, 4)]
        public string levelEndTip;
        public bool isUnlockedByDefault = false;

        [Header("Visual Theme")]
        [Tooltip("The preset theme name from ThemeDatabase. If selected, overrides theme color and background sprite.")]
        public string themePresetName;
        public Color levelThemeColor = Color.white;
        public Sprite levelBackgroundSprite;

        [Header("Pages (Rounds)")]
        public List<PageData> pages = new List<PageData>();

        [Header("Legacy (Fallback)")]
        [Tooltip("Scene to load if not using the dynamic scriptable game scene.")]
        public string sceneToLoad = "Game";
        public Sprite levelIcon;
    }
}