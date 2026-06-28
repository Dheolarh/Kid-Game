using UnityEngine;

namespace KidGame.Mechanics.Counting
{
    public class CountingSlot : MonoBehaviour
    {
        [Tooltip("Parent for the spawned object icons (should have a GridLayoutGroup).")]
        [SerializeField] private Transform objectGrid;

        [Tooltip("The answer drop target inside this slot.")]
        [SerializeField] private AnswerDropZone dropZone;

        /// <summary>The object count this slot was set up with.</summary>
        public int CorrectCount { get; private set; }

        public void Setup(GameObject objectPrefab, int count, CountingGameManager manager)
        {
            CorrectCount = count;

            for (int i = 0; i < count; i++)
            {
                var obj = Instantiate(objectPrefab, objectGrid);

                if (obj.GetComponent<CountingObject>() == null)
                    obj.AddComponent<CountingObject>();
            }

            // Pass a lambda so AnswerDropZone doesn't need to reference CountingSlot/Manager directly
            dropZone.Setup(count, () => manager.OnSlotAnswered(this));
            _itemCount = count;
            _isSetup = true;
            AdjustHeights(count);
        }

        public void Setup(System.Collections.Generic.List<GameObject> itemPrefabs, int totalSum, CountingGameManager manager)
        {
            CorrectCount = totalSum;

            foreach (var prefab in itemPrefabs)
            {
                var obj = Instantiate(prefab, objectGrid);

                if (obj.GetComponent<CountingObject>() == null)
                    obj.AddComponent<CountingObject>();
            }

            dropZone.Setup(totalSum, () => manager.OnSlotAnswered(this));
            _itemCount = itemPrefabs.Count;
            _isSetup = true;
            AdjustHeights(itemPrefabs.Count);
        }

        private float _initialSlotHeight = -1f;
        private float _initialGridHeight = -1f;

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
                else _initialSlotHeight = 370f; // fallback design height

                var gridRt = objectGrid as RectTransform;
                float gridHeight = gridRt != null ? gridRt.rect.height : 0f;

                var leGrid = gridRt != null ? gridRt.GetComponent<UnityEngine.UI.LayoutElement>() : null;
                float leGridHeight = leGrid != null ? leGrid.preferredHeight : 0f;

                if (gridHeight > 0f) _initialGridHeight = gridHeight;
                else if (leGridHeight > 0f) _initialGridHeight = leGridHeight;
                else _initialGridHeight = 369.83f; // fallback design height
            }
        }

        private int _itemCount = -1;
        private bool _isSetup = false;
        private float _lastParentWidth = -1f;

        private void Update()
        {
            if (!_isSetup || _itemCount <= 0) return;

            var parentRt = transform.parent as RectTransform;
            if (parentRt != null)
            {
                float parentWidth = parentRt.rect.width;
                if (parentWidth > 0f && Mathf.Abs(parentWidth - _lastParentWidth) > 0.1f)
                {
                    _lastParentWidth = parentWidth;
                    AdjustHeights(_itemCount);
                }
            }
        }

        private void AdjustHeights(int itemCount)
        {
            var parentRt = transform.parent as RectTransform;
            if (parentRt != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
            }

            CacheInitialHeights();

            float gridHeight = GetGridCalculatedHeight(objectGrid, itemCount);

            var rt = objectGrid as RectTransform;
            if (rt != null)
            {
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, gridHeight);
            }

            var leGrid = objectGrid.GetComponent<UnityEngine.UI.LayoutElement>();
            if (leGrid == null)
            {
                leGrid = objectGrid.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            }
            leGrid.preferredHeight = gridHeight;

            var leSlot = GetComponent<UnityEngine.UI.LayoutElement>();
            if (leSlot == null)
            {
                leSlot = gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            }

            var layoutGroup = GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            float verticalPadding = layoutGroup != null ? (layoutGroup.padding.top + layoutGroup.padding.bottom) : 0f;

            float minSlotHeight = 120f + verticalPadding;
            float newSlotHeight = Mathf.Max(minSlotHeight, gridHeight + verticalPadding);

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
    }
}
