using System.Collections.Generic;
using UnityEngine;
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

        public int CorrectSum { get; private set; }

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

            SpawnObjects(leftPrefab,  leftCount,  leftGrid);
            SpawnObjects(rightPrefab, rightCount, rightGrid);

            // Lambda keeps AnswerDropZone decoupled from Addition-specific types
            dropZone.Setup(CorrectSum, () => manager.OnSlotAnswered());
            AdjustHeights(leftCount, rightCount);
        }

        public void Setup(List<GameObject> leftDicePrefabs,
                          List<GameObject> rightDicePrefabs,
                          int leftSum, int rightSum,
                          AdditionGameManager manager)
        {
            CorrectSum = leftSum + rightSum;

            foreach (var prefab in leftDicePrefabs)
                SpawnObject(prefab, leftGrid);

            foreach (var prefab in rightDicePrefabs)
                SpawnObject(prefab, rightGrid);

            dropZone.Setup(CorrectSum, () => manager.OnSlotAnswered());
            AdjustHeights(leftDicePrefabs.Count, rightDicePrefabs.Count);
        }

        private float _initialSlotHeight = -1f;
        private float _initialObjectBoxHeight = -1f;

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

        private void AdjustHeights(int leftCount, int rightCount)
        {
            CacheInitialHeights();

            float leftHeight = GetGridCalculatedHeight(leftGrid, leftCount);
            float rightHeight = GetGridCalculatedHeight(rightGrid, rightCount);
            float maxGridHeight = Mathf.Max(leftHeight, rightHeight);

            // Resize Object box (the parent of left and right grids) FIRST
            var rtObjectBox = leftGrid.parent as RectTransform;
            if (rtObjectBox != null)
            {
                rtObjectBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxGridHeight);
                var leObjectBox = rtObjectBox.GetComponent<UnityEngine.UI.LayoutElement>();
                if (leObjectBox == null) leObjectBox = rtObjectBox.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                leObjectBox.preferredHeight = maxGridHeight;
            }

            // Resize Left Grid SECOND
            var rtLeft = leftGrid as RectTransform;
            Debug.LogWarning($"[AdditionDebug] Slot: leftCount={leftCount}, rightCount={rightCount}, leftGridWidth={rtLeft.rect.width}, leftHeight={leftHeight}, ObjectBoxHeight={_initialObjectBoxHeight}");
            if (rtLeft != null) rtLeft.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, leftHeight);
            var leLeft = leftGrid.GetComponent<UnityEngine.UI.LayoutElement>();
            if (leLeft == null) leLeft = leftGrid.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            leLeft.preferredHeight = leftHeight;

            // Resize Right Grid THIRD
            var rtRight = rightGrid as RectTransform;
            if (rtRight != null) rtRight.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rightHeight);
            var leRight = rightGrid.GetComponent<UnityEngine.UI.LayoutElement>();
            if (leRight == null) leRight = rightGrid.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            leRight.preferredHeight = rightHeight;

            // Center and scale Plus symbol vertically
            var plusTransform = leftGrid.parent.Find("Plus") as RectTransform;
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

            // Resize Slot Row
            var leSlot = GetComponent<UnityEngine.UI.LayoutElement>();
            if (leSlot == null) leSlot = gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

            float delta = maxGridHeight - _initialObjectBoxHeight;
            float newSlotHeight = _initialSlotHeight + Mathf.Max(0f, delta);

            leSlot.preferredHeight = newSlotHeight;

            var slotRt = transform as RectTransform;
            if (slotRt != null)
            {
                slotRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newSlotHeight);
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
                        width = UnityEngine.UI.LayoutUtility.GetPreferredWidth(rectTransform);
                        if (width <= 0) width = UnityEngine.UI.LayoutUtility.GetMinWidth(rectTransform);
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
