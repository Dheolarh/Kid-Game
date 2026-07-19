using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Mechanics.Counting;

namespace KidGame.Mechanics.NumberRecall
{
    public class NumberRecallSlot : MonoBehaviour
    {
        [Tooltip("The prefab for a revealed number box.")]
        [SerializeField] private GameObject numberPrefab;

        [Tooltip("The prefab for an empty answer box (drop zone).")]
        [SerializeField] private GameObject answerPrefab;

        private int _unsolvedCount;
        private System.Action _onCompleted;
        private int _sequenceLength;

        public void Setup(int startValue, int length, int step, List<int> hiddenIndices, Color[] palette, System.Action onCompleted)
        {
            _sequenceLength = length;
            _onCompleted = onCompleted;
            _unsolvedCount = hiddenIndices.Count;

            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
                Destroy(fitter);
            }

            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < length; i++)
            {
                int val = startValue + i * step;
                Color col = palette[i % palette.Length];

                if (hiddenIndices.Contains(i))
                {
                    // Spawn answer slot
                    var answerGo = Instantiate(answerPrefab, transform);
                    answerGo.name = $"answer_{val}";
                    
                    var dropZone = answerGo.GetComponent<AnswerDropZone>();
                    if (dropZone == null) dropZone = answerGo.AddComponent<AnswerDropZone>();

                    dropZone.Setup(val, () => {
                        _unsolvedCount--;
                        if (_unsolvedCount <= 0)
                        {
                            _onCompleted?.Invoke();
                        }
                    });
                }
                else
                {
                    // Spawn revealed number
                    var numberGo = Instantiate(numberPrefab, transform);
                    numberGo.name = $"number_{val}";

                    var card = numberGo.GetComponent<AnswerCard>();
                    if (card != null)
                    {
                        Destroy(card);
                    }

                    var label = numberGo.GetComponentInChildren<TMPro.TMP_Text>();
                    if (label != null) label.text = val.ToString();

                    var bgImage = numberGo.GetComponent<Image>();
                    if (bgImage != null) bgImage.color = col;
                }
            }

            AdjustHeights(length);
        }

        private float _lastParentWidth = -1f;

        private bool _isAdjusting;

        private void OnRectTransformDimensionsChange()
        {
            if (_sequenceLength <= 0 || _isAdjusting) return;
            if (!isActiveAndEnabled) return;

            var parentRt = transform.parent as RectTransform;
            if (parentRt == null) return;

            float parentWidth = parentRt.rect.width;
            if (parentWidth > 0f && Mathf.Abs(parentWidth - _lastParentWidth) > 0.1f)
            {
                _lastParentWidth = parentWidth;
                AdjustHeights(_sequenceLength);
            }
        }

        public void RecalculateHeight()
        {
            AdjustHeights(_sequenceLength);
        }

        private void AdjustHeights(int length)
        {
            _isAdjusting = true;
            try
            {
                var grid = GetComponentInChildren<GridLayoutGroup>();
                if (grid == null) return;

                // Force layout rebuild on slot's parent container to resolve parent width first
                var parentRt = transform.parent as RectTransform;
                if (parentRt != null)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                }

                float gridHeight = GetGridCalculatedHeight(grid.transform, length);

                // Resize Grid RectTransform if it is a child
                if (grid.transform != transform)
                {
                    var rtGrid = grid.transform as RectTransform;
                    if (rtGrid != null) rtGrid.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, gridHeight);
                    var leGrid = grid.GetComponent<LayoutElement>();
                    if (leGrid == null) leGrid = grid.gameObject.AddComponent<LayoutElement>();
                    leGrid.preferredHeight = gridHeight;
                }

                // Calculate slot preferred height from any LayoutGroup (like GridLayoutGroup, HorizontalLayoutGroup, or VerticalLayoutGroup)
                LayoutGroup layoutGroup = GetComponent<LayoutGroup>();
                float verticalPadding = layoutGroup != null ? (layoutGroup.padding.top + layoutGroup.padding.bottom) : 0f;

                // Since gridHeight from GetGridCalculatedHeight already includes the grid padding,
                float newSlotHeight = gridHeight;
                if (layoutGroup != null && !(layoutGroup is GridLayoutGroup))
                {
                    newSlotHeight += verticalPadding;
                }
                newSlotHeight = Mathf.Max(100f, newSlotHeight);

                var leSlot = GetComponent<LayoutElement>();
                if (leSlot == null) leSlot = gameObject.AddComponent<LayoutElement>();
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
            var grid = gridTransform.GetComponent<GridLayoutGroup>();
            if (grid == null) return 0f;

            int columns = 1;
            if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
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
                        width = LayoutUtility.GetPreferredWidth(rectTransform);
                        if (width <= 0) width = LayoutUtility.GetMinWidth(rectTransform);
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
