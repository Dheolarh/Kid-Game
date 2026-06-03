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

        [Header("Scene References")]
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private Transform answersContainer;
        [SerializeField] private Button    nextButton;

        [Header("Config")]
        [Tooltip("Number of slot rows per round (always 5 per design).")]
        [SerializeField] private int slotCount = 5;
        [SerializeField, Range(1, 12)] private int minCount = 1;
        [SerializeField, Range(1, 12)] private int maxCount = 12;

        // ── Built-in palette (6 high-contrast, non-white colors) ─────────────

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

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Start()
        {
            // Prevent Unity's default Button behaviour of fading to 50% alpha
            // when interactable = false. Keep the same color but slightly dimmed.
            var colors          = nextButton.colors;
            var disabled        = colors.normalColor;
            disabled.a          = 0.6f;          // slight dim to signal "locked", but no transparency
            colors.disabledColor = disabled;
            nextButton.colors   = colors;

            nextButton.interactable = false;
            nextButton.onClick.AddListener(GenerateRound);
            GenerateRound();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by AnswerDropZone when a slot is correctly answered.</summary>
        public void OnSlotAnswered(CountingSlot slot)
        {
            _answeredCount++;
            if (_answeredCount >= slotCount)
                nextButton.interactable = true;
        }

        // ── Round Management ──────────────────────────────────────────────────

        public void GenerateRound()
        {
            ClearPrevious();
            _answeredCount = 0;
            nextButton.interactable = false;

            // 1. Five unique random counts
            var counts = UniqueRandomList(slotCount, minCount, maxCount);

            // 2. Shuffle category order (different category per slot)
            var catOrder = Enumerable.Range(0, objectCategoryPrefabs.Length).ToList();
            Shuffle(catOrder);

            // 3. Shuffle color palette (one color per card)
            var colors = Palette.Take(slotCount).ToList();
            Shuffle(colors);

            // 4. Shuffle answer values — exactly 5, one per slot
            var answerValues = new List<int>(counts);
            Shuffle(answerValues);

            // 5. Spawn slots — each gets a DIFFERENT object category
            for (int i = 0; i < slotCount; i++)
            {
                var go   = Instantiate(slotPrefab, slotsContainer);
                var slot = go.GetComponent<CountingSlot>();
                var cat  = objectCategoryPrefabs[catOrder[i % catOrder.Count]];
                slot.Setup(cat, counts[i], this);
                _slots.Add(slot);
            }

            // 6. Spawn answer cards
            for (int i = 0; i < answerValues.Count; i++)
            {
                var go   = Instantiate(answerCardPrefab, answersContainer);
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
