using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Portrait Containers")]
        [SerializeField] private Transform portraitSlotsContainer;
        [SerializeField] private Transform portraitAnswersContainer;

        [Header("Landscape Containers")]
        [SerializeField] private Transform landscapeSlotsContainer;
        [SerializeField] private Transform landscapeAnswersContainer;

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

        // ── Orientation ─────────────────────────────────────────────────────

        private bool IsLandscape => Screen.width > Screen.height;

        private Transform ActiveSlotsContainer
            => IsLandscape ? landscapeSlotsContainer   : portraitSlotsContainer;

        private Transform ActiveAnswersContainer
            => IsLandscape ? landscapeAnswersContainer : portraitAnswersContainer;

        private bool _wasLandscape;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            var colors          = nextButton.colors;
            var disabled        = colors.normalColor;
            disabled.a          = 0.6f;
            colors.disabledColor = disabled;
            nextButton.colors   = colors;

            _wasLandscape = IsLandscape;
            nextButton.interactable = false;
            nextButton.onClick.AddListener(GenerateRound);
            GenerateRound();
        }

        private void Update()
        {
            if (this == null) return;
            if (portraitSlotsContainer == null || landscapeSlotsContainer == null ||
                portraitAnswersContainer == null || landscapeAnswersContainer == null) return;

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

            Debug.Log($"[CountingGame] Orientation changed → moved {_slots.Count} slots, {_cards.Count} cards.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by AnswerDropZone when a slot is correctly answered.</summary>
        public void OnSlotAnswered(CountingSlot slot)
        {
            _answeredCount++;
            if (_answeredCount >= _slots.Count)
                nextButton.interactable = true;
        }

        // ── Round Management ──────────────────────────────────────────────────

        public void GenerateRound()
        {
            // ── Validate required Inspector references ────────────────────────
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
                if (objectCategoryPrefabs == null || objectCategoryPrefabs.Length == 0)
                {
                    Debug.LogError("[CountingGame] Object Category Prefabs array is empty.");
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
            if (ActiveSlotsContainer == null)
            {
                Debug.LogError("[CountingGame] Slots Container for the current orientation is not assigned.");
                return;
            }
            if (ActiveAnswersContainer == null)
            {
                Debug.LogError("[CountingGame] Answers Container for the current orientation is not assigned.");
                return;
            }
            // ─────────────────────────────────────────────────────────────────

            ClearPrevious();
            _answeredCount = 0;
            nextButton.interactable = false;

            List<int> counts;
            List<(List<GameObject> prefabs, int totalSum)> slotData = null;
            List<int> normalCounts = null;
            List<int> catOrder = null;

            var activePrefabs = GetActiveThemePrefabs();
            if (activePrefabs == null || activePrefabs.Length == 0)
            {
                activePrefabs = objectCategoryPrefabs;
            }

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
            if (ActiveSlotsContainer == null)
            {
                Debug.LogError("[CountingGame] Slots Container for the current orientation is not assigned.");
                return;
            }
            if (ActiveAnswersContainer == null)
            {
                Debug.LogError("[CountingGame] Answers Container for the current orientation is not assigned.");
                return;
            }
            // ─────────────────────────────────────────────────────────────────

            if (diceMode || fingerMode)
            {
                int maxVal = diceMode ? 6 : 5;
                var rawData = GenerateCountingDiceOrFingerData(slotCount, minCount, maxCount, maxVal);
                counts = rawData.Select(d => d.totalSum).ToList();

                slotData = new List<(List<GameObject>, int)>();
                foreach (var data in rawData)
                {
                    var prefabs = new List<GameObject>();
                    foreach (var val in data.itemValues)
                        prefabs.Add(diceMode ? dicePrefabs[val - 1] : fingerPrefabs[val - 1]);

                    slotData.Add((prefabs, data.totalSum));
                }
            }
            else
            {
                normalCounts = UniqueRandomList(slotCount, minCount, maxCount);
                counts = normalCounts;

                catOrder = Enumerable.Range(0, activePrefabs.Length).ToList();
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
                var go   = Instantiate(slotPrefab, ActiveSlotsContainer);
                var slot = go.GetComponent<CountingSlot>();

                if (diceMode || fingerMode)
                {
                    slot.Setup(slotData[i].prefabs, slotData[i].totalSum, this);
                }
                else
                {
                    var cat  = activePrefabs[catOrder[i % catOrder.Count]];
                    slot.Setup(cat, normalCounts[i], this);
                }
                _slots.Add(slot);
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
        }

        private void ClearPrevious()
        {
            foreach (var s in _slots) { if (s) Destroy(s.gameObject); }
            foreach (var c in _cards) { if (c) Destroy(c.gameObject); }
            _slots.Clear();
            _cards.Clear();
        }

        // ── Generation Helpers ────────────────────────────────────────────────

        private List<(List<int> itemValues, int totalSum)> GenerateCountingDiceOrFingerData(int count, int minItems, int maxItems, int maxValPerItem)
        {
            var results = new List<(List<int>, int)>();
            var usedSums = new HashSet<int>();
            int maxTries = 1000;

            while (results.Count < count && maxTries-- > 0)
            {
                int itemCount = Random.Range(minItems, maxItems + 1);
                List<int> itemValues = new List<int>();
                int totalSum = 0;
                for (int d = 0; d < itemCount; d++)
                {
                    int val = Random.Range(1, maxValPerItem + 1);
                    itemValues.Add(val);
                    totalSum += val;
                }

                if (!usedSums.Contains(totalSum))
                {
                    results.Add((itemValues, totalSum));
                    usedSums.Add(totalSum);
                }
            }

            if (results.Count < count)
            {
                Debug.LogWarning($"[CountingGame] Could only generate {results.Count}/{count} unique sums. Consider widening minCount/maxCount.");
            }

            return results;
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
            var pool = Enumerable.Range(min, max - min + 1).ToList();
            Shuffle(pool);
            return pool.Take(count).ToList();
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
