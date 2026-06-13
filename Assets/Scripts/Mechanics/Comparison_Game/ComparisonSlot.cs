using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Mechanics.Comparison
{
    public class ComparisonSlot : MonoBehaviour
    {
        private bool _isSetup = false;
        private List<int> _leftNumbers;
        private List<int> _rightNumbers;
        private ComparisonGameManager _manager;

        public void Setup(
            List<int> leftNumbers, 
            List<int> rightNumbers, 
            ComparisonGameManager manager, 
            GameObject numberPrefab, 
            GameObject plusPrefab, 
            GameObject dropZonePrefab)
        {
            _leftNumbers = leftNumbers;
            _rightNumbers = rightNumbers;
            _manager = manager;
            _isSetup = true;

            // Clear any existing children
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            // Clear conflicting layout groups (keep GridLayoutGroup)
            var hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) DestroyImmediate(hlg);
            var vlg = GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);

            // Configure GridLayoutGroup on this row dynamically
            var glg = GetComponent<GridLayoutGroup>();
            if (glg == null) glg = gameObject.AddComponent<GridLayoutGroup>();
            if (glg != null)
            {
                glg.cellSize = new Vector2(100f, 100f);
                glg.spacing = new Vector2(20f, 0f);
                glg.childAlignment = TextAnchor.MiddleCenter;
                glg.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                glg.constraintCount = 1;
                glg.padding = new RectOffset(20, 20, 10, 10);
            }

            // Set Slot preferred height
            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredHeight = 120f;
            }

            // 1. Calculate sums to determine expected relation sign
            int leftSum = 0;
            foreach (int n in leftNumbers) leftSum += n;

            int rightSum = 0;
            foreach (int n in rightNumbers) rightSum += n;

            ComparisonSign expectedSign;
            if (leftSum < rightSum)
                expectedSign = ComparisonSign.LessThan;
            else if (leftSum == rightSum)
                expectedSign = ComparisonSign.EqualTo;
            else
                expectedSign = ComparisonSign.GreaterThan;

            int colorIndex = 0;

            // 2. Spawn Left Operands
            for (int i = 0; i < leftNumbers.Count; i++)
            {
                var numGo = Instantiate(numberPrefab, transform);
                numGo.name = $"left_num_{leftNumbers[i]}";
                ConfigureNumberBox(numGo, leftNumbers[i], manager.GetColorForIndex(colorIndex++));

                if (i < leftNumbers.Count - 1)
                {
                    var plusGo = Instantiate(plusPrefab, transform);
                    plusGo.name = "left_plus";
                    ConfigureTextSymbol(plusGo, "+");
                }
            }

            // 3. Spawn Drop Zone in the middle
            var dropZoneGo = Instantiate(dropZonePrefab, transform);
            dropZoneGo.name = "sign_drop_zone";
            var dropZone = dropZoneGo.GetComponent<ComparisonDropZone>();
            if (dropZone == null) dropZone = dropZoneGo.AddComponent<ComparisonDropZone>();
            dropZone.Setup(expectedSign, () => manager.OnSlotAnswered());

            // 4. Spawn Right Operands
            for (int i = 0; i < rightNumbers.Count; i++)
            {
                var numGo = Instantiate(numberPrefab, transform);
                numGo.name = $"right_num_{rightNumbers[i]}";
                ConfigureNumberBox(numGo, rightNumbers[i], manager.GetColorForIndex(colorIndex++));

                if (i < rightNumbers.Count - 1)
                {
                    var plusGo = Instantiate(plusPrefab, transform);
                    plusGo.name = "right_plus";
                    ConfigureTextSymbol(plusGo, "+");
                }
            }

            // Force layout rebuild
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        private void ConfigureNumberBox(GameObject go, int val, Color color)
        {
            var textComp = go.GetComponentInChildren<TMPro.TMP_Text>();
            if (textComp != null) textComp.text = val.ToString();

            var bg = go.GetComponent<Image>();
            if (bg != null) bg.color = color;

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(100f, 100f);
            }
        }

        private void ConfigureTextSymbol(GameObject go, string symbol)
        {
            var textComp = go.GetComponentInChildren<TMPro.TMP_Text>();
            if (textComp != null) textComp.text = symbol;

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(50f, 100f);
            }
        }
    }
}
