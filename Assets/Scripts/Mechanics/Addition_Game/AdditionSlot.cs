using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Mechanics.Counting;  

namespace KidGame.Mechanics.Addition
{
    public class AdditionSlot : MonoBehaviour
    {
        [Tooltip("Parent for the LEFT group of objects (GridLayoutGroup).")]
        [SerializeField] private Transform leftGrid;

        [Tooltip("Parent for the RIGHT group of objects (GridLayoutGroup).")]
        [SerializeField] private Transform rightGrid;

        [Tooltip("The answer drop target for this slot.")]
        [SerializeField] private AnswerDropZone dropZone;

        [Tooltip("The left sub-answer drop target (optional).")]
        [SerializeField] private AnswerDropZone leftDropZone;

        [Tooltip("The right sub-answer drop target (optional).")]
        [SerializeField] private AnswerDropZone rightDropZone;

        public int CorrectSum { get; private set; }

        private Transform ActualLeftGrid => (leftGrid != null && leftGrid.Find("Objects") != null) ? leftGrid.Find("Objects") : leftGrid;
        private Transform ActualRightGrid => (rightGrid != null && rightGrid.Find("Objects") != null) ? rightGrid.Find("Objects") : rightGrid;

        /// <param name="leftPrefab">Object icon for the left grid.</param>
        /// <param name="leftCount">How many left objects to spawn (1–12).</param>
        /// <param name="rightPrefab">Object icon for the right grid.</param>
        /// <param name="rightCount">How many right objects to spawn (1–12).</param>
        /// <param name="manager">Owning manager — notified on correct answer.</param>
        public void Setup(GameObject leftPrefab,  int leftCount,
                          GameObject rightPrefab, int rightCount,
                          AdditionGameManager manager)
        {
            CorrectSum = leftCount + rightCount;

            SpawnObjects(leftPrefab,  leftCount,  ActualLeftGrid);
            SpawnObjects(rightPrefab, rightCount, ActualRightGrid);

            bool countAddMode = manager.CountAddMode && leftDropZone != null && rightDropZone != null;
            if (countAddMode)
            {
                leftDropZone.gameObject.SetActive(true);
                rightDropZone.gameObject.SetActive(true);
                leftDropZone.Setup(leftCount, () => CheckAllAnswers(manager));
                rightDropZone.Setup(rightCount, () => CheckAllAnswers(manager));
            }
            else
            {
                if (leftDropZone != null) leftDropZone.gameObject.SetActive(false);
                if (rightDropZone != null) rightDropZone.gameObject.SetActive(false);
            }

            dropZone.Setup(CorrectSum, () => CheckAllAnswers(manager), () => {
                bool leftOk = leftDropZone == null || !leftDropZone.gameObject.activeSelf || leftDropZone.IsAnswered;
                bool rightOk = rightDropZone == null || !rightDropZone.gameObject.activeSelf || rightDropZone.IsAnswered;
                return leftOk && rightOk;
            });
            _leftCount = leftCount;
            _rightCount = rightCount;
            _countAddMode = countAddMode;
            _isSetup = true;

            AdjustHeights(leftCount, rightCount, countAddMode);
        }

        public void Setup(List<GameObject> leftDicePrefabs,
                          List<GameObject> rightDicePrefabs,
                          int leftSum, int rightSum,
                          AdditionGameManager manager)
        {
            CorrectSum = leftSum + rightSum;

            foreach (var prefab in leftDicePrefabs)
                SpawnObject(prefab, ActualLeftGrid);

            foreach (var prefab in rightDicePrefabs)
                SpawnObject(prefab, ActualRightGrid);

            bool countAddMode = manager.CountAddMode && leftDropZone != null && rightDropZone != null;
            if (countAddMode)
            {
                leftDropZone.gameObject.SetActive(true);
                rightDropZone.gameObject.SetActive(true);
                leftDropZone.Setup(leftSum, () => CheckAllAnswers(manager));
                rightDropZone.Setup(rightSum, () => CheckAllAnswers(manager));
            }
            else
            {
                if (leftDropZone != null) leftDropZone.gameObject.SetActive(false);
                if (rightDropZone != null) rightDropZone.gameObject.SetActive(false);
            }

            dropZone.Setup(CorrectSum, () => CheckAllAnswers(manager), () => {
                bool leftOk = leftDropZone == null || !leftDropZone.gameObject.activeSelf || leftDropZone.IsAnswered;
                bool rightOk = rightDropZone == null || !rightDropZone.gameObject.activeSelf || rightDropZone.IsAnswered;
                return leftOk && rightOk;
            });
            _leftCount = leftDicePrefabs.Count;
            _rightCount = rightDicePrefabs.Count;
            _countAddMode = countAddMode;
            _isSetup = true;

            AdjustHeights(leftDicePrefabs.Count, rightDicePrefabs.Count, countAddMode);
        }

        private void CheckAllAnswers(AdditionGameManager manager)
        {
            bool leftOk = leftDropZone == null || !leftDropZone.gameObject.activeSelf || leftDropZone.IsAnswered;
            bool rightOk = rightDropZone == null || !rightDropZone.gameObject.activeSelf || rightDropZone.IsAnswered;
            bool finalOk = dropZone.IsAnswered;

            if (leftOk && rightOk && finalOk)
            {
                manager.OnSlotAnswered();
            }
        }

        private int _leftCount = -1;
        private int _rightCount = -1;
        private bool _countAddMode;
        private bool _isSetup = false;
        private float _lastParentWidth = -1f;

        private float _initialSlotHeight = -1f;
        private float _initialObjectBoxHeight = -1f;

        private bool _isAdjusting;

        private void OnRectTransformDimensionsChange()
        {
            if (!_isSetup || _leftCount < 0 || _rightCount < 0 || _isAdjusting) return;
            if (!isActiveAndEnabled) return;

            var parentRt = transform.parent as RectTransform;
            if (parentRt == null) return;

            float parentWidth = parentRt.rect.width;
            if (parentWidth > 0f && Mathf.Abs(parentWidth - _lastParentWidth) > 0.1f)
            {
                _lastParentWidth = parentWidth;
                AdjustHeights(_leftCount, _rightCount, _countAddMode);
            }
        }

        private void CacheInitialHeights()
        {
            if (_initialSlotHeight < 0f)
            {
                var slotRt = transform as RectTransform;
                float rectHeight = slotRt != null ? slotRt.rect.height : 0f;

                var le = GetComponent<UnityEngine.UI.LayoutElement>();
                float leHeight = le != null ? le.preferredHeight : 0f;

                if (rectHeight > 0f) _initialSlotHeight = rectHeight;
                else if (leHeight > 0f) _initialSlotHeight = leHeight;
                else _initialSlotHeight = 190f; // fallback design height

                // The Object box base height should correspond to the slot height.
                // Since the prefab has a mismatched height of 438.892f, we force it to match the slot height.
                _initialObjectBoxHeight = _initialSlotHeight;
            }
        }

        private float GetColumnHeight(float maxGridHeight, bool countAddMode, AnswerDropZone dropZone)
        {
            if (countAddMode && dropZone != null && dropZone.gameObject.activeSelf)
            {
                var colLayout = dropZone.transform.parent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                float spacing = colLayout != null ? colLayout.spacing : 0f;
                float padding = colLayout != null ? (colLayout.padding.top + colLayout.padding.bottom) : 0f;
                
                var dropRt = dropZone.transform as RectTransform;
                float dropHeight = dropRt != null ? dropRt.rect.height : 70f;
                if (dropHeight <= 0f) dropHeight = 70f;
                
                return maxGridHeight + spacing + padding + dropHeight;
            }
            return maxGridHeight;
        }

        private void AdjustHeights(int leftCount, int rightCount, bool countAddMode)
        {
            _isAdjusting = true;
            try
            {
            var parentRt = transform.parent as RectTransform;
            if (parentRt != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
            }

            CacheInitialHeights();

            var aLeft = ActualLeftGrid;
            var aRight = ActualRightGrid;

            bool hasColumns = aLeft.parent != aRight.parent;

            // Force layout rebuild on parent container FIRST to resolve current widths immediately
            var rtObjectBox = (hasColumns ? aLeft.parent.parent : aLeft.parent) as RectTransform;
            if (rtObjectBox != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rtObjectBox);
            }

            float leftGridHeight = GetGridCalculatedHeight(aLeft, leftCount);
            float rightGridHeight = GetGridCalculatedHeight(aRight, rightCount);
            float maxGridHeight = Mathf.Max(leftGridHeight, rightGridHeight);

            float leftColHeight = hasColumns ? GetColumnHeight(maxGridHeight, countAddMode, leftDropZone) : maxGridHeight;
            float rightColHeight = hasColumns ? GetColumnHeight(maxGridHeight, countAddMode, rightDropZone) : maxGridHeight;
            float maxColHeight = Mathf.Max(leftColHeight, rightColHeight);

            // 1. Resize Object box (the parent container) FIRST
            if (rtObjectBox != null)
            {
                rtObjectBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxColHeight);
                var leObjectBox = rtObjectBox.GetComponent<UnityEngine.UI.LayoutElement>();
                if (leObjectBox == null) leObjectBox = rtObjectBox.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                leObjectBox.preferredHeight = maxColHeight;
            }

            // 2. Resize Grid Child Elements to the SAME maxGridHeight (aligns sub-answer drop zones)
            var rtLeft = aLeft as RectTransform;
            if (rtLeft != null) rtLeft.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxGridHeight);
            var leLeft = aLeft.GetComponent<UnityEngine.UI.LayoutElement>();
            if (leLeft == null) leLeft = aLeft.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            leLeft.preferredHeight = maxGridHeight;

            var rtRight = aRight as RectTransform;
            if (rtRight != null) rtRight.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxGridHeight);
            var leRight = aRight.GetComponent<UnityEngine.UI.LayoutElement>();
            if (leRight == null) leRight = aRight.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            leRight.preferredHeight = maxGridHeight;

            // 3. Resize Columns (if they exist)
            if (hasColumns)
            {
                var rtLeftCol = aLeft.parent as RectTransform;
                if (rtLeftCol != null) rtLeftCol.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, leftColHeight);
                var leLeftCol = aLeft.parent.GetComponent<UnityEngine.UI.LayoutElement>();
                if (leLeftCol == null) leLeftCol = aLeft.parent.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                leLeftCol.preferredHeight = leftColHeight;

                var rtRightCol = aRight.parent as RectTransform;
                if (rtRightCol != null) rtRightCol.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rightColHeight);
                var leRightCol = aRight.parent.GetComponent<UnityEngine.UI.LayoutElement>();
                if (leRightCol == null) leRightCol = aRight.parent.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                leRightCol.preferredHeight = rightColHeight;
            }

            // 4. Center and scale Plus symbol vertically
            Transform parentOfPlus = hasColumns ? aLeft.parent.parent : aLeft.parent;
            var plusTransform = parentOfPlus.Find("Plus") as RectTransform;
            if (plusTransform != null)
            {
                var min = plusTransform.anchorMin;
                var max = plusTransform.anchorMax;
                min.y = 0.5f;
                max.y = 0.5f;
                plusTransform.anchorMin = min;
                plusTransform.anchorMax = max;

                plusTransform.sizeDelta = new Vector2(plusTransform.sizeDelta.x, 80f);
                plusTransform.anchoredPosition = new Vector2(plusTransform.anchoredPosition.x, 0f);
            }

            // 5. Resize Slot Row
            var leSlot = GetComponent<UnityEngine.UI.LayoutElement>();
            if (leSlot == null) leSlot = gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

            var layoutGroup = GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            float verticalPadding = layoutGroup != null ? (layoutGroup.padding.top + layoutGroup.padding.bottom) : 0f;

            float newSlotHeight = Mathf.Max(190f + verticalPadding, maxColHeight + verticalPadding);

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
                        if (width <= 0) width = 310f; // safe design fallback for new prefab variant
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

        private static void SpawnObjects(GameObject prefab, int count, Transform parent)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnObject(prefab, parent);
            }
        }

        private static void SpawnObject(GameObject prefab, Transform parent)
        {
            var obj = Object.Instantiate(prefab, parent);

            // Tap animation — added automatically, no prefab changes needed
            if (obj.GetComponent<CountingObject>() == null)
                obj.AddComponent<CountingObject>();
        }
    }
}
