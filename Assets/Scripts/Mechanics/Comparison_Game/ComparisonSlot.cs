using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Mechanics.Counting;

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
            GameObject dropZonePrefab,
            GameObject leftObjectPrefab = null,
            GameObject rightObjectPrefab = null)
        {
            _leftNumbers = leftNumbers;
            _rightNumbers = rightNumbers;
            _manager = manager;
            _isSetup = true;

            // 1. Resolve left/right containers in prefab hierarchy
            Transform leftContainer = transform.Find("left");
            Transform rightContainer = transform.Find("right");

            // If not found, self-heal to use root as containers
            if (leftContainer == null) leftContainer = transform;
            if (rightContainer == null) rightContainer = transform;

            // 2. Clear existing items
            if (leftContainer != transform)
            {
                foreach (Transform child in leftContainer) Destroy(child.gameObject);
            }
            if (rightContainer != transform)
            {
                foreach (Transform child in rightContainer) Destroy(child.gameObject);
            }
            // Clear any direct children (except the left/right container GameObjects)
            foreach (Transform child in transform)
            {
                if (child != leftContainer && child != rightContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // 3. Configure layout of containers
            if (manager.NumbersOnlyMode)
            {
                ConfigureContainerLayout(leftContainer);
                ConfigureContainerLayout(rightContainer);
            }

            // Configure Slot root layout (always horizontal layout to hold left_container, drop_zone, right_container)
            var rootGlg = GetComponent<GridLayoutGroup>();
            if (rootGlg != null) DestroyImmediate(rootGlg);
            var rootVlg = GetComponent<VerticalLayoutGroup>();
            if (rootVlg != null) DestroyImmediate(rootVlg);

            var rootHlg = GetComponent<HorizontalLayoutGroup>();
            if (rootHlg == null) rootHlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            if (rootHlg != null)
            {
                rootHlg.spacing = 40f; // matches screenshot spacing
                rootHlg.childAlignment = TextAnchor.MiddleCenter;
                rootHlg.childControlWidth = false;
                rootHlg.childControlHeight = true;
                rootHlg.childForceExpandWidth = false;
                rootHlg.childForceExpandHeight = false;
                rootHlg.padding = new RectOffset(20, 20, 10, 40);
            }

            // Set Slot preferred height
            var le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredHeight = 120f;
            }

            // 4. Calculate expected sign
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

            // 5. Spawn Left Operands
            for (int i = 0; i < leftNumbers.Count; i++)
            {
                if (manager.NumbersOnlyMode)
                {
                    var numGo = Instantiate(numberPrefab, leftContainer);
                    numGo.name = $"left_num_{leftNumbers[i]}";
                    ConfigureNumberBox(numGo, leftNumbers[i], manager.GetColorForIndex(colorIndex++));

                    if (i < leftNumbers.Count - 1)
                    {
                        var plusGo = Instantiate(plusPrefab, leftContainer);
                        plusGo.name = "left_plus";
                        ConfigureTextSymbol(plusGo, "+");
                    }
                }
                else
                {
                    // Spawn objects directly under leftContainer (object mode)
                    for (int j = 0; j < leftNumbers[i]; j++)
                    {
                        var prefabToUse = leftObjectPrefab != null ? leftObjectPrefab : manager.LeftObjectPrefab;
                        var obj = Instantiate(prefabToUse, leftContainer);
                        if (obj.GetComponent<CountingObject>() == null)
                        {
                            obj.AddComponent<CountingObject>();
                        }
                    }
                }
            }

            // 6. Spawn Drop Zone in the middle
            var dropZoneGo = Instantiate(dropZonePrefab, transform);
            dropZoneGo.name = "sign_drop_zone";

            int dropZoneIndex = 1;
            if (leftContainer == transform)
            {
                // In single-row mode (numbersOnlyMode), we lay out left numbers and plus symbols directly under the slot root.
                // There are leftNumbers.Count number boxes and (leftNumbers.Count - 1) plus symbols.
                dropZoneIndex = leftNumbers.Count + (leftNumbers.Count - 1);
            }
            dropZoneGo.transform.SetSiblingIndex(dropZoneIndex);

            var dzRt = dropZoneGo.GetComponent<RectTransform>();
            if (dzRt != null)
            {
                dzRt.sizeDelta = new Vector2(100f, 100f);
            }

            var dzLe = dropZoneGo.GetComponent<LayoutElement>();
            if (dzLe == null) dzLe = dropZoneGo.AddComponent<LayoutElement>();
            if (dzLe != null)
            {
                dzLe.flexibleWidth = 0f;
                dzLe.flexibleHeight = 0f;
                dzLe.preferredWidth = 100f;
                dzLe.preferredHeight = 100f;
            }

            var dropZone = dropZoneGo.GetComponent<ComparisonDropZone>();
            if (dropZone == null) dropZone = dropZoneGo.AddComponent<ComparisonDropZone>();
            dropZone.Setup(expectedSign, () => manager.OnSlotAnswered());

            // 7. Spawn Right Operands
            for (int i = 0; i < rightNumbers.Count; i++)
            {
                if (manager.NumbersOnlyMode)
                {
                    var numGo = Instantiate(numberPrefab, rightContainer);
                    numGo.name = $"right_num_{rightNumbers[i]}";
                    ConfigureNumberBox(numGo, rightNumbers[i], manager.GetColorForIndex(colorIndex++));

                    if (i < rightNumbers.Count - 1)
                    {
                        var plusGo = Instantiate(plusPrefab, rightContainer);
                        plusGo.name = "right_plus";
                        ConfigureTextSymbol(plusGo, "+");
                    }
                }
                else
                {
                    // Spawn objects directly under rightContainer (object mode)
                    for (int j = 0; j < rightNumbers[i]; j++)
                    {
                        var prefabToUse = rightObjectPrefab != null ? rightObjectPrefab : manager.RightObjectPrefab;
                        var obj = Instantiate(prefabToUse, rightContainer);
                        if (obj.GetComponent<CountingObject>() == null)
                        {
                            obj.AddComponent<CountingObject>();
                        }
                    }
                }
            }

            // Force layout rebuild
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            if (leftContainer != transform) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(leftContainer as RectTransform);
            if (rightContainer != transform) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rightContainer as RectTransform);
        }

        private void ConfigureContainerLayout(Transform container)
        {
            if (container == transform) return;

            var glg = container.GetComponent<GridLayoutGroup>();
            if (glg != null) DestroyImmediate(glg);
            var vlg = container.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);

            var hlg = container.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = 15f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
        }



        private void ConfigureNumberBox(GameObject go, int val, Color color)
        {
            var card = go.GetComponent<AnswerCard>();
            if (card != null)
            {
                Destroy(card);
            }

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
