using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Interface;

namespace KidGame.Mechanics.Counting
{
    public class CountingGameManager : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [Tooltip("One prefab per object category (balls, flowers, cakes…). Need at least 5.")]
        [SerializeField] private GameObject[] objectCategoryPrefabs;
        [Tooltip("The CountingSlot row prefab.")]
        [SerializeField] private GameObject slotPrefab;
        [Tooltip("The AnswerCard draggable prefab.")]
        [SerializeField] private GameObject answerCardPrefab;

        [Header("Containers")]
        [SerializeField] private Transform slotsContainer;
        [Tooltip("Separate container transform configured specifically for Premade Counting Slots (Optional). If null, uses slotsContainer.")]
        [SerializeField] private Transform premadeSlotsContainer;
        [SerializeField] private Transform answersContainer;

        [Header("Shared")]
        [SerializeField] private Button nextButton;

        [Header("Config")]
        [Tooltip("Number of slot rows per round (always 5 per design).")]
        [SerializeField] private int slotCount = 5;
        [SerializeField, Range(1, 12)] private int minCount = 1;
        [SerializeField, Range(1, 12)] private int maxCount = 12;

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

        [Header("Object Category Themes")]
        [Tooltip("Define themed collections of object prefabs (e.g., Ocean, Animals). Enable one to restrict spawning to that collection.")]
        [SerializeField] private List<ObjectCategoryTheme> themes;

        private GameObject premadeSlotPrefab;
        private KidGame.Interface.PremadeSlotData premadeSlotData;

        public GameObject PremadeSlotPrefab { get => premadeSlotPrefab; set => premadeSlotPrefab = value; }
        public KidGame.Interface.PremadeSlotData PremadeSlotData { get => premadeSlotData; set => premadeSlotData = value; }
        public Transform PremadeSlotsContainer { get => premadeSlotsContainer; set => premadeSlotsContainer = value; }

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

        private readonly List<CountingSlot> _slots = new List<CountingSlot>();
        private readonly List<AnswerCard>   _cards = new List<AnswerCard>();
        private int _answeredCount;
        public Button NextButton => nextButton;

        public void Configure(int slotCount, int minCount, int maxCount, bool diceMode, bool fingerMode, string activeThemeName, GameObject premadeSlotPrefab = null, KidGame.Interface.PremadeSlotData premadeSlotData = null)
        {
            this.slotCount = slotCount;
            this.minCount = minCount;
            this.maxCount = maxCount;
            this.diceMode = diceMode;
            this.fingerMode = fingerMode;
            this.premadeSlotPrefab = premadeSlotPrefab;
            this.premadeSlotData = premadeSlotData;

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
            ConfigureNextButton(nextButton);

            SetNextButtonInteractable(false);

            if (KidGame.Interface.GameFlowManager.Instance == null)
            {
                if (nextButton != null) nextButton.onClick.AddListener(GenerateRound);
            }

            GenerateRound();
        }

        public void StartGame()
        {
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

        private void SetNextButtonInteractable(bool interactable)
        {
            if (nextButton != null) nextButton.interactable = (KidGame.Interface.GameFlowManager.Instance != null) || interactable;
        }




        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by AnswerDropZone when a slot is correctly answered.</summary>
        public void OnSlotAnswered(CountingSlot slot)
        {
            _answeredCount++;
            if (_answeredCount >= _slots.Count)
                SetNextButtonInteractable(true);
            GameFlowManager.Instance?.NotifyRoundStateChanged();
        }

        private void OnPremadeRoundCompleted()
        {
            SetNextButtonInteractable(true);
            GameFlowManager.Instance?.NotifyRoundStateChanged();
        }

        // ── Round Management ──────────────────────────────────────────────────

        public void GenerateRound()
        {
            ClearPrevious();
            _answeredCount = 0;
            SetNextButtonInteractable(false);

            bool isPremade = (premadeSlotPrefab != null || premadeSlotData != null);

            Transform activeSlotsContainer = (isPremade && premadeSlotsContainer != null) 
                ? premadeSlotsContainer 
                : slotsContainer;

            if (premadeSlotsContainer != null && premadeSlotsContainer != slotsContainer)
            {
                premadeSlotsContainer.gameObject.SetActive(isPremade);
                slotsContainer.gameObject.SetActive(!isPremade);
            }

            if (isPremade)
            {
                // Premade Content / Slot Mode: Spawn pre-designed level layout prefab or template from ScriptableObject
                GameObject targetPrefab = premadeSlotPrefab;
                if (targetPrefab == null && premadeSlotData != null)
                {
                    targetPrefab = premadeSlotData.templatePrefab;
                }

                if (targetPrefab == null)
                {
                    targetPrefab = Resources.Load<GameObject>("PremadeTemplate");
                }

#if UNITY_EDITOR
                if (targetPrefab == null)
                {
                    targetPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Levels/Levels Object/SharedLevels/1 - 20.prefab");
                }
#endif

                if (targetPrefab == null)
                {
                    Debug.LogError("[CountingGameManager] No premade slot prefab or template assigned!");
                    return;
                }

                var slotGo = Instantiate(targetPrefab, activeSlotsContainer);

                List<int> numberAnswers = new List<int>();
                List<string> stringAnswers = new List<string>();

                var premadeCounting = slotGo.GetComponent<PremadeCountingSlot>();
                var premadeRecall = slotGo.GetComponent<KidGame.Mechanics.NumberRecall.PremadeRecallSlot>();

                if (premadeCounting != null)
                {
                    if (premadeSlotData != null) premadeCounting.ApplySlotData(premadeSlotData);
                    var res = premadeCounting.Setup(OnPremadeRoundCompleted, true);
                    numberAnswers = res.numberAnswers;
                    stringAnswers = res.stringAnswers;
                }
                else if (premadeRecall != null)
                {
                    if (premadeSlotData != null) premadeRecall.ApplySlotData(premadeSlotData);
                    numberAnswers = premadeRecall.Setup(OnPremadeRoundCompleted, true);
                }
                else
                {
                    premadeCounting = slotGo.AddComponent<PremadeCountingSlot>();
                    var res = premadeCounting.Setup(OnPremadeRoundCompleted, true);
                    numberAnswers = res.numberAnswers;
                    stringAnswers = res.stringAnswers;
                }

                if (stringAnswers != null && stringAnswers.Count > 0)
                {
                    var premadeColors = new List<Color>();
                    for (int i = 0; i < stringAnswers.Count; i++) premadeColors.Add(Palette[i % Palette.Length]);
                    Shuffle(premadeColors);

                    var shuffled = new List<string>(stringAnswers);
                    Shuffle(shuffled);

                    for (int i = 0; i < shuffled.Count; i++)
                    {
                        var go = Instantiate(answerCardPrefab, answersContainer);
                        var card = go.GetComponent<AnswerCard>();
                        char c = !string.IsNullOrEmpty(shuffled[i]) ? shuffled[i][0] : ' ';
                        card.Setup((int)c, premadeColors[i], shuffled[i], customAcceptedScaleMultiplier: 1.4f);
                        _cards.Add(card);
                    }
                }
                else if (numberAnswers != null && numberAnswers.Count > 0)
                {
                    var premadeColors = new List<Color>();
                    for (int i = 0; i < numberAnswers.Count; i++) premadeColors.Add(Palette[i % Palette.Length]);
                    Shuffle(premadeColors);

                    var shuffled = new List<int>(numberAnswers);
                    Shuffle(shuffled);

                    for (int i = 0; i < shuffled.Count; i++)
                    {
                        var go = Instantiate(answerCardPrefab, answersContainer);
                        var card = go.GetComponent<AnswerCard>();
                        card.Setup(shuffled[i], premadeColors[i], customAcceptedScaleMultiplier: 1.4f);
                        _cards.Add(card);
                    }
                }

                UpdateScrollLocking();
                return;
            }

            // ── Validate required Inspector references for procedural generation ──────
            if (diceMode)
            {
                if (dicePrefabs == null || dicePrefabs.Length != 6)
                {
                    Debug.LogError("[CountingGame] Dice Mode is enabled, but Dice Prefabs array does not have exactly 6 elements.");
                    return;
                }
                for (int i = 0; i < 6; i++)
                {
                    if (dicePrefabs[i] == null)
                    {
                        Debug.LogError($"[CountingGame] Dice Prefab at index {i} is not assigned.");
                        return;
                    }
                }
            }
            else if (fingerMode)
            {
                if (fingerPrefabs == null || fingerPrefabs.Length != 5)
                {
                    Debug.LogError("[CountingGame] Finger Mode is enabled, but Finger Prefabs array does not have exactly 5 elements.");
                    return;
                }
                for (int i = 0; i < 5; i++)
                {
                    if (fingerPrefabs[i] == null)
                    {
                        Debug.LogError($"[CountingGame] Finger Prefab at index {i} is not assigned.");
                        return;
                    }
                }
            }
            else
            {
                var activePrefabs = GetActiveThemePrefabs();
                if (activePrefabs == null || activePrefabs.Length == 0)
                {
                    activePrefabs = objectCategoryPrefabs;
                }
                if (activePrefabs == null || activePrefabs.Length == 0)
                {
                    Debug.LogError("[CountingGame] No object prefabs available. Assign default category prefabs or enable an object category theme.");
                    return;
                }
            }
            if (slotPrefab == null)
            {
                Debug.LogError("[CountingGame] Slot Prefab is not assigned in the Inspector.");
                return;
            }
            if (answerCardPrefab == null)
            {
                Debug.LogError("[CountingGame] Answer Card Prefab is not assigned in the Inspector.");
                return;
            }
            if (slotsContainer == null)
            {
                Debug.LogError("[CountingGame] Slots Container is not assigned.");
                return;
            }
            if (answersContainer == null)
            {
                Debug.LogError("[CountingGame] Answers Container is not assigned.");
                return;
            }
            // ─────────────────────────────────────────────────────────────────

            List<int> counts;
            List<(List<GameObject> prefabs, List<int> itemValues, int totalSum)> slotData = null;
            List<int> normalCounts = null;
            List<int> catOrder = null;

            var categoryPrefabs = GetActiveThemePrefabs();
            if (categoryPrefabs == null || categoryPrefabs.Length == 0)
            {
                categoryPrefabs = objectCategoryPrefabs;
            }

            if (diceMode || fingerMode)
            {
                int maxVal = diceMode ? 6 : 5;
                var rawData = GenerateCountingDiceOrFingerData(slotCount, minCount, maxCount, maxVal);
                counts = rawData.Select(d => d.totalSum).ToList();

                slotData = new List<(List<GameObject> prefabs, List<int> itemValues, int totalSum)>();
                foreach (var data in rawData)
                {
                    var prefabs = new List<GameObject>();
                    foreach (var val in data.itemValues)
                        prefabs.Add(diceMode ? dicePrefabs[val - 1] : fingerPrefabs[val - 1]);

                    slotData.Add((prefabs, data.itemValues, data.totalSum));
                }
            }
            else
            {
                normalCounts = UniqueRandomList(slotCount, minCount, maxCount);
                counts = normalCounts;

                catOrder = Enumerable.Range(0, categoryPrefabs.Length).ToList();
                Shuffle(catOrder);
            }

            // 4. Shuffle answer values — exactly one per slot
            var answerValues = new List<int>(counts);
            Shuffle(answerValues);

            // 3. Shuffle color palette (exactly matching the number of answers)
            var colors = new List<Color>();
            for (int i = 0; i < answerValues.Count; i++)
            {
                colors.Add(Palette[i % Palette.Length]);
            }
            Shuffle(colors);

            // 5. Spawn slots
            int actualSlotCount = (diceMode || fingerMode) ? slotData.Count : normalCounts.Count;
            for (int i = 0; i < actualSlotCount; i++)
            {
                var go   = Instantiate(slotPrefab, slotsContainer);
                var slot = go.GetComponent<CountingSlot>();

                if (diceMode || fingerMode)
                {
                    slot.Setup(slotData[i].prefabs, slotData[i].itemValues, slotData[i].totalSum, this);
                }
                else
                {
                    var cat  = categoryPrefabs[catOrder[i % catOrder.Count]];
                    slot.Setup(cat, normalCounts[i], this);
                }
                _slots.Add(slot);
            }

            // Force layout to recalculate immediately after spawning all slots
            var rt = slotsContainer.GetComponent<RectTransform>();
            if (rt != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            // 6. Spawn answer cards into the answer grid
            for (int i = 0; i < answerValues.Count; i++)
            {
                var go   = Instantiate(answerCardPrefab, answersContainer);
                var card = go.GetComponent<AnswerCard>();
                card.Setup(answerValues[i], colors[i], customAcceptedScaleMultiplier: 1.4f);
                _cards.Add(card);
            }
            UpdateScrollLocking();
        }

        private void ClearPrevious()
        {
            if (slotsContainer != null)
            {
                for (int i = slotsContainer.childCount - 1; i >= 0; i--)
                {
                    var child = slotsContainer.GetChild(i);
                    child.gameObject.SetActive(false);
                    child.SetParent(null);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
            if (premadeSlotsContainer != null && premadeSlotsContainer != slotsContainer)
            {
                for (int i = premadeSlotsContainer.childCount - 1; i >= 0; i--)
                {
                    var child = premadeSlotsContainer.GetChild(i);
                    child.gameObject.SetActive(false);
                    child.SetParent(null);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
            if (answersContainer != null)
            {
                for (int i = answersContainer.childCount - 1; i >= 0; i--)
                {
                    var child = answersContainer.GetChild(i);
                    child.gameObject.SetActive(false);
                    child.SetParent(null);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
            _slots.Clear();
            _cards.Clear();
        }

        // ── Generation Helpers ────────────────────────────────────────────────

        private List<(List<int> itemValues, int totalSum)> GenerateCountingDiceOrFingerData(int count, int minCount, int maxCount, int maxValPerItem)
        {
            var results = new List<(List<int>, int)>();
            var targetSums = UniqueRandomList(count, minCount, maxCount);

            foreach (int targetSum in targetSums)
            {
                var itemValues = BreakDownTargetSumIntoItems(targetSum, maxValPerItem);
                results.Add((itemValues, targetSum));
            }

            return results;
        }

        private List<int> BreakDownTargetSumIntoItems(int targetSum, int maxValPerItem)
        {
            var items = new List<int>();
            int remaining = targetSum;

            if (remaining <= maxValPerItem)
            {
                items.Add(remaining);
                return items;
            }

            while (remaining > 0)
            {
                int chunk = Mathf.Min(maxValPerItem, remaining);
                items.Add(chunk);
                remaining -= chunk;
            }

            return items;
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

        private List<int> UniqueRandomList(int count, int min, int max)
        {
            int rangeCount = Mathf.Max(1, max - min + 1);
            var pool = Enumerable.Range(min, rangeCount).ToList();
            Shuffle(pool);

            var result = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (i < pool.Count)
                {
                    result.Add(pool[i]);
                }
                else
                {
                    result.Add(Random.Range(min, max + 1));
                }
            }
            return result;
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
            UpdateScrollLockForContainer(slotsContainer);
            UpdateScrollLockForContainer(answersContainer);
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

                // Answers scroll horizontally; slots scroll vertically
                bool scrollVertical = (container != answersContainer);

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
            if (premadeSlotPrefab != null || premadeSlotData != null)
            {
                return nextButton != null && nextButton.interactable;
            }
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
