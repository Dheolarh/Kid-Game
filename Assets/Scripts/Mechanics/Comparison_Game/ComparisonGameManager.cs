using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Mechanics.Comparison
{
    public class ComparisonGameManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private GameObject numberBoxPrefab;
        [SerializeField] private GameObject plusSymbolPrefab;
        [SerializeField] private GameObject comparisonDropZonePrefab;
        [SerializeField] private GameObject answerCardPrefab;

        [Header("Portrait Containers")]
        [SerializeField] private Transform portraitSlotsContainer;
        [SerializeField] private Transform portraitAnswersContainer;

        [Header("Landscape Containers")]
        [SerializeField] private Transform landscapeSlotsContainer;
        [SerializeField] private Transform landscapeAnswersContainer;

        [Header("Shared UI")]
        [SerializeField] private Button portraitNextButton;
        [SerializeField] private Button landscapeNextButton;

        [Header("Config")]
        [SerializeField] private int slotCount = 3;
        [SerializeField] private int minVal = 1;
        [SerializeField] private int maxVal = 10;
        [SerializeField] private bool mixAdditionEquations = true;

        [Header("Tray Colors")]
        [SerializeField] private Color lessThanColor = new Color(0.20f, 0.60f, 0.86f);   // blue
        [SerializeField] private Color equalToColor = new Color(0.15f, 0.68f, 0.38f);    // green
        [SerializeField] private Color greaterThanColor = new Color(0.95f, 0.61f, 0.07f); // orange

        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.30f, 0.24f),   // Red
            new Color(0.20f, 0.60f, 0.86f),   // Blue
            new Color(0.95f, 0.61f, 0.07f),   // Orange
            new Color(0.15f, 0.68f, 0.38f),   // Green
            new Color(0.61f, 0.35f, 0.71f),   // Purple
            new Color(0.10f, 0.74f, 0.61f),   // Teal
            new Color(0.90f, 0.49f, 0.13f),   // Pumpkin
            new Color(0.17f, 0.24f, 0.31f),   // Wet Asphalt
            new Color(0.52f, 0.73f, 0.40f),   // Light Green
            new Color(0.82f, 0.40f, 0.82f),   // Pinky Purple
            new Color(0.92f, 0.30f, 0.50f),   // Rose
            new Color(0.35f, 0.70f, 0.90f)    // Sky Blue
        };

        private readonly List<ComparisonSlot> _slots = new List<ComparisonSlot>();
        private readonly List<ComparisonCard> _cards = new List<ComparisonCard>();
        private List<Color> _shuffledColors = new List<Color>();
        private int _answeredCount;
        private bool _wasLandscape;

        private bool IsLandscape => Screen.width > Screen.height;

        private Transform ActiveSlotsContainer
            => IsLandscape ? landscapeSlotsContainer : portraitSlotsContainer;

        private Transform ActiveAnswersContainer
            => IsLandscape ? landscapeAnswersContainer : portraitAnswersContainer;

        private void Start()
        {
            ConfigureNextButton(portraitNextButton);
            ConfigureNextButton(landscapeNextButton);

            _wasLandscape = IsLandscape;
            SetNextButtonsInteractable(false);

            if (portraitNextButton != null) portraitNextButton.onClick.AddListener(GenerateRound);
            if (landscapeNextButton != null) landscapeNextButton.onClick.AddListener(GenerateRound);

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
            var newSlots = ActiveSlotsContainer;
            var newAnswers = ActiveAnswersContainer;
            if (newSlots == null || newAnswers == null) return;

            // Migrate active slot rows
            foreach (var slot in _slots)
            {
                if (slot)
                {
                    slot.transform.SetParent(newSlots, worldPositionStays: false);
                }
            }

            // Migrate stationary cards in the old tray to the new tray
            Transform oldAnswers = IsLandscape ? portraitAnswersContainer : landscapeAnswersContainer;
            if (oldAnswers != null)
            {
                var cardsInOldTray = oldAnswers.GetComponentsInChildren<ComparisonCard>(true);
                foreach (var card in cardsInOldTray)
                {
                    if (card && !card.IsAccepted)
                    {
                        card.transform.SetParent(newAnswers, worldPositionStays: false);
                        card.UpdateHomeParent(newAnswers);
                    }
                }
            }

            var rtSlots = newSlots.GetComponent<RectTransform>();
            var rtAnswers = newAnswers.GetComponent<RectTransform>();
            if (rtSlots) LayoutRebuilder.ForceRebuildLayoutImmediate(rtSlots);
            if (rtAnswers) LayoutRebuilder.ForceRebuildLayoutImmediate(rtAnswers);

            Debug.Log($"[ComparisonGame] Orientation changed → moved {_slots.Count} slots.");
        }

        public void OnSlotAnswered()
        {
            _answeredCount++;
            if (_answeredCount >= _slots.Count)
            {
                SetNextButtonsInteractable(true);
            }
        }

        public Color GetColorForIndex(int idx)
        {
            if (_shuffledColors == null || _shuffledColors.Count == 0)
            {
                return Palette[idx % Palette.Length];
            }
            return _shuffledColors[idx % _shuffledColors.Count];
        }

        public void GenerateRound()
        {
            // Validate required fields
            if (slotPrefab == null || numberBoxPrefab == null || plusSymbolPrefab == null ||
                comparisonDropZonePrefab == null || answerCardPrefab == null)
            {
                Debug.LogError("[ComparisonGame] One or more prefabs are not assigned in the Inspector.");
                return;
            }

            if (ActiveSlotsContainer == null || ActiveAnswersContainer == null)
            {
                Debug.LogError("[ComparisonGame] Active slot or active answers container is not assigned.");
                return;
            }

            ClearPrevious();
            _answeredCount = 0;
            SetNextButtonsInteractable(false);

            // Prepare shuffled colors for operand boxes
            _shuffledColors = new List<Color>(Palette);
            Shuffle(_shuffledColors);

            // Generate Slot Rows
            HashSet<string> generatedKeys = new HashSet<string>();
            int maxTries = 200;

            for (int i = 0; i < slotCount; i++)
            {
                List<int> left = null;
                List<int> right = null;
                string key = "";
                int tries = 0;

                do
                {
                    GenerateComparisonPair(out left, out right);
                    key = string.Join("+", left) + "_" + string.Join("+", right);
                    tries++;
                } while (generatedKeys.Contains(key) && tries < maxTries);

                generatedKeys.Add(key);

                var slotGo = Instantiate(slotPrefab, ActiveSlotsContainer);
                var slot = slotGo.GetComponent<ComparisonSlot>();
                if (slot == null) slot = slotGo.AddComponent<ComparisonSlot>();

                slot.Setup(left, right, this, numberBoxPrefab, plusSymbolPrefab, comparisonDropZonePrefab);
                _slots.Add(slot);
            }

            // Spawn the three permanent options in the tray
            SpawnAnswerCard(ComparisonSign.LessThan, lessThanColor);
            SpawnAnswerCard(ComparisonSign.EqualTo, equalToColor);
            SpawnAnswerCard(ComparisonSign.GreaterThan, greaterThanColor);

            // Rebuild layouts
            var rtSlots = ActiveSlotsContainer.GetComponent<RectTransform>();
            if (rtSlots != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rtSlots);

            var rtAnswers = ActiveAnswersContainer.GetComponent<RectTransform>();
            if (rtAnswers != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rtAnswers);
        }

        private void SpawnAnswerCard(ComparisonSign sign, Color color)
        {
            var cardGo = Instantiate(answerCardPrefab, ActiveAnswersContainer);
            var card = cardGo.GetComponent<ComparisonCard>();
            if (card == null) card = cardGo.AddComponent<ComparisonCard>();

            card.Setup(sign, color, isClone: false);
            _cards.Add(card);
        }

        private void GenerateComparisonPair(out List<int> leftNumbers, out List<int> rightNumbers)
        {
            leftNumbers = new List<int>();
            rightNumbers = new List<int>();

            bool useAdditionLeft = false;
            bool useAdditionRight = false;

            if (mixAdditionEquations)
            {
                int mode = Random.Range(0, 4);
                if (mode == 0) useAdditionLeft = true;
                else if (mode == 1) useAdditionRight = true;
                else if (mode == 2) { useAdditionLeft = true; useAdditionRight = true; }
            }

            // Decide relationship type: LessThan (0), EqualTo (1), GreaterThan (2) with equal probability
            int relation = Random.Range(0, 3);

            if (relation == 1) // Force EqualTo
            {
                int minSum = minVal;
                int maxSum = maxVal;

                if (useAdditionLeft && useAdditionRight)
                {
                    minSum = minVal * 2;
                    maxSum = maxVal * 2;
                }
                else if (useAdditionLeft || useAdditionRight)
                {
                    minSum = minVal * 2;
                    maxSum = maxVal;
                }

                // Safeguard against invalid ranges where addition cannot be split within a single value constraint
                if (minSum > maxSum)
                {
                    minSum = minVal;
                    maxSum = maxVal;
                    useAdditionLeft = false;
                    useAdditionRight = false;
                }

                int targetSum = Random.Range(minSum, maxSum + 1);

                // Populate left
                if (useAdditionLeft)
                {
                    int a = Random.Range(minVal, targetSum - minVal + 1);
                    int b = targetSum - a;
                    leftNumbers.Add(a);
                    leftNumbers.Add(b);
                }
                else
                {
                    leftNumbers.Add(targetSum);
                }

                // Populate right
                if (useAdditionRight)
                {
                    int c = Random.Range(minVal, targetSum - minVal + 1);
                    int d = targetSum - c;
                    rightNumbers.Add(c);
                    rightNumbers.Add(d);
                }
                else
                {
                    rightNumbers.Add(targetSum);
                }
            }
            else // Generate LessThan (0) or GreaterThan (2)
            {
                int tries = 0;
                do
                {
                    leftNumbers.Clear();
                    rightNumbers.Clear();

                    if (useAdditionLeft)
                    {
                        leftNumbers.Add(Random.Range(minVal, maxVal + 1));
                        leftNumbers.Add(Random.Range(minVal, maxVal + 1));
                    }
                    else
                    {
                        leftNumbers.Add(Random.Range(minVal, maxVal + 1));
                    }

                    if (useAdditionRight)
                    {
                        rightNumbers.Add(Random.Range(minVal, maxVal + 1));
                        rightNumbers.Add(Random.Range(minVal, maxVal + 1));
                    }
                    else
                    {
                        rightNumbers.Add(Random.Range(minVal, maxVal + 1));
                    }

                    int leftSum = GetSum(leftNumbers);
                    int rightSum = GetSum(rightNumbers);

                    if (relation == 0 && leftSum < rightSum) break;
                    if (relation == 2 && leftSum > rightSum) break;

                    tries++;
                } while (tries < 100);
            }
        }

        private int GetSum(List<int> list)
        {
            int sum = 0;
            foreach (int n in list) sum += n;
            return sum;
        }

        private void ClearPrevious()
        {
            foreach (var s in _slots) { if (s) Destroy(s.gameObject); }
            foreach (var c in _cards) { if (c) Destroy(c.gameObject); }
            _slots.Clear();
            _cards.Clear();

            // Also clear any runtime cloned cards in the active and inactive tray containers
            ClearTrayChildren(portraitAnswersContainer);
            ClearTrayChildren(landscapeAnswersContainer);
        }

        private void ClearTrayChildren(Transform tray)
        {
            if (tray == null) return;
            foreach (Transform child in tray)
            {
                Destroy(child.gameObject);
            }
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
