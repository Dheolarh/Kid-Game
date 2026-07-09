using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Mechanics.Counting;

namespace KidGame.Mechanics.NumberRecall
{
    public class NumberRecallGameManager : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [Tooltip("The NumberRecallSlot sequence container prefab.")]
        [SerializeField] private GameObject slotPrefab;
        [Tooltip("The AnswerCard draggable card prefab.")]
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

        [Header("Sequence Config")]
        [Tooltip("Number of slots (sequences) to spawn per round.")]
        [SerializeField] private int slotCount = 4;
        [Tooltip("Minimum number of items in the sequence.")]
        [SerializeField] private int minSequenceLength = 5;
        [Tooltip("Maximum number of items in the sequence.")]
        [SerializeField] private int maxSequenceLength = 10;
        [Tooltip("The minimum starting number of the sequence.")]
        [SerializeField] private int minStartValue = 1;
        [Tooltip("The maximum starting number of the sequence.")]
        [SerializeField] private int maxStartValue = 20;
        [Tooltip("The step difference between consecutive numbers in the sequence.")]
        [SerializeField] private int step = 1;
        [Tooltip("If true, sequences count backwards instead of forwards.")]
        [SerializeField] private bool countBackwards;

        [Header("Interval Config")]
        [Tooltip("Minimum consecutive revealed numbers.")]
        [SerializeField] private int minConsecutiveRevealed = 1;
        [Tooltip("Maximum consecutive revealed numbers.")]
        [SerializeField] private int maxConsecutiveRevealed = 2;
        [Tooltip("Minimum consecutive hidden answers.")]
        [SerializeField] private int minConsecutiveHidden = 1;
        [Tooltip("Maximum consecutive hidden answers.")]
        [SerializeField] private int maxConsecutiveHidden = 2;

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

        private readonly List<NumberRecallSlot> _slots = new List<NumberRecallSlot>();
        private readonly List<AnswerCard>       _cards = new List<AnswerCard>();
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

        public void Configure(int slotCount, int minSequenceLength, int maxSequenceLength, int minStartValue, int maxStartValue, int step, bool countBackwards, int minConsecutiveRevealed, int maxConsecutiveRevealed, int minConsecutiveHidden, int maxConsecutiveHidden)
        {
            this.slotCount = slotCount;
            this.minSequenceLength = minSequenceLength;
            this.maxSequenceLength = maxSequenceLength;
            this.minStartValue = minStartValue;
            this.maxStartValue = maxStartValue;
            this.step = step;
            this.countBackwards = countBackwards;
            this.minConsecutiveRevealed = minConsecutiveRevealed;
            this.maxConsecutiveRevealed = maxConsecutiveRevealed;
            this.minConsecutiveHidden = minConsecutiveHidden;
            this.maxConsecutiveHidden = maxConsecutiveHidden;

            _slots.Clear();
            _cards.Clear();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Configure slots containers to not force expand height and control children height
            ConfigureContainerLayout(portraitSlotsContainer);
            ConfigureContainerLayout(landscapeSlotsContainer);

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

        private void ConfigureContainerLayout(Transform container)
        {
            if (container == null) return;
            var vg = container.GetComponent<VerticalLayoutGroup>();
            if (vg != null)
            {
                vg.childForceExpandHeight = false;
                vg.childControlHeight = true;
            }
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
            {
                if (slot)
                {
                    slot.transform.SetParent(newSlots, worldPositionStays: false);
                    slot.RecalculateHeight();
                }
            }

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
 
            UpdateScrollLocking();
        }

        // ── Round Generation ──────────────────────────────────────────────────

        public void GenerateRound()
        {
            // ── Validate Inspector References ────────────────────────────────
            if (slotPrefab == null)
            {
                Debug.LogError("[NumberRecall] Slot Prefab is not assigned in the Inspector.");
                return;
            }
            if (answerCardPrefab == null)
            {
                Debug.LogError("[NumberRecall] Answer Card Prefab is not assigned in the Inspector.");
                return;
            }
            if (ActiveSlotsContainer == null || ActiveAnswersContainer == null)
            {
                Debug.LogError("[NumberRecall] Portrait or Landscape containers are missing.");
                return;
            }
            // ─────────────────────────────────────────────────────────────────

            ClearPrevious();
            _answeredCount = 0;
            SetNextButtonsInteractable(false);

            var trayValues = new List<int>();

            // Spawn configured number of slots
            for (int sIdx = 0; sIdx < slotCount; sIdx++)
            {
                // Choose a random sequence length for this slot
                int sequenceLength = Random.Range(minSequenceLength, maxSequenceLength + 1);

                // 1. Choose a random starting number for this sequence
                int startValue;
                int actualStep = countBackwards ? -step : step;
                if (countBackwards)
                {
                    int minStartVal = minStartValue + (sequenceLength - 1) * step;
                    int maxStartVal = Mathf.Max(minStartVal, maxStartValue);
                    startValue = Random.Range(minStartVal, maxStartVal + 1);
                }
                else
                {
                    startValue = Random.Range(minStartValue, maxStartValue + 1);
                }

                // 2. Determine which indices to hide based on interval config
                var hiddenIndices = new List<int>();
                int minRev = Mathf.Max(1, minConsecutiveRevealed);
                int maxRev = Mathf.Max(minRev, maxConsecutiveRevealed);
                int minHid = Mathf.Max(1, minConsecutiveHidden);
                int maxHid = Mathf.Max(minHid, maxConsecutiveHidden);

                int currentIdx = 0;
                bool currentlyHidden = false; // Start with revealed numbers
                while (currentIdx < sequenceLength)
                {
                    if (currentlyHidden)
                    {
                        int count = Random.Range(minHid, maxHid + 1);
                        for (int i = 0; i < count && currentIdx < sequenceLength; i++)
                        {
                            hiddenIndices.Add(currentIdx);
                            currentIdx++;
                        }
                        currentlyHidden = false;
                    }
                    else
                    {
                        int count = Random.Range(minRev, maxRev + 1);
                        currentIdx += count;
                        currentlyHidden = true;
                    }
                }

                // 3. Spawns the sequence container slot
                var slotGo = Instantiate(slotPrefab, ActiveSlotsContainer);
                var slot = slotGo.GetComponent<NumberRecallSlot>();
                if (slot == null) slot = slotGo.AddComponent<NumberRecallSlot>();

                slot.Setup(startValue, sequenceLength, actualStep, hiddenIndices, Palette, OnSequenceCompleted);
                _slots.Add(slot);

                // Collect missing values for answer tray
                for (int i = 0; i < sequenceLength; i++)
                {
                    if (hiddenIndices.Contains(i))
                    {
                        trayValues.Add(startValue + i * actualStep);
                    }
                }
            }

            // 4. Generate answer tray cards for all gathered hidden values
            // Shuffle color palette to match count of answer cards
            var colors = new List<Color>();
            for (int i = 0; i < trayValues.Count; i++)
            {
                colors.Add(Palette[i % Palette.Length]);
            }
            Shuffle(colors);

            // Shuffles tray values so they aren't ordered in the tray
            var shuffledIndices = Enumerable.Range(0, trayValues.Count).ToList();
            Shuffle(shuffledIndices);

            for (int i = 0; i < trayValues.Count; i++)
            {
                int valIdx = shuffledIndices[i];
                var cardGo = Instantiate(answerCardPrefab, ActiveAnswersContainer);
                var card = cardGo.GetComponent<AnswerCard>();
                card.Setup(trayValues[valIdx], colors[i]);
                _cards.Add(card);
            }

            // Forces layout calculations immediate
            var rtSlots = ActiveSlotsContainer.GetComponent<RectTransform>();
            if (rtSlots != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rtSlots);
            var rtAnswers = ActiveAnswersContainer.GetComponent<RectTransform>();
            if (rtAnswers != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rtAnswers);
 
            UpdateScrollLocking();
        }

        private void OnSequenceCompleted()
        {
            _answeredCount++;
            if (_answeredCount >= _slots.Count)
            {
                SetNextButtonsInteractable(true);
            }
        }

        private void ClearPrevious()
        {
            foreach (var s in _slots) { if (s) Destroy(s.gameObject); }
            foreach (var c in _cards) { if (c) Destroy(c.gameObject); }
            _slots.Clear();
            _cards.Clear();
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

        private void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
