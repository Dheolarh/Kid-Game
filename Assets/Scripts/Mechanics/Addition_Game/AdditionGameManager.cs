using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Mechanics.Counting;

namespace KidGame.Mechanics.Addition
{
    public class AdditionGameManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [Tooltip("Object icon prefabs — need at least 2 (left/right use different ones per slot).")]
        [SerializeField] private GameObject[] objectCategoryPrefabs;
        [Tooltip("The AdditionSlot row prefab.")]
        [SerializeField] private GameObject slotPrefab;
        [Tooltip("The AnswerCard draggable prefab (same as counting game).")]
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
        [Tooltip("Number of slot rows per round.")]
        [SerializeField] private int slotCount = 5;
        [Tooltip("Minimum number of objects per grid side.")]
        [SerializeField, Range(1, 12)] private int minPerGrid = 1;
        [Tooltip("Maximum number of objects per grid side.")]
        [SerializeField, Range(1, 12)] private int maxPerGrid = 12;

        // ── Built-in palette (same 6 high-contrast colors as counting game) ──

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

        private readonly List<AdditionSlot> _slots = new List<AdditionSlot>();
        private readonly List<AnswerCard>   _cards = new List<AnswerCard>();
        private int _answeredCount;

        // ── Orientation ───────────────────────────────────────────────────────

        private bool IsLandscape => Screen.width > Screen.height;

        private Transform ActiveSlotsContainer
            => IsLandscape ? landscapeSlotsContainer   : portraitSlotsContainer;

        private Transform ActiveAnswersContainer
            => IsLandscape ? landscapeAnswersContainer : portraitAnswersContainer;

        private bool _wasLandscape;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            // Prevent Unity's default 50%-alpha fade on disabled buttons
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
            var newSlots   = ActiveSlotsContainer;
            var newAnswers = ActiveAnswersContainer;

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
        }

        // ── Public API (called by AdditionSlot via lambda) ────────────────────

        /// <summary>Called once per correctly-answered slot.</summary>
        public void OnSlotAnswered()
        {
            _answeredCount++;
            if (_answeredCount >= slotCount)
                nextButton.interactable = true;
        }

        // ── Round Management ──────────────────────────────────────────────────

        public void GenerateRound()
        {
            // ── Validate required Inspector references ────────────────────────
            if (objectCategoryPrefabs == null || objectCategoryPrefabs.Length < 2)
            {
                Debug.LogError("[AdditionGame] Assign at least 2 Object Category Prefabs in the Inspector.");
                return;
            }
            if (slotPrefab == null)
            {
                Debug.LogError("[AdditionGame] Slot Prefab is not assigned in the Inspector.");
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
            nextButton.interactable = false;

            // 1. Generate pairs with UNIQUE sums (e.g. 3+5=8, 6+4=10, not two slots summing to 8)
            var pairs = GenerateUniqueSumPairs(slotCount, minPerGrid, maxPerGrid);

            // 2. Collect the sums for answer cards
            var sums = pairs.Select(p => p.left + p.right).ToList();

            // 3. Shuffle color palette
            var colors = Palette.Take(slotCount).ToList();
            Shuffle(colors);

            // 4. Shuffle answer values
            var answerValues = new List<int>(sums);
            Shuffle(answerValues);

            // 5. Pick ONE category for all left grids and ONE (different) for all right grids
            var catOrder    = Enumerable.Range(0, objectCategoryPrefabs.Length).ToList();
            Shuffle(catOrder);
            var leftPrefab  = objectCategoryPrefabs[catOrder[0]];
            var rightPrefab = objectCategoryPrefabs[catOrder.Count > 1 ? catOrder[1] : catOrder[0]];

            // Debug: shows what was actually picked — remove once confirmed working
            Debug.Log($"[AdditionGame] Round categories → Left: {leftPrefab?.name}, Right: {rightPrefab?.name}" +
                      $" (pool size: {objectCategoryPrefabs.Length})");

            // 6. Spawn addition slots — all use the same left/right category pair
            for (int i = 0; i < slotCount; i++)
            {
                var go   = Instantiate(slotPrefab, ActiveSlotsContainer);
                var slot = go.GetComponent<AdditionSlot>();

                slot.Setup(leftPrefab,  pairs[i].left,
                           rightPrefab, pairs[i].right,
                           this);

                _slots.Add(slot);
            }

            // Force layout to recalculate immediately after spawning all slots
            var rt = ActiveSlotsContainer.GetComponent<RectTransform>();
            if (rt != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            // 7. Spawn answer cards into the active orientation's answer grid
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

        /// <summary>
        /// Generates <paramref name="count"/> (left, right) pairs whose sums are all unique.
        /// left and right are each in [min, max].
        /// </summary>
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
