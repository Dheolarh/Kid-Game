using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Mechanics.Counting;
using KidGame.Interface;

namespace KidGame.Mechanics.Addition
{
    public class AdditionGameManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [Tooltip("Object icon prefabs — need at least 2 (left/right use different ones per slot).")]
        [SerializeField] private GameObject[] objectCategoryPrefabs;
        [Tooltip("The AdditionSlot row prefab (normal mode).")]
        [SerializeField] private GameObject slotPrefab;
        [Tooltip("The AdditionSlot row prefab (count-add mode).")]
        [SerializeField] private GameObject countAddSlotPrefab;
        [Tooltip("The AdditionSlot row prefab (numbers-only mode).")]
        [SerializeField] private GameObject numbersOnlySlotPrefab;
        [Tooltip("The AnswerCard draggable prefab (same as counting game).")]
        [SerializeField] private GameObject answerCardPrefab;

        [Header("Portrait Containers")]
        [SerializeField] private Transform portraitSlotsContainer;
        [SerializeField] private Transform portraitAnswersContainer;

        [Header("Landscape Containers")]
        [SerializeField] private Transform landscapeSlotsContainer;
        [SerializeField] private Transform landscapeAnswersContainer;

        [Header("Shared")]
        [SerializeField] private Button portraitNextButton;
        [SerializeField] private Button landscapeNextButton;

        [Header("Config")]
        [Tooltip("Number of slot rows per round.")]
        [SerializeField] private int slotCount = 5;
        [Tooltip("Minimum number of objects per grid side.")]
        [SerializeField, Range(1, 12)] private int minPerGrid = 1;
        [Tooltip("Maximum number of objects per grid side.")]
        [SerializeField, Range(1, 12)] private int maxPerGrid = 12;

        [Header("Dice Mode")]
        [Tooltip("If true, spawns dice prefabs for counting instead of regular prefabs.")]
        [SerializeField] private bool diceMode;
        [Tooltip("Dice prefabs for face values 1 to 6 (index 0 = face 1, index 1 = face 2, ..., index 5 = face 6).")]
        [SerializeField] private GameObject[] dicePrefabs;

        [Header("Fingers Mode")]
        [Tooltip("If true, spawns finger prefabs for counting.")]
        [SerializeField] private bool fingerMode;
        [Tooltip("Finger prefabs for values 1 to 5 (index 0 = 1 finger, ..., index 4 = 5 fingers).")]
        [SerializeField] private GameObject[] fingerPrefabs;

        [Header("Mode Configuration")]
        [Tooltip("If true, requires counting the left/right groups as sub-answers before adding them.")]
        [SerializeField] private bool countAddMode;

        public bool CountAddMode => countAddMode;

        [Header("Numbers Only Mode")]
        [SerializeField] private bool numbersOnlyMode;
        [Tooltip("Minimum number of operands in the addition equation.")]
        [SerializeField, Range(2, 5)] private int minOperandCount = 2;
        [Tooltip("Maximum number of operands in the addition equation.")]
        [SerializeField, Range(2, 5)] private int maxOperandCount = 3;
        [Tooltip("Minimum value of generated numbers in the addition equation.")]
        [SerializeField] private int minNumberValue = 1;
        [Tooltip("Maximum value of generated numbers in the addition equation.")]
        [SerializeField] private int maxNumberValue = 50;
        [SerializeField] private GameObject numberBoxPrefab;
        [SerializeField] private GameObject plusSymbolPrefab;
        [SerializeField] private GameObject equalsSymbolPrefab;

        public bool NumbersOnlyMode => numbersOnlyMode;
        public Color GetColorForIndex(int idx) => Palette[idx % Palette.Length];

        [Header("Object Category Themes")]
        [Tooltip("Define themed collections of object prefabs (e.g., Ocean, Animals). Enable one to restrict spawning to that collection.")]
        [SerializeField] private List<ObjectCategoryTheme> themes;


        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.30f, 0.24f),   // red
            new Color(0.20f, 0.60f, 0.86f),   // blue
            new Color(0.95f, 0.61f, 0.07f),   // orange
            new Color(0.15f, 0.68f, 0.38f),   // green
            new Color(0.61f, 0.35f, 0.71f),   // purple
            new Color(0.10f, 0.74f, 0.61f),   // teal
        };

        // ── Runtime state ─────────────────────────────────────────────────────

        private readonly List<MonoBehaviour> _slots = new List<MonoBehaviour>();
        private readonly List<AnswerCard>   _cards = new List<AnswerCard>();
        private int _answeredCount;

        // ── Orientation ───────────────────────────────────────────────────────

        private bool IsLandscape => Screen.width > Screen.height;

        private Transform ActiveSlotsContainer
            => IsLandscape ? landscapeSlotsContainer   : portraitSlotsContainer;

        private Transform ActiveAnswersContainer
            => IsLandscape ? landscapeAnswersContainer : portraitAnswersContainer;

        private bool _wasLandscape;

        public Button PortraitNextButton => portraitNextButton;
        public Button LandscapeNextButton => landscapeNextButton;

        public void Configure(int slotCount, int minPerGrid, int maxPerGrid, bool diceMode, bool fingerMode, bool countAddMode, bool numbersOnlyMode, int minOperandCount, int maxOperandCount, int minNumberValue, int maxNumberValue, string activeThemeName)
        {
            this.slotCount = slotCount;
            this.minPerGrid = minPerGrid;
            this.maxPerGrid = maxPerGrid;
            this.diceMode = diceMode;
            this.fingerMode = fingerMode;
            this.countAddMode = countAddMode;
            this.numbersOnlyMode = numbersOnlyMode;
            this.minOperandCount = minOperandCount;
            this.maxOperandCount = maxOperandCount;
            this.minNumberValue = minNumberValue;
            this.maxNumberValue = maxNumberValue;

            if (themes != null)
            {
                foreach (var theme in themes)
                {
                    if (theme != null)
                    {
                        theme.isEnabled = (theme.themeName == activeThemeName);
                    }
                }
            }

            _slots.Clear();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            ConfigureNextButton(portraitNextButton);
            ConfigureNextButton(landscapeNextButton);

            _wasLandscape = IsLandscape;
            SetNextButtonsInteractable(false);

            if (KidGame.Interface.GameFlowManager.Instance == null)
            {
                if (portraitNextButton != null) portraitNextButton.onClick.AddListener(GenerateRound);
                if (landscapeNextButton != null) landscapeNextButton.onClick.AddListener(GenerateRound);
            }

            GenerateRound();
        }

        private void ConfigureNextButton(Button btn)
        {
            if (btn == null) return;
            var colors = btn.colors;
            var disabled = colors.normalColor;
            disabled.a = 0.6f;
            colors.disabledColor = disabled;
            btn.colors = colors;
        }

        private void SetNextButtonsInteractable(bool interactable)
        {
            if (portraitNextButton != null) portraitNextButton.interactable = interactable;
            if (landscapeNextButton != null) landscapeNextButton.interactable = interactable;
        }

        private void Update()
        {
            if (this == null) return;
            if (portraitSlotsContainer == null || landscapeSlotsContainer == null ||
                portraitAnswersContainer == null || landscapeAnswersContainer == null) return;

            // Detect orientation flip and migrate live slots/cards to the new containers
            bool landscape = IsLandscape;
            if (landscape != _wasLandscape && _slots.Count > 0)
            {
                _wasLandscape = landscape;
                MoveToActiveContainers();
            }
        }

        private void MoveToActiveContainers()
        {
            if (this == null) return;
            var newSlots   = ActiveSlotsContainer;
            var newAnswers = ActiveAnswersContainer;
            if (newSlots == null || newAnswers == null) return;

            foreach (var slot in _slots)
                if (slot) slot.transform.SetParent(newSlots, worldPositionStays: false);

            foreach (var card in _cards)
            {
                if (card && !card.IsAccepted)
                {
                    card.transform.SetParent(newAnswers, worldPositionStays: false);
                    card.UpdateHomeParent(newAnswers);
                }
            }

            var rtSlots   = newSlots.GetComponent<RectTransform>();
            var rtAnswers = newAnswers.GetComponent<RectTransform>();
            if (rtSlots)   UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rtSlots);
            if (rtAnswers) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rtAnswers);

            Debug.Log($"[AdditionGame] Orientation changed → moved {_slots.Count} slots, {_cards.Count} cards.");
            UpdateScrollLocking();
        }

        // ── Public API (called by AdditionSlot via lambda) ────────────────────

        /// <summary>Called once per correctly-answered slot.</summary>
        public void OnSlotAnswered()
        {
            _answeredCount++;
            if (_answeredCount >= _slots.Count)
                SetNextButtonsInteractable(true);
            GameFlowManager.Instance?.NotifyRoundStateChanged();
        }

        // ── Round Management ──────────────────────────────────────────────────

        public void GenerateRound()
        {
            var activePrefabs = GetActiveThemePrefabs();
            if (activePrefabs == null || activePrefabs.Length == 0)
            {
                activePrefabs = objectCategoryPrefabs;
            }

            // ── Validate required Inspector references ────────────────────────
            if (diceMode)
            {
                if (dicePrefabs == null || dicePrefabs.Length != 6)
                {
                    Debug.LogError("[AdditionGame] Dice Mode is enabled, but Dice Prefabs array does not have exactly 6 elements.");
                    return;
                }
                for (int i = 0; i < 6; i++)
                {
                    if (dicePrefabs[i] == null)
                    {
                        Debug.LogError($"[AdditionGame] Dice Prefab at index {i} is not assigned.");
                        return;
                    }
                }
            }
            else if (fingerMode)
            {
                if (fingerPrefabs == null || fingerPrefabs.Length != 5)
                {
                    Debug.LogError("[AdditionGame] Finger Mode is enabled, but Finger Prefabs array does not have exactly 5 elements.");
                    return;
                }
                for (int i = 0; i < 5; i++)
                {
                    if (fingerPrefabs[i] == null)
                    {
                        Debug.LogError($"[AdditionGame] Finger Prefab at index {i} is not assigned.");
                        return;
                    }
                }
            }
            else
            {
                if (activePrefabs == null || activePrefabs.Length == 0)
                {
                    Debug.LogError("[AdditionGame] No object prefabs available. Assign default category prefabs or enable an object category theme.");
                    return;
                }
            }
            var prefabToSpawn = (countAddMode && !numbersOnlyMode) ? countAddSlotPrefab : (numbersOnlyMode ? numbersOnlySlotPrefab : slotPrefab);
            if (prefabToSpawn == null) prefabToSpawn = slotPrefab;

            if (prefabToSpawn == null)
            {
                Debug.LogError($"[AdditionGame] {(numbersOnlyMode ? "Numbers-Only" : (countAddMode ? "Count-Add" : "Normal"))} Slot Prefab is not assigned in the Inspector.");
                return;
            }
            if (answerCardPrefab == null)
            {
                Debug.LogError("[AdditionGame] Answer Card Prefab is not assigned in the Inspector.");
                return;
            }
            if (ActiveSlotsContainer == null)
            {
                Debug.LogError("[AdditionGame] Slots Container for the current orientation is not assigned.");
                return;
            }
            if (ActiveAnswersContainer == null)
            {
                Debug.LogError("[AdditionGame] Answers Container for the current orientation is not assigned.");
                return;
            }
            // ─────────────────────────────────────────────────────────────────

            ClearPrevious();
            _answeredCount = 0;
            SetNextButtonsInteractable(false);

            List<int> sums = new List<int>();
            var slotData = new List<(List<GameObject> leftPrefabs, List<GameObject> rightPrefabs, int leftSum, int rightSum)>();
            List<(int left, int right)> normalPairs = null;
            var equations = new List<List<int>>();

            if (numbersOnlyMode)
            {
                int minOp = Mathf.Max(2, minOperandCount);
                int maxOp = Mathf.Max(minOp, maxOperandCount);
                for (int s = 0; s < slotCount; s++)
                {
                    int opCount = Random.Range(minOp, maxOp + 1);
                    var eq = new List<int>();
                    int sum = 0;
                    for (int o = 0; o < opCount; o++)
                    {
                        int val = Random.Range(minNumberValue, maxNumberValue + 1);
                        eq.Add(val);
                        sum += val;
                    }
                    equations.Add(eq);
                    sums.Add(sum);
                }
            }
            else if (diceMode || fingerMode)
            {
                int maxVal = diceMode ? 6 : 5;
                var rawPairs = GenerateDiceOrFingerPairs(slotCount, minPerGrid, maxPerGrid, maxVal);
                sums = rawPairs.Select(p => p.leftSum + p.rightSum).ToList();

                foreach (var pair in rawPairs)
                {
                    var leftPrefabs = new List<GameObject>();
                    foreach (var val in pair.leftValues)
                        leftPrefabs.Add(diceMode ? dicePrefabs[val - 1] : fingerPrefabs[val - 1]);

                    var rightPrefabs = new List<GameObject>();
                    foreach (var val in pair.rightValues)
                        rightPrefabs.Add(diceMode ? dicePrefabs[val - 1] : fingerPrefabs[val - 1]);

                    slotData.Add((leftPrefabs, rightPrefabs, pair.leftSum, pair.rightSum));
                }
            }
            else
            {
                normalPairs = GenerateUniqueSumPairs(slotCount, minPerGrid, maxPerGrid);
                sums = normalPairs.Select(p => p.left + p.right).ToList();
            }

            // 4. Collect and shuffle answer values
            var answerValues = new List<int>();
            if (countAddMode && !numbersOnlyMode)
            {
                if (diceMode || fingerMode)
                {
                    foreach (var data in slotData)
                    {
                        answerValues.Add(data.leftSum);
                        answerValues.Add(data.rightSum);
                        answerValues.Add(data.leftSum + data.rightSum);
                    }
                }
                else
                {
                    foreach (var pair in normalPairs)
                    {
                        answerValues.Add(pair.left);
                        answerValues.Add(pair.right);
                        answerValues.Add(pair.left + pair.right);
                    }
                }
            }
            else
            {
                answerValues.AddRange(sums);
            }
            Shuffle(answerValues);

            // 3. Shuffle color palette (exactly matching the number of answers)
            var colors = new List<Color>();
            for (int i = 0; i < answerValues.Count; i++)
            {
                colors.Add(Palette[i % Palette.Length]);
            }
            Shuffle(colors);

            // 5. Spawn addition slots
            int actualSlotCount = numbersOnlyMode ? equations.Count : ((diceMode || fingerMode) ? slotData.Count : normalPairs.Count);
            for (int i = 0; i < actualSlotCount; i++)
            {
                var go = Instantiate(prefabToSpawn, ActiveSlotsContainer);

                if (numbersOnlyMode)
                {
                    var slot = go.GetComponent<NumbersOnlyAdditionSlot>();
                    slot.Setup(equations[i], this, numberBoxPrefab, plusSymbolPrefab, equalsSymbolPrefab);
                    _slots.Add(slot);
                }
                else
                {
                    var slot = go.GetComponent<AdditionSlot>();
                    if (diceMode || fingerMode)
                    {
                        var data = slotData[i];
                        slot.Setup(data.leftPrefabs, data.rightPrefabs, data.leftSum, data.rightSum, this);
                    }
                    else
                    {
                        // Pick two different categories specifically for this slot row from active prefabs
                        var catOrder = Enumerable.Range(0, activePrefabs.Length).ToList();
                        Shuffle(catOrder);
                        var leftPrefab  = activePrefabs[catOrder[0]];
                        var rightPrefab = activePrefabs[catOrder.Count > 1 ? catOrder[1] : catOrder[0]];

                        slot.Setup(leftPrefab,  normalPairs[i].left,
                                   rightPrefab, normalPairs[i].right,
                                   this);
                    }
                    _slots.Add(slot);
                }
            }

            // Force layout to recalculate immediately after spawning all slots
            var rt = ActiveSlotsContainer.GetComponent<RectTransform>();
            if (rt != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);



            // 6. Spawn answer cards into the active orientation's answer grid
            for (int i = 0; i < answerValues.Count; i++)
            {
                var go   = Instantiate(answerCardPrefab, ActiveAnswersContainer);
                var card = go.GetComponent<AnswerCard>();
                card.Setup(answerValues[i], colors[i]);
                _cards.Add(card);
            }
            UpdateScrollLocking();
        }

        private void ClearPrevious()
        {
            foreach (var s in _slots) { if (s) Destroy(s.gameObject); }
            foreach (var c in _cards) { if (c) Destroy(c.gameObject); }
            _slots.Clear();
            _cards.Clear();
        }

        private List<(List<int> leftValues, List<int> rightValues, int leftSum, int rightSum)> GenerateDiceOrFingerPairs(int count, int minItems, int maxItems, int maxValPerItem)
        {
            var results = new List<(List<int>, List<int>, int, int)>();
            var usedSums = new HashSet<int>();
            int maxTries = 1000;

            while (results.Count < count && maxTries-- > 0)
            {
                int leftCount = Random.Range(minItems, maxItems + 1);
                int rightCount = Random.Range(minItems, maxItems + 1);

                List<int> leftFaces = new List<int>();
                int leftSum = 0;
                for (int d = 0; d < leftCount; d++)
                {
                    int face = Random.Range(1, maxValPerItem + 1);
                    leftFaces.Add(face);
                    leftSum += face;
                }

                List<int> rightFaces = new List<int>();
                int rightSum = 0;
                for (int d = 0; d < rightCount; d++)
                {
                    int face = Random.Range(1, maxValPerItem + 1);
                    rightFaces.Add(face);
                    rightSum += face;
                }

                int totalSum = leftSum + rightSum;

                if (!usedSums.Contains(totalSum))
                {
                    results.Add((leftFaces, rightFaces, leftSum, rightSum));
                    usedSums.Add(totalSum);
                }
            }

            return results;
        }

        // ── Generation Helpers ────────────────────────────────────────────────

        private List<(int left, int right)> GenerateUniqueSumPairs(int count, int min, int max)
        {
            var pairs    = new List<(int, int)>();
            var usedSums = new HashSet<int>();
            int maxTries = 500;

            while (pairs.Count < count && maxTries-- > 0)
            {
                int left  = Random.Range(min, max + 1);
                int right = Random.Range(min, max + 1);
                int sum   = left + right;

                if (!usedSums.Contains(sum))
                {
                    pairs.Add((left, right));
                    usedSums.Add(sum);
                }
            }

            // If we somehow couldn't fill all slots (very unlikely), log a warning
            if (pairs.Count < count)
                Debug.LogWarning($"[AdditionGame] Could only generate {pairs.Count}/{count} unique-sum pairs. " +
                                 "Consider widening minPerGrid/maxPerGrid.");

            return pairs;
        }

        private GameObject[] GetActiveThemePrefabs()
        {
            if (themes == null) return null;
            foreach (var theme in themes)
            {
                if (theme != null && theme.isEnabled && theme.prefabs != null && theme.prefabs.Length > 0)
                {
                    return theme.prefabs;
                }
            }
            return null;
        }

        private void UpdateScrollLocking()
        {
            UpdateScrollLockingInternal();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(UpdateScrollLockingRoutine());
            }
        }

        private System.Collections.IEnumerator UpdateScrollLockingRoutine()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            UpdateScrollLockingInternal();
        }

        private void UpdateScrollLockingInternal()
        {
            UpdateScrollLockForContainer(portraitSlotsContainer);
            UpdateScrollLockForContainer(portraitAnswersContainer);
            UpdateScrollLockForContainer(landscapeSlotsContainer);
            UpdateScrollLockForContainer(landscapeAnswersContainer);
        }

        private void UpdateScrollLockForContainer(Transform container)
        {
            if (container == null) return;

            var scrollRect = container.GetComponentInParent<ScrollRect>();
            if (scrollRect == null) return;

            var contentRt = container as RectTransform;
            var viewportRt = scrollRect.viewport;
            if (viewportRt == null)
            {
                viewportRt = scrollRect.GetComponent<RectTransform>();
            }

            if (contentRt != null && viewportRt != null)
            {
                // Force-rebuild all nested child layouts recursively first, ensuring ContentSizeFitter / LayoutGroup
                // components have fully computed their actual size before the parent contentRt layout is rebuilt.
                RebuildLayoutsRecursive(contentRt);

                LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRt);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

                // Portrait answers scroll horizontally, all other lists (slots and landscape answers) scroll vertically
                bool scrollVertical = (container != portraitAnswersContainer);

                if (scrollVertical)
                {
                    float contentHeight = Mathf.Max(contentRt.rect.height, UnityEngine.UI.LayoutUtility.GetPreferredHeight(contentRt));
                    scrollRect.vertical = (contentHeight > viewportRt.rect.height);
                    scrollRect.horizontal = false;
                }
                else
                {
                    float contentWidth = Mathf.Max(contentRt.rect.width, UnityEngine.UI.LayoutUtility.GetPreferredWidth(contentRt));
                    scrollRect.horizontal = (contentWidth > viewportRt.rect.width);
                    scrollRect.vertical = false;
                }
            }
        }

        private void RebuildLayoutsRecursive(Transform t)
        {
            if (t == null) return;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child != null)
                {
                    RebuildLayoutsRecursive(child);
                    var childRt = child as RectTransform;
                    if (childRt != null)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(childRt);
                    }
                }
            }
        }

        public bool IsRoundCompleted()
        {
            if (_slots == null || _slots.Count == 0) return false;
            return _answeredCount >= _slots.Count;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
