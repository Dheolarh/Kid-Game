using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KidGame.Mechanics.Matching
{
    public enum MatchVariant
    {
        Number,
        Word,
        Dice,
        Finger
    }

    public class MatchGameManager : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Containers")]
        [Tooltip("The Content container inside the Portrait ScrollRect.")]
        [SerializeField] private RectTransform portraitContent;
        [Tooltip("The Content container inside the Landscape ScrollRect.")]
        [SerializeField] private RectTransform landscapeContent;

        [Header("Next Buttons")]
        [SerializeField] private Button portraitNextButton;
        [SerializeField] private Button landscapeNextButton;

        [Header("Prefabs & Templates")]
        [Tooltip("The horizontal Slot row prefab containing [left] [space] [right] children.")]
        [SerializeField] private GameObject slotRowPrefab;
        [Tooltip("Prefab for Number Card items.")]
        [SerializeField] private GameObject baseNumberPrefab;
        [Tooltip("Prefab for Word Card items.")]
        [SerializeField] private GameObject baseWordPrefab;
        [Tooltip("Dice prefabs for values 1 to 6 (index 0 = 1 dot).")]
        [SerializeField] private GameObject[] dicePrefabs;
        [Tooltip("Finger prefabs for values 1 to 5 (index 0 = 1 finger).")]
        [SerializeField] private GameObject[] fingerPrefabs;

        [Header("Game Mode Configuration")]
        [SerializeField] private MatchVariant leftVariant = MatchVariant.Number;
        [SerializeField] private MatchVariant rightVariant = MatchVariant.Word;
        [Tooltip("Number of slots (matching pairs) to spawn per round.")]
        [SerializeField, Range(3, 10)] private int slotCount = 5;
        [SerializeField] private int minVal = 1;
        [SerializeField] private int maxVal = 10;
        [Tooltip("If true, the left column items will also be shuffled. Otherwise, they appear in ascending order.")]
        [SerializeField] private bool shuffleLeftColumn = false;

        [Header("Line Settings")]
        [SerializeField] private GameObject linePrefab;
        [SerializeField] private float lineThickness = 8f;
        [SerializeField] private Color connectionLineColor = Color.black;
        [SerializeField] private Color matchedOutlineColor = Color.green;

        private static readonly string[] NumberWords =
        {
            "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty"
        };

        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.30f, 0.24f),   // red
            new Color(0.20f, 0.60f, 0.86f),   // blue
            new Color(0.95f, 0.61f, 0.07f),   // orange
            new Color(0.15f, 0.68f, 0.38f),   // green
            new Color(0.61f, 0.35f, 0.71f),   // purple
            new Color(0.10f, 0.74f, 0.61f),   // teal
        };

        // ── Runtime State ────────────────────────────────────────────────────

        private readonly List<GameObject> _slots = new List<GameObject>();
        private readonly List<MatchGameCard> _allCards = new List<MatchGameCard>();

        private class Connection
        {
            public MatchGameCard leftCard;
            public MatchGameCard rightCard;
            public MatchGameLine line;
        }

        private readonly List<Connection> _connections = new List<Connection>();
        private MatchGameCard _selectedCard;
        private MatchGameLine _dragLine;
        private MatchGameCard _dragStartCard;
        private readonly Dictionary<int, Color> _leftColors = new Dictionary<int, Color>();
        private readonly Dictionary<int, Color> _rightColors = new Dictionary<int, Color>();

        // ── Orientation ───────────────────────────────────────────────────────

        private bool IsLandscape => Screen.width > Screen.height;

        private RectTransform ActiveContent
            => IsLandscape ? landscapeContent : portraitContent;

        private bool _wasLandscape;

        // ── Lifecycle ─────────────────────────────────────────────────────────

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
            if (portraitContent == null || landscapeContent == null) return;

            bool landscape = IsLandscape;
            if (landscape != _wasLandscape && _slots.Count > 0)
            {
                _wasLandscape = landscape;
                MoveToActiveContainers();
            }
        }

        private void LateUpdate()
        {
            if (_slots.Count == 0) return;
            UpdateLinePositions();
        }

        private void MoveToActiveContainers()
        {
            if (this == null) return;
            var newContent = ActiveContent;
            if (newContent == null) return;

            // Move slots
            foreach (var slot in _slots)
            {
                if (slot) slot.transform.SetParent(newContent, worldPositionStays: false);
            }

            // Move permanent lines and send to back
            foreach (var conn in _connections)
            {
                if (conn.line)
                {
                    conn.line.transform.SetParent(newContent, worldPositionStays: false);
                    conn.line.transform.SetAsFirstSibling();
                }
            }

            // Rebuild layout
            var rt = newContent.GetComponent<RectTransform>();
            if (rt) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            UpdateLinePositions();
            UpdateScrollLocking();
        }

        // ── Round Management ──────────────────────────────────────────────────

        public void GenerateRound()
        {
            // Validate Inspector References
            if (slotRowPrefab == null)
            {
                Debug.LogError("[MatchGame] Slot Row Prefab is not assigned.");
                return;
            }
            if (linePrefab == null)
            {
                Debug.LogError("[MatchGame] Line Prefab is not assigned.");
                return;
            }
            if (portraitContent == null || landscapeContent == null)
            {
                Debug.LogError("[MatchGame] Portrait/Landscape Content containers are missing.");
                return;
            }
            if (leftVariant == MatchVariant.Dice || rightVariant == MatchVariant.Dice)
            {
                if (dicePrefabs == null || dicePrefabs.Length < 6)
                {
                    Debug.LogError("[MatchGame] Dice prefabs array must have at least 6 items.");
                    return;
                }
            }
            if (leftVariant == MatchVariant.Finger || rightVariant == MatchVariant.Finger)
            {
                if (fingerPrefabs == null || fingerPrefabs.Length < 5)
                {
                    Debug.LogError("[MatchGame] Finger prefabs array must have at least 5 items.");
                    return;
                }
            }

            ClearPrevious();
            SetNextButtonsInteractable(false);

            // Determine upper limit bounds based on selected variants (Dice max 6, Finger max 5)
            int maxAllowedValue = maxVal;
            if (leftVariant == MatchVariant.Dice || rightVariant == MatchVariant.Dice)
            {
                maxAllowedValue = Mathf.Min(maxAllowedValue, 6);
            }
            if (leftVariant == MatchVariant.Finger || rightVariant == MatchVariant.Finger)
            {
                maxAllowedValue = Mathf.Min(maxAllowedValue, 5);
            }
            int minAllowedValue = Mathf.Clamp(minVal, 1, maxAllowedValue);

            // Generate unique numbers
            int count = Mathf.Min(slotCount, (maxAllowedValue - minAllowedValue + 1));
            List<int> matchIds = new List<int>();
            var pool = Enumerable.Range(minAllowedValue, maxAllowedValue - minAllowedValue + 1).ToList();
            Shuffle(pool);
            for (int i = 0; i < count; i++)
            {
                matchIds.Add(pool[i]);
            }

            // Assign unique colors to each value for this round, ensuring left and right columns
            // do not share the same color for matching pairs.
            _leftColors.Clear();
            _rightColors.Clear();
            List<Color> baseColors = new List<Color>(Palette);
            Shuffle(baseColors);
            
            // Shift offset between 1 and Palette.Length - 1 guarantees different colors
            int shift = Random.Range(1, baseColors.Count);
            
            for (int i = 0; i < matchIds.Count; i++)
            {
                int val = matchIds[i];
                _leftColors[val] = baseColors[i % baseColors.Count];
                _rightColors[val] = baseColors[(i + shift) % baseColors.Count];
            }

            // Shuffle left and right values separately
            List<int> leftValues = new List<int>(matchIds);
            if (shuffleLeftColumn)
            {
                Shuffle(leftValues);
            }
            else
            {
                leftValues.Sort();
            }

            List<int> rightValues = new List<int>(matchIds);
            Shuffle(rightValues);

            // Spawn rows
            for (int i = 0; i < count; i++)
            {
                var rowGo = Instantiate(slotRowPrefab, ActiveContent);
                rowGo.name = $"SlotRow_{i}";

                Transform leftAnchor = rowGo.transform.Find("left");
                Transform rightAnchor = rowGo.transform.Find("right");

                if (leftAnchor == null || rightAnchor == null)
                {
                    Debug.LogError("[MatchGame] Slot prefab is missing children named 'left' or 'right'.");
                    Destroy(rowGo);
                    continue;
                }

                // Clear design-time placeholder editor templates from the anchors
                ClearChildren(leftAnchor);
                ClearChildren(rightAnchor);

                // Spawn Left Item
                int leftVal = leftValues[i];
                GameObject leftItem = SpawnItem(leftAnchor, leftVariant, leftVal, isLeft: true);
                if (leftItem != null)
                {
                    var card = leftItem.GetComponent<MatchGameCard>();
                    if (card == null) card = leftItem.AddComponent<MatchGameCard>();
                    card.Setup(leftVal, true, this);
                    _allCards.Add(card);
                }

                // Spawn Right Item
                int rightVal = rightValues[i];
                GameObject rightItem = SpawnItem(rightAnchor, rightVariant, rightVal, isLeft: false);
                if (rightItem != null)
                {
                    var card = rightItem.GetComponent<MatchGameCard>();
                    if (card == null) card = rightItem.AddComponent<MatchGameCard>();
                    card.Setup(rightVal, false, this);
                    _allCards.Add(card);
                }

                _slots.Add(rowGo);
            }

            // Rebuild active layout
            var rt = ActiveContent.GetComponent<RectTransform>();
            if (rt) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            UpdateScrollLocking();
        }

        private GameObject SpawnItem(Transform parent, MatchVariant variant, int value, bool isLeft)
        {
            GameObject instantiated = null;
            switch (variant)
            {
                case MatchVariant.Number:
                    if (baseNumberPrefab != null)
                    {
                        instantiated = Instantiate(baseNumberPrefab, parent);
                        var textComp = instantiated.GetComponentInChildren<TMPro.TMP_Text>();
                        if (textComp != null) textComp.text = value.ToString();

                        var img = instantiated.GetComponent<Image>();
                        if (img != null)
                        {
                            img.color = isLeft ? GetLeftColorForValue(value) : GetRightColorForValue(value);
                        }
                    }
                    break;

                case MatchVariant.Word:
                    if (baseWordPrefab != null)
                    {
                        instantiated = Instantiate(baseWordPrefab, parent);
                        var textComp = instantiated.GetComponentInChildren<TMPro.TMP_Text>();
                        if (textComp != null)
                        {
                            if (value >= 0 && value < NumberWords.Length)
                                textComp.text = NumberWords[value];
                            else
                                textComp.text = value.ToString();
                        }

                        var img = instantiated.GetComponent<Image>();
                        if (img != null)
                        {
                            img.color = isLeft ? GetLeftColorForValue(value) : GetRightColorForValue(value);
                        }
                    }
                    break;

                case MatchVariant.Dice:
                    int diceIdx = Mathf.Clamp(value - 1, 0, dicePrefabs.Length - 1);
                    if (dicePrefabs[diceIdx] != null)
                    {
                        instantiated = Instantiate(dicePrefabs[diceIdx], parent);
                    }
                    break;

                case MatchVariant.Finger:
                    int fingerIdx = Mathf.Clamp(value - 1, 0, fingerPrefabs.Length - 1);
                    if (fingerPrefabs[fingerIdx] != null)
                    {
                        // Wrap the finger hand drawing inside the baseNumberPrefab card container to provide
                        // a solid card background. This ensures visual consistency and prevents the Outline
                        // component from duplicate-rendering the thin hand stroke lines (eliminating the double-stroke effect).
                        if (baseNumberPrefab != null)
                        {
                            instantiated = Instantiate(baseNumberPrefab, parent);
                            
                            var textComp = instantiated.GetComponentInChildren<TMPro.TMP_Text>();
                            if (textComp != null) textComp.gameObject.SetActive(false);

                            var img = instantiated.GetComponent<Image>();
                            if (img != null) img.color = Color.white;

                            GameObject handGo = Instantiate(fingerPrefabs[fingerIdx], instantiated.transform);
                            var handImg = handGo.GetComponent<Image>();
                            if (handImg != null) handImg.raycastTarget = false;

                            var handRt = handGo.GetComponent<RectTransform>();
                            if (handRt != null)
                            {
                                handRt.anchoredPosition = Vector2.zero;
                                handRt.localPosition = Vector3.zero;
                                handRt.localScale = Vector3.one;
                                // Center and stretch with padding inside the card background
                                handRt.anchorMin = new Vector2(0.15f, 0.15f);
                                handRt.anchorMax = new Vector2(0.85f, 0.85f);
                                handRt.offsetMin = Vector2.zero;
                                handRt.offsetMax = Vector2.zero;
                            }
                        }
                        else
                        {
                            instantiated = Instantiate(fingerPrefabs[fingerIdx], parent);
                        }
                    }
                    break;
            }
            if (instantiated != null)
            {
                var rt = instantiated.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.localPosition = Vector3.zero;
                    rt.localScale = Vector3.one;
                }
            }
            return instantiated;
        }

        private void ClearPrevious()
        {
            foreach (var s in _slots) { if (s) Destroy(s.gameObject); }
            foreach (var conn in _connections) { if (conn.line) Destroy(conn.line.gameObject); }

            _slots.Clear();
            _connections.Clear();
            _allCards.Clear();
            _selectedCard = null;
            _leftColors.Clear();
            _rightColors.Clear();

            // Clear any editor design-time static slots from the content containers
            if (portraitContent != null)
            {
                foreach (Transform child in portraitContent)
                {
                    Destroy(child.gameObject);
                }
            }
            if (landscapeContent != null)
            {
                foreach (Transform child in landscapeContent)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void ClearChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                Destroy(child.gameObject);
            }
        }

        // ── Interaction Callbacks (called by MatchGameCard) ──────────────────

        public void OnCardSelected(MatchGameCard card)
        {
            if (card.IsMatched) return;

            Debug.Log($"[MatchGame] Card Clicked: Name={card.gameObject.name}, MatchId={card.MatchId}, IsLeft={card.IsLeftCard}");

            if (_selectedCard == null)
            {
                _selectedCard = card;
                _selectedCard.SetSelected(true);
                Debug.Log($"[MatchGame] Selected first card: MatchId={card.MatchId}");
            }
            else if (_selectedCard == card)
            {
                _selectedCard.SetSelected(false);
                _selectedCard = null;
                Debug.Log("[MatchGame] Deselected card");
            }
            else if (_selectedCard.IsLeftCard == card.IsLeftCard)
            {
                // Switch selection on the same side
                _selectedCard.SetSelected(false);
                _selectedCard = card;
                _selectedCard.SetSelected(true);
                Debug.Log($"[MatchGame] Switched selection on same side to MatchId={card.MatchId}");
            }
            else
            {
                // Tapped opposites!
                Debug.Log($"[MatchGame] Comparing: Selected Card MatchId={_selectedCard.MatchId} (Left={_selectedCard.IsLeftCard}) with Tapped Card MatchId={card.MatchId} (Left={card.IsLeftCard})");
                if (_selectedCard.MatchId == card.MatchId)
                {
                    Debug.Log("[MatchGame] Match Correct!");
                    ConnectCards(_selectedCard, card);
                }
                else
                {
                    Debug.Log("[MatchGame] Match Incorrect!");
                    // Play mismatch red shake animation
                    _selectedCard.ShowMismatch();
                    card.ShowMismatch();
                }
                _selectedCard = null;
            }
        }

        // ── Helper Mechanics ─────────────────────────────────────────────────

        private void ConnectCards(MatchGameCard cardA, MatchGameCard cardB)
        {
            cardA.SetMatched(matchedOutlineColor);
            cardB.SetMatched(matchedOutlineColor);

            var lineGo = Instantiate(linePrefab, ActiveContent);
            lineGo.name = $"line_{cardA.MatchId}";

            // Prevent Layout Group from positioning this line object
            var layoutElement = lineGo.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = lineGo.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            lineGo.transform.SetAsFirstSibling(); // Render behind card elements

            var line = lineGo.GetComponent<MatchGameLine>();
            if (line == null) line = lineGo.AddComponent<MatchGameLine>();
            
            // Set dynamic color based on the left card's assigned color!
            Color lineCol = GetLeftColorForValue(cardA.MatchId);
            line.SetColor(lineCol);
            line.StartAnimate();

            var conn = new Connection
            {
                leftCard = cardA.IsLeftCard ? cardA : cardB,
                rightCard = cardA.IsLeftCard ? cardB : cardA,
                line = line
            };
            _connections.Add(conn);

            UpdateLinePositions();

            if (_connections.Count >= _slots.Count)
            {
                SetNextButtonsInteractable(true);
            }
        }

        // ── Drag & Drop Interaction Callbacks (called by MatchGameCard) ──────

        public void OnCardBeginDrag(MatchGameCard card, PointerEventData eventData)
        {
            if (card.IsMatched || _dragLine != null) return;

            // Clear any active tap-selection before starting drag
            if (_selectedCard != null)
            {
                _selectedCard.SetSelected(false);
                _selectedCard = null;
            }

            _dragStartCard = card;
            _dragStartCard.SetSelected(true);

            // Instantiate a temporary line
            var lineGo = Instantiate(linePrefab, ActiveContent);
            lineGo.name = $"drag_line_{card.MatchId}";
            
            var layoutElement = lineGo.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = lineGo.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            
            lineGo.transform.SetAsFirstSibling();

            _dragLine = lineGo.GetComponent<MatchGameLine>();
            if (_dragLine == null) _dragLine = lineGo.AddComponent<MatchGameLine>();
            
            // Set dynamic color based on the left card's assigned color!
            Color lineCol = GetLeftColorForValue(card.MatchId);
            _dragLine.SetColor(lineCol);
            
            // Force progress to 1 so it immediately follows the drag cursor
            _dragLine.SetProgress(1f);

            UpdateDragLinePosition(eventData);
        }

        public void OnCardDrag(MatchGameCard card, PointerEventData eventData)
        {
            if (card != _dragStartCard || _dragLine == null) return;
            UpdateDragLinePosition(eventData);
        }

        public void OnCardEndDrag(MatchGameCard card, PointerEventData eventData)
        {
            if (card != _dragStartCard || _dragLine == null) return;

            MatchGameCard targetCard = null;
            var hitGo = eventData.pointerCurrentRaycast.gameObject;
            if (hitGo != null)
            {
                // Can be on the card component itself or its children
                targetCard = hitGo.GetComponentInParent<MatchGameCard>();
            }

            if (targetCard != null && targetCard.IsLeftCard != _dragStartCard.IsLeftCard && !targetCard.IsMatched)
            {
                if (targetCard.MatchId == _dragStartCard.MatchId)
                {
                    // Correct match: Snap and save connection!
                    _dragStartCard.SetSelected(false);
                    targetCard.SetSelected(false);

                    FinalizeDragConnection(_dragStartCard, targetCard, _dragLine);
                    _dragLine = null;
                }
                else
                {
                    // Wrong match: Shake and reverse the line back
                    _dragStartCard.SetSelected(false);
                    targetCard.ShowMismatch();
                    _dragStartCard.ShowMismatch();

                    AnimateLineReverseAndDestroy(_dragLine);
                    _dragLine = null;
                }
            }
            else
            {
                // Dragged onto empty space or invalid target
                _dragStartCard.SetSelected(false);
                AnimateLineReverseAndDestroy(_dragLine);
                _dragLine = null;
            }

            _dragStartCard = null;
        }

        private void UpdateDragLinePosition(PointerEventData eventData)
        {
            if (_dragStartCard == null || _dragLine == null) return;

            Vector2 start = GetLocalPositionInContent(_dragStartCard.RectTransform);
            Vector2 end = ScreenPointToLocalPoint(eventData.position, eventData);
            
            _dragLine.SetPoints(start, end, lineThickness);
        }

        private void FinalizeDragConnection(MatchGameCard cardA, MatchGameCard cardB, MatchGameLine line)
        {
            cardA.SetMatched(matchedOutlineColor);
            cardB.SetMatched(matchedOutlineColor);

            var conn = new Connection
            {
                leftCard = cardA.IsLeftCard ? cardA : cardB,
                rightCard = cardA.IsLeftCard ? cardB : cardA,
                line = line
            };
            _connections.Add(conn);

            UpdateLinePositions();

            if (_connections.Count >= _slots.Count)
            {
                SetNextButtonsInteractable(true);
            }
        }

        private void AnimateLineReverseAndDestroy(MatchGameLine line)
        {
            if (line == null) return;
            StartCoroutine(LineReverseCoroutine(line));
        }

        private System.Collections.IEnumerator LineReverseCoroutine(MatchGameLine line)
        {
            float startProgress = line.Progress;
            float duration = 0.2f; // Time to shrink back
            float elapsed = 0f;

            Vector2 start = line.StartPos;
            Vector2 end = line.EndPos;

            while (elapsed < duration && line != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Lerp(startProgress, 0f, elapsed / duration);
                
                Vector2 currentEnd = start + (end - start) * progress;
                line.SetPoints(start, currentEnd, lineThickness);
                
                yield return null;
            }

            if (line != null)
            {
                Destroy(line.gameObject);
            }
        }

        private Vector2 ScreenPointToLocalPoint(Vector2 screenPoint, PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                ActiveContent,
                screenPoint,
                eventData.pressEventCamera,
                out Vector2 localPoint
            );
            return localPoint;
        }

        private void UpdateLinePositions()
        {
            foreach (var conn in _connections)
            {
                if (conn.leftCard != null && conn.rightCard != null && conn.line != null)
                {
                    Vector2 start = GetLocalPositionInContent(conn.leftCard.RectTransform);
                    Vector2 end = GetLocalPositionInContent(conn.rightCard.RectTransform);
                    conn.line.SetPoints(start, end, lineThickness);
                }
            }
        }

        private Vector2 GetLocalPositionInContent(RectTransform target)
        {
            if (target == null || ActiveContent == null) return Vector2.zero;
            return ActiveContent.InverseTransformPoint(target.position);
        }

        private Color GetLeftColorForValue(int value)
        {
            if (_leftColors.TryGetValue(value, out Color col))
            {
                return col;
            }
            return Color.white;
        }

        private Color GetRightColorForValue(int value)
        {
            if (_rightColors.TryGetValue(value, out Color col))
            {
                return col;
            }
            return Color.white;
        }

        private void UpdateScrollLocking()
        {
            UpdateScrollLockForContainer(portraitContent);
            UpdateScrollLockForContainer(landscapeContent);
        }

        private void UpdateScrollLockForContainer(RectTransform contentRt)
        {
            if (contentRt == null) return;

            var scrollRect = contentRt.GetComponentInParent<ScrollRect>();
            if (scrollRect == null) return;

            var viewportRt = scrollRect.viewport;
            if (viewportRt == null)
            {
                viewportRt = scrollRect.GetComponent<RectTransform>();
            }

            if (viewportRt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRt);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

                bool isLandscape = false;
                Transform curr = contentRt;
                while (curr != null)
                {
                    string nameLower = curr.name.ToLower();
                    if (nameLower.Contains("landscape") || nameLower.Contains("lanscape"))
                    {
                        isLandscape = true;
                        break;
                    }
                    if (nameLower.Contains("portrait") || nameLower.Contains("potrait"))
                    {
                        isLandscape = false;
                        break;
                    }
                    curr = curr.parent;
                }

                if (isLandscape)
                {
                    scrollRect.horizontal = (contentRt.rect.width > viewportRt.rect.width);
                    scrollRect.vertical = false;
                }
                else
                {
                    scrollRect.vertical = (contentRt.rect.height > viewportRt.rect.height);
                    scrollRect.horizontal = false;
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
