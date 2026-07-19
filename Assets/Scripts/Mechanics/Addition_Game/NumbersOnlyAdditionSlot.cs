using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Mechanics.Counting;

namespace KidGame.Mechanics.Addition
{
    public class NumbersOnlyAdditionSlot : MonoBehaviour
    {
        [Tooltip("The container where numbers and operator/equals symbols will be spawned (GridLayoutGroup).")]
        [SerializeField] private Transform objectBox;

        [Tooltip("The built-in answer drop zone for this numbers-only slot.")]
        [SerializeField] private AnswerDropZone answerDropZone;

        public int CorrectSum { get; private set; }

        private float _lastParentWidth = -1f;
        private int _lastOperandCount = -1;
        private bool _isSetup = false;

        public void Setup(List<int> numbers, AdditionGameManager manager, GameObject numberPrefab, GameObject plusPrefab, GameObject equalsPrefab)
        {
            _isSetup = true;
            _lastOperandCount = numbers.Count;

            int sum = 0;
            foreach (var num in numbers) sum += num;
            CorrectSum = sum;

            // Clear any existing children in objectBox
            if (objectBox != null)
            {
                foreach (Transform child in objectBox)
                {
                    Destroy(child.gameObject);
                }

                // Spawn numbers and operators
                for (int i = 0; i < numbers.Count; i++)
                {
                    // Spawn number box
                    var numGo = Instantiate(numberPrefab, objectBox);
                    numGo.name = $"number_{numbers[i]}";

                    var card = numGo.GetComponent<AnswerCard>();
                    if (card != null)
                    {
                        Destroy(card);
                    }

                    var textComp = numGo.GetComponentInChildren<TMPro.TMP_Text>();
                    if (textComp != null) textComp.text = numbers[i].ToString();

                    // Set dynamic background color
                    var bgImage = numGo.GetComponent<UnityEngine.UI.Image>();
                    if (bgImage != null)
                    {
                        bgImage.color = manager.GetColorForIndex(i);
                    }

                    if (i < numbers.Count - 1)
                    {
                        // Spawn plus sign
                        var plusGo = Instantiate(plusPrefab, objectBox);
                        plusGo.name = "plus";
                        var plusText = plusGo.GetComponentInChildren<TMPro.TMP_Text>();
                        if (plusText != null) plusText.text = "+";
                    }
                }
            }

            // Setup the built-in answer drop zone
            if (answerDropZone != null)
            {
                answerDropZone.Setup(CorrectSum, () => manager.OnSlotAnswered());
            }

            // Adjust heights
            AdjustHeights();
        }

        private bool _isAdjusting;

        private void OnRectTransformDimensionsChange()
        {
            if (!_isSetup || _lastOperandCount <= 0 || objectBox == null || _isAdjusting) return;
            if (!isActiveAndEnabled) return;

            var parentRt = transform.parent as RectTransform;
            if (parentRt == null) return;

            float parentWidth = parentRt.rect.width;
            if (parentWidth > 0f && Mathf.Abs(parentWidth - _lastParentWidth) > 0.1f)
            {
                _lastParentWidth = parentWidth;
                AdjustHeights();
            }
        }

        private void AdjustHeights()
        {
            _isAdjusting = true;
            try
            {
                if (objectBox == null) return;

                var grid = objectBox.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                if (grid == null) return;

                // Force layout rebuild on parent RectTransform first to resolve parent width
                var parentRt = transform.parent as RectTransform;
                if (parentRt != null)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                }

                // Total grid items count in the objectBox container is:
                // itemCount numbers + (itemCount - 1) pluses
                int totalItemsInGrid = _lastOperandCount + (_lastOperandCount - 1);

                float gridHeight = GetGridCalculatedHeight(objectBox, totalItemsInGrid);

                // Set size on Object box
                var rtObjectBox = objectBox as RectTransform;
                if (rtObjectBox != null)
                {
                    rtObjectBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, gridHeight);
                    var leObjectBox = objectBox.GetComponent<UnityEngine.UI.LayoutElement>();
                    if (leObjectBox == null) leObjectBox = objectBox.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                    leObjectBox.preferredHeight = gridHeight;
                }

                // Set size on Slot Row (this slot)
                var leSlot = GetComponent<UnityEngine.UI.LayoutElement>();
                if (leSlot == null) leSlot = gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

                // Calculate height. If slot contains answerDropZone parallel/outside objectBox (e.g. In a HorizontalLayoutGroup),
                // the slot height needs to be at least the max of gridHeight and answerDropZone height.
                float targetContentHeight = gridHeight;
                if (answerDropZone != null)
                {
                    var rtDrop = answerDropZone.transform as RectTransform;
                    float dropHeight = rtDrop != null ? rtDrop.rect.height : 100f;
                    targetContentHeight = Mathf.Max(gridHeight, dropHeight);
                }

                var layoutGroup = GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                float verticalPadding = layoutGroup != null ? (layoutGroup.padding.top + layoutGroup.padding.bottom) : 0f;

                // Use base height of 100f or calculated height
                float newSlotHeight = Mathf.Max(100f, targetContentHeight + verticalPadding);
                leSlot.preferredHeight = newSlotHeight;

                var slotRt = transform as RectTransform;
                if (slotRt != null)
                {
                    slotRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newSlotHeight);
                }
            }
            finally
            {
                _isAdjusting = false;
            }
        }

        private float GetGridCalculatedHeight(Transform gridTransform, int itemCount)
        {
            if (gridTransform == null) return 0f;
            var grid = gridTransform.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (grid == null) return 0f;

            int columns = 1;
            if (grid.constraint == UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount)
            {
                columns = grid.constraintCount;
            }
            else
            {
                var rectTransform = gridTransform as RectTransform;
                if (rectTransform != null)
                {
                    float width = rectTransform.rect.width;
                    if (width <= 0)
                    {
                        var current = rectTransform.parent as RectTransform;
                        while (current != null && current.rect.width <= 0)
                        {
                            current = current.parent as RectTransform;
                        }
                        if (current != null)
                        {
                            width = current.rect.width;
                            var parentLayout = rectTransform.parent.GetComponent<LayoutGroup>();
                            if (parentLayout != null)
                            {
                                width -= (parentLayout.padding.left + parentLayout.padding.right);
                            }
                        }
                    }
                    if (width <= 0)
                    {
                        width = UnityEngine.UI.LayoutUtility.GetPreferredWidth(rectTransform);
                        if (width <= 0) width = UnityEngine.UI.LayoutUtility.GetMinWidth(rectTransform);
                        if (width <= 0) width = 310f; // safe design fallback
                    }
                    if (width > 0)
                    {
                        columns = Mathf.FloorToInt((width - grid.padding.horizontal + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x));
                    }
                }
            }
            if (columns < 1) columns = 1;

            int rows = Mathf.CeilToInt((float)itemCount / columns);
            if (rows < 1) rows = 1;

            return grid.padding.vertical + (rows * grid.cellSize.y) + ((rows - 1) * grid.spacing.y);
        }
    }
}
