using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KidGame.Mechanics.Tracing
{
    public class TracingModeManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Slot Prefab")]
        [Tooltip("The slot prefab that holds a SlotTracer with the shape prefab assigned.")]
        [SerializeField] private GameObject slotPrefab;
        [Tooltip("How many slot instances to spawn in each content container.")]
        [SerializeField, Range(1, 12)] private int slotCount = 6;

        [Header("Dynamic Slots")]
        [Tooltip("Optional list of numbers (e.g. '123') or word phrases (e.g. 'one hundred forty eight') to trace dynamically. If empty, defaults to spawning the editor prefab's shape slotCount times.")]
        [SerializeField] private List<string> valuesToTrace = new List<string>();

        [Header("Character Prefabs Lookup")]
        [SerializeField] private List<GameObject> numberPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> lowercasePrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> uppercasePrefabs = new List<GameObject>();

        [Header("Portrait – Content Containers")]
        [SerializeField] private Transform portraitTutorialContent;
        [SerializeField] private Transform portraitGameContent;

        [Header("Landscape – Content Containers")]
        [SerializeField] private Transform landscapeTutorialContent;
        [SerializeField] private Transform landscapeGameContent;

        [Header("Portrait – Mode GameObjects")]
        [SerializeField] private GameObject portraitTutorialMode;
        [SerializeField] private GameObject portraitGameMode;

        [Header("Landscape – Mode GameObjects")]
        [SerializeField] private GameObject landscapeTutorialMode;
        [SerializeField] private GameObject landscapeGameMode;

        [Header("Portrait – Tutorial Objective UI")]
        [Tooltip("Example TMP — shows the character large  e.g.  0")]
        [SerializeField] private TMP_Text portraitExampleText;
        [Tooltip("Description TMP — shows: Let's practice writing \"zero\", \"0\"")]
        [SerializeField] private TMP_Text portraitDescriptionText;

        [Header("Landscape – Tutorial Objective UI")]
        [Tooltip("Example TMP — shows the character large  e.g.  0")]
        [SerializeField] private TMP_Text landscapeExampleText;
        [Tooltip("Description TMP — shows: Let's practice writing \"zero\", \"0\"")]
        [SerializeField] private TMP_Text landscapeDescriptionText;

        [Header("Test")]
        [Tooltip("Flip this in the Inspector at runtime to switch modes instantly.")]
        [SerializeField] private bool tutorialModeActive = true;

        // ── Private state ─────────────────────────────────────────────────────

        private string _exampleText;
        private string _descriptionWord;
        private string _descriptionNumber;
        private string _customSentence;

        private List<List<SlotTracer>> _portraitRowTracers = new List<List<SlotTracer>>();
        private List<List<SlotTracer>> _landscapeRowTracers = new List<List<SlotTracer>>();
        private bool _lastIsLandscape;
        private float _portraitMinScale = 1f;
        private float _landscapeMinScale = 1f;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            if (slotPrefab == null)
            {
                Debug.LogError("[TracingModeManager] Slot Prefab is not assigned.");
                yield break;
            }

            _portraitRowTracers.Clear();
            _landscapeRowTracers.Clear();

            // Read display info from the prefab's SlotTracer BEFORE instantiating
            var prefabTracer = slotPrefab.GetComponent<SlotTracer>();
            if (prefabTracer != null)
            {
                _exampleText       = prefabTracer.exampleText;
                _descriptionWord   = prefabTracer.descriptionWord;
                _descriptionNumber = prefabTracer.descriptionNumber;
                _customSentence    = prefabTracer.customDescriptionSentence;
            }

            // Wait 1 frame so Canvas layout dimensions are calculated
            yield return null;

            // Spawn into all four containers
            SpawnInto(portraitTutorialContent, _portraitRowTracers);
            SpawnInto(portraitGameContent, _portraitRowTracers);
            SpawnInto(landscapeTutorialContent, _landscapeRowTracers);
            SpawnInto(landscapeGameContent, _landscapeRowTracers);

            // Wait for SlotTracer.Start() coroutines to finish spawning shapes:
            //   Frame 1: yield → SpawnShape
            //   Frame 2: yield → ColorizeStartDots
            yield return null;
            yield return null;

            // Calculate uniform scales for Portrait and Landscape independently
            _portraitMinScale = FindMinScale(_portraitRowTracers);
            _landscapeMinScale = FindMinScale(_landscapeRowTracers);

            // Apply the uniform scales
            ApplyAllUniformScales();

            // Apply starting mode and populate objective text
            ApplyMode(tutorialModeActive);

            // Initialize orientation
            _lastIsLandscape = Screen.width > Screen.height;

            // Perform initial sequencing update
            UpdateAllSequencing();
        }

        private void Update()
        {
            bool currentIsLandscape = Screen.width > Screen.height;
            if (currentIsLandscape != _lastIsLandscape)
            {
                _lastIsLandscape = currentIsLandscape;
                StartCoroutine(UpdateAllSequencingDelayed());
            }
        }

        private IEnumerator UpdateAllSequencingDelayed()
        {
            // Wait for 4 frames (matching orientation transition timing in TracingOrientationAdapter)
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            UpdateAllSequencing();
            ApplyAllUniformScales();
        }

        private void UpdateAllSequencing()
        {
            foreach (var list in _portraitRowTracers)
                UpdateSequencingState(list);

            foreach (var list in _landscapeRowTracers)
                UpdateSequencingState(list);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetTutorialMode()   { tutorialModeActive = true;  ApplyMode(true);  }
        public void SetGameMode()       { tutorialModeActive = false; ApplyMode(false); }
        public void ToggleMode()        { tutorialModeActive = !tutorialModeActive; ApplyMode(tutorialModeActive); }

        // ── Mode Switch ───────────────────────────────────────────────────────

        private void ApplyMode(bool isTutorial)
        {
            // Show / hide mode root GameObjects
            if (portraitTutorialMode)  portraitTutorialMode .SetActive(isTutorial);
            if (portraitGameMode)      portraitGameMode     .SetActive(!isTutorial);
            if (landscapeTutorialMode) landscapeTutorialMode.SetActive(isTutorial);
            if (landscapeGameMode)     landscapeGameMode    .SetActive(!isTutorial);

            // Update objective panel if switching into tutorial
            if (isTutorial) RefreshObjective();
        }

        // ── Objective UI ──────────────────────────────────────────────────────

        private void RefreshObjective()
        {
            // If in edit mode and fields are not initialized, read from prefab
            if (!Application.isPlaying && string.IsNullOrEmpty(_descriptionWord) && slotPrefab != null)
            {
                var prefabTracer = slotPrefab.GetComponent<SlotTracer>();
                if (prefabTracer != null)
                {
                    _exampleText       = prefabTracer.exampleText;
                    _descriptionWord   = prefabTracer.descriptionWord;
                    _descriptionNumber = prefabTracer.descriptionNumber;
                    _customSentence    = prefabTracer.customDescriptionSentence;
                }
            }
            // Build description: use custom sentence if provided, otherwise auto-build
            string desc = !string.IsNullOrWhiteSpace(_customSentence)
                ? _customSentence
                : $"Let's practice writing {_descriptionWord} \"{_descriptionNumber}\"";

            // Portrait
            if (portraitExampleText)     portraitExampleText.text     = _exampleText;
            if (portraitDescriptionText) portraitDescriptionText.text = desc;

            // Landscape
            if (landscapeExampleText)     landscapeExampleText.text     = _exampleText;
            if (landscapeDescriptionText) landscapeDescriptionText.text = desc;
        }

        private float GetContainerWidth(Transform container)
        {
            if (container == null) return 800f;

            // Traverse up to check if we are in Landscape or Portrait mode
            bool isLandscape = false;
            Transform curr = container;
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

            var rt = container.GetComponent<RectTransform>();
            if (rt != null)
            {
                float w = rt.rect.width;
                if (w > 100f) return w;
            }

            return isLandscape ? 1200f : 800f;
        }

        private int GetMaxWordLengthInSpawnList()
        {
            int maxLen = 0;
            if (valuesToTrace == null || valuesToTrace.Count == 0) return slotCount;

            foreach (var val in valuesToTrace)
            {
                if (string.IsNullOrWhiteSpace(val)) continue;
                string[] words = val.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in words)
                {
                    string clean = FilterWord(w);
                    if (string.IsNullOrEmpty(clean)) continue;
                    if (clean.Equals("and", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (clean.Length > maxLen)
                    {
                        maxLen = clean.Length;
                    }
                }
            }
            return maxLen > 0 ? maxLen : slotCount;
        }

        private float GetCalculatedCellSize(float containerWidth, int maxWordLen)
        {
            if (maxWordLen <= 0) return 250f;
            float maxSlotWidth = containerWidth - 80f;
            // C * L + (L - 1) * (-10f) = maxSlotWidth
            // C = (maxSlotWidth + (maxWordLen - 1) * 10f) / maxWordLen
            float C = (maxSlotWidth + (maxWordLen - 1) * 10f) / maxWordLen;
            if (C < 30f) C = 30f;
            if (C > 250f) C = 250f; // Cap cell size at 250 to avoid overly large single digits
            return C;
        }

        private Transform FindContainer(Transform tracerTransform)
        {
            Transform curr = tracerTransform;
            while (curr != null)
            {
                if (curr.name == "Content")
                {
                    return curr;
                }
                curr = curr.parent;
            }
            return tracerTransform.parent; // fallback
        }

        private void SpawnInto(Transform container, List<List<SlotTracer>> rowTracersList)
        {
            if (container == null) return;

            // Clear any existing children first
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                SafeDestroy(container.GetChild(i).gameObject);
            }

            // Fallback: If no values to trace are provided, use standard slotPrefab instantiation
            if (valuesToTrace == null || valuesToTrace.Count == 0)
            {
                for (int i = 0; i < slotCount; i++)
                {
                    var go = Instantiate(slotPrefab, container);
                    var tracer = go.GetComponent<SlotTracer>();
                    if (tracer != null)
                    {
                        rowTracersList.Add(new List<SlotTracer> { tracer });
                    }
                }
                return;
            }

            // Adjust layout of the container
            AdjustContainerLayout(container);

            float availableContainerWidth = GetContainerWidth(container);
            int maxWordLen = GetMaxWordLengthInSpawnList();
            float C = GetCalculatedCellSize(availableContainerWidth, maxWordLen);
            float maxSlotWidth = availableContainerWidth - 80f;

            foreach (var val in valuesToTrace)
            {
                if (string.IsNullOrWhiteSpace(val)) continue;

                // Create a parent EntryGroup GameObject under the container
                GameObject entryGroupGo = new GameObject($"EntryGroup_{val.Trim()}", typeof(RectTransform));
                entryGroupGo.transform.SetParent(container, false);

                // Add layout depending on scroll direction
                var scrollRect = container.GetComponentInParent<ScrollRect>();
                bool isVertical = scrollRect != null ? scrollRect.vertical : true;

                if (isVertical)
                {
                    var vlg = entryGroupGo.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 10f; // Small spacing between slot rows of the same entry/phrase
                    vlg.childAlignment = TextAnchor.UpperLeft;
                    vlg.childControlWidth = false;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = false;
                    vlg.childForceExpandHeight = false;
                }
                else
                {
                    var hlg = entryGroupGo.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 10f; // Small spacing
                    hlg.childAlignment = TextAnchor.MiddleLeft;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                }

                var groupLe = entryGroupGo.AddComponent<LayoutElement>();
                var groupRt = entryGroupGo.GetComponent<RectTransform>();

                bool isNumber = IsDigitsOnly(val.Trim());

                if (isNumber)
                {
                    SpawnNumberSlot(entryGroupGo.transform, val.Trim(), rowTracersList, maxSlotWidth, C);

                    if (isVertical)
                    {
                        groupLe.preferredWidth = maxSlotWidth;
                        groupLe.preferredHeight = C;
                        groupRt.sizeDelta = new Vector2(maxSlotWidth, C);
                    }
                    else
                    {
                        groupLe.preferredWidth = maxSlotWidth;
                        groupLe.preferredHeight = C;
                        groupRt.sizeDelta = new Vector2(maxSlotWidth, C);
                    }
                }
                else
                {
                    string[] words = val.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length == 0) continue;

                    List<string> filteredWords = new List<string>();
                    foreach (var w in words)
                    {
                        string cleanWord = FilterWord(w);
                        if (string.IsNullOrEmpty(cleanWord)) continue;
                        if (cleanWord.Equals("and", System.StringComparison.OrdinalIgnoreCase)) continue;

                        filteredWords.Add(cleanWord);
                    }

                    if (filteredWords.Count == 0) continue;

                    foreach (var word in filteredWords)
                    {
                        SpawnWordSlot(entryGroupGo.transform, word, maxSlotWidth, C, rowTracersList);
                    }

                    int numSlots = filteredWords.Count;
                    if (isVertical)
                    {
                        float totalHeight = numSlots * C + Mathf.Max(0, numSlots - 1) * 10f;
                        groupLe.preferredWidth = maxSlotWidth;
                        groupLe.preferredHeight = totalHeight;
                        groupRt.sizeDelta = new Vector2(maxSlotWidth, totalHeight);
                    }
                    else
                    {
                        float totalWidth = numSlots * maxSlotWidth + Mathf.Max(0, numSlots - 1) * 10f;
                        groupLe.preferredWidth = totalWidth;
                        groupLe.preferredHeight = C;
                        groupRt.sizeDelta = new Vector2(totalWidth, C);
                    }
                }
            }
        }

        private void SpawnNumberSlot(Transform container, string number, List<List<SlotTracer>> rowTracersList, float maxSlotWidth, float cellSize)
        {
            GameObject rowGo = Instantiate(slotPrefab, container);
            rowGo.name = $"Slot_Number_{number}";

            // Since this slotPrefab instance serves as the parent container for the character cells,
            // we remove the SlotTracer component from it (the character cells themselves will have SlotTracers).
            var tracer = rowGo.GetComponent<SlotTracer>();
            if (tracer != null)
            {
                SafeDestroy(tracer);
            }

            RectTransform rowRt = rowGo.GetComponent<RectTransform>();
            if (rowRt != null)
            {
                rowRt.sizeDelta = new Vector2(maxSlotWidth, cellSize);
            }

            var le = rowGo.GetComponent<LayoutElement>();
            if (le == null) le = rowGo.AddComponent<LayoutElement>();
            le.preferredWidth = maxSlotWidth;
            le.preferredHeight = cellSize;

            PopulateSlotRowCharacters(rowGo, number, true, cellSize, rowTracersList);
        }

        private void SpawnWordSlot(Transform container, string word, float maxSlotWidth, float cellSize, List<List<SlotTracer>> rowTracersList)
        {
            GameObject rowGo = Instantiate(slotPrefab, container);
            rowGo.name = $"Slot_Word_{word}";

            // Since this slotPrefab instance serves as the parent container for the character cells,
            // we remove the SlotTracer component from it (the character cells themselves will have SlotTracers).
            var tracer = rowGo.GetComponent<SlotTracer>();
            if (tracer != null)
            {
                SafeDestroy(tracer);
            }

            RectTransform rowRt = rowGo.GetComponent<RectTransform>();
            if (rowRt != null)
            {
                rowRt.sizeDelta = new Vector2(maxSlotWidth, cellSize);
            }

            var le = rowGo.GetComponent<LayoutElement>();
            if (le == null) le = rowGo.AddComponent<LayoutElement>();
            le.preferredWidth = maxSlotWidth;
            le.preferredHeight = cellSize;

            PopulateSlotRowCharacters(rowGo, word, false, cellSize, rowTracersList);
        }

        private void PopulateSlotRowCharacters(GameObject rowGo, string characters, bool isDigit, float cellSize, List<List<SlotTracer>> rowTracersList)
        {
            // Set spacing to -20f if the entry is a multi-digit number, otherwise 0f
            float spacing = (isDigit && characters.Length > 1) ? -20f : 0f;

            // Locate target parent (TracerContainer if it exists in the slotPrefab hierarchy, otherwise fallback to rowGo itself)
            Transform targetParent = rowGo.transform.Find("TracerContainer");
            if (targetParent == null)
            {
                targetParent = rowGo.transform;
            }
            else
            {
                // If using TracerContainer, set its RectTransform anchors to stretch and fill the parent rowGo
                var tpRt = targetParent.GetComponent<RectTransform>();
                if (tpRt != null)
                {
                    tpRt.anchorMin = Vector2.zero;
                    tpRt.anchorMax = Vector2.one;
                    tpRt.anchoredPosition = Vector2.zero;
                    tpRt.sizeDelta = Vector2.zero;
                }
            }

            // Remove any other conflicting layout components from rowGo if we are using TracerContainer
            if (targetParent != rowGo.transform)
            {
                var rowGrid = rowGo.GetComponent<GridLayoutGroup>();
                if (rowGrid != null) SafeDestroy(rowGrid);

                var rowHlg = rowGo.GetComponent<HorizontalLayoutGroup>();
                if (rowHlg != null) SafeDestroy(rowHlg);

                var rowVlg = rowGo.GetComponent<VerticalLayoutGroup>();
                if (rowVlg != null) SafeDestroy(rowVlg);
            }

            // Set up HorizontalLayoutGroup on targetParent
            var existingGrid = targetParent.GetComponent<GridLayoutGroup>();
            if (existingGrid != null)
            {
                SafeDestroy(existingGrid);
            }
            var existingVlg = targetParent.GetComponent<VerticalLayoutGroup>();
            if (existingVlg != null)
            {
                SafeDestroy(existingVlg);
            }

            HorizontalLayoutGroup hlg = targetParent.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = targetParent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = spacing;

            // Remove/Disable Masks from rowGo and targetParent to avoid clipping paths or hand guides
            var rowMask = rowGo.GetComponent<Mask>();
            if (rowMask != null) rowMask.enabled = false;

            var rowRectMask = rowGo.GetComponent<RectMask2D>();
            if (rowRectMask != null) rowRectMask.enabled = false;

            if (targetParent != rowGo.transform)
            {
                var tpMask = targetParent.GetComponent<Mask>();
                if (tpMask != null) tpMask.enabled = false;

                var tpRectMask = targetParent.GetComponent<RectMask2D>();
                if (tpRectMask != null) tpRectMask.enabled = false;
            }

            List<SlotTracer> subTracers = new List<SlotTracer>();

            foreach (char c in characters)
            {
                var shapePrefab = GetPrefabForCharacter(c);
                if (shapePrefab == null)
                {
                    Debug.LogWarning($"[TracingModeManager] No shape prefab found for character '{c}'");
                    continue;
                }

                // Create a container GameObject for the character shape
                GameObject cellGo = new GameObject($"Cell_{shapePrefab.name}", typeof(RectTransform));
                cellGo.transform.SetParent(targetParent, false);

                // Set cell container size statically to cellSize x cellSize
                RectTransform cellRt = cellGo.GetComponent<RectTransform>();
                if (cellRt != null)
                {
                    cellRt.sizeDelta = new Vector2(cellSize, cellSize);
                }

                var cellLe = cellGo.AddComponent<LayoutElement>();
                cellLe.preferredWidth = cellSize;
                cellLe.preferredHeight = cellSize;

                // Add SlotTracer component to the cell container
                var cellTracer = cellGo.AddComponent<SlotTracer>();
                if (cellTracer != null)
                {
                    cellTracer.ShapePrefab = shapePrefab;
                    cellTracer.SizePadding = 0.05f; // Reduce size padding to maximize character scale
                    
                    subTracers.Add(cellTracer);
                }
            }

            foreach (var cellTracer in subTracers)
            {
                cellTracer.EnsureShapeSpawned();
                cellTracer.RescaleShape();
            }

            // Add the subTracers list to the row tracking system
            rowTracersList.Add(subTracers);

            // Set up complete listener
            foreach (var childTracer in subTracers)
            {
                childTracer.OnCompletedEvent.AddListener(() => {
                    UpdateSequencingState(subTracers);
                });
            }
        }


        // ── Layout Helpers ────────────────────────────────────────────────────

        private void AdjustContainerLayout(Transform container)
        {
            var scrollRect = container.GetComponentInParent<ScrollRect>();
            bool isVertical = scrollRect != null ? scrollRect.vertical : true;

            var grid = container.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                var spacing = grid.spacing;
                var alignment = grid.childAlignment;

                SafeDestroy(grid);

                if (isVertical)
                {
                    var vlg = container.gameObject.GetComponent<VerticalLayoutGroup>();
                    if (vlg == null) vlg = container.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.spacing = 35f; // Large spacing between different entries
                    vlg.childAlignment = TextAnchor.UpperLeft;
                    vlg.childControlWidth = false;
                    vlg.childControlHeight = false;
                    vlg.childForceExpandWidth = false;
                    vlg.childForceExpandHeight = false;
                }
                else
                {
                    var hlg = container.gameObject.GetComponent<HorizontalLayoutGroup>();
                    if (hlg == null) hlg = container.gameObject.AddComponent<HorizontalLayoutGroup>();
                    hlg.spacing = 35f; // Large spacing between different entries
                    hlg.childAlignment = TextAnchor.MiddleLeft;
                    hlg.childControlWidth = false;
                    hlg.childControlHeight = false;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                }
            }

            // Ensure ContentSizeFitter is present and correctly configured to prevent scroll snap back
            var csf = container.gameObject.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = container.gameObject.AddComponent<ContentSizeFitter>();
            if (isVertical)
            {
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }


        private void UpdateSequencingState(List<SlotTracer> subTracers)
        {
            if (subTracers == null || subTracers.Count == 0) return;

            bool foundActive = false;
            for (int i = 0; i < subTracers.Count; i++)
            {
                var tracer = subTracers[i];
                if (tracer == null) continue;

                tracer.EnsureShapeSpawned();

                if (!tracer.IsCompleted && !foundActive)
                {
                    SetTracerCollidersActive(tracer, true);
                    foundActive = true;

                    if (tracer.shape != null)
                    {
                        int currentPathIdx = tracer.shape.GetCurrentPathIndex();
                        tracer.shape.ShowPathNumbers(currentPathIdx);
                        tracer.ColorizeStartDots();

                        tracer.shape.CancelInvoke("EnableTracingHand");
                        tracer.shape.Invoke("EnableTracingHand", 0.5f);
                    }
                }
                else
                {
                    SetTracerCollidersActive(tracer, false);

                    if (tracer.shape != null)
                    {
                        tracer.shape.CancelInvoke("EnableTracingHand");
                        tracer.shape.DisableTracingHand();

                        if (!tracer.IsCompleted)
                        {
                            tracer.shape.ShowPathNumbers(-1);
                        }
                    }
                }
            }
        }

        private void SetTracerCollidersActive(SlotTracer tracer, bool active)
        {
            if (tracer == null) return;
            var colliders = tracer.GetComponentsInChildren<Collider2D>(true);
            foreach (var col in colliders)
            {
                col.enabled = active;
            }
        }

        // ── Prefab Lookup ─────────────────────────────────────────────────────

        private GameObject GetPrefabForCharacter(char c)
        {
            if (char.IsDigit(c))
            {
                int digit = c - '0';
                string searchName = $"{digit}-Number";
                foreach (var p in numberPrefabs)
                {
                    if (p != null && p.name == searchName)
                        return p;
                }
            }
            else if (char.IsLower(c))
            {
                string searchName = $"{c}-Letter";
                foreach (var p in lowercasePrefabs)
                {
                    if (p != null && p.name == searchName)
                        return p;
                }
            }
            else if (char.IsUpper(c))
            {
                string searchName = $"{c}-Letter";
                foreach (var p in uppercasePrefabs)
                {
                    if (p != null && p.name == searchName)
                        return p;
                }
            }
            return null;
        }

        private bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }

        private string FilterWord(string word)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in word)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private float FindMinScale(List<List<SlotTracer>> rowList)
        {
            float minScale = float.MaxValue;
            bool foundAny = false;

            foreach (var row in rowList)
            {
                foreach (var tracer in row)
                {
                    if (tracer == null) continue;
                    tracer.EnsureShapeSpawned();

                    if (tracer.shape != null)
                    {
                        var slotRT = tracer.GetComponent<RectTransform>();
                        var shapeRT = tracer.shape.GetComponent<RectTransform>();

                        if (slotRT != null && shapeRT != null)
                        {
                            // Use sizeDelta to avoid layout rebuild lag and inactive hierarchy issues
                            Vector2 slotSize = slotRT.sizeDelta;
                            if (slotSize.x <= 0f || slotSize.y <= 0f)
                            {
                                slotSize = slotRT.rect.size;
                            }
                            Vector2 shapeSize = shapeRT.rect.size;

                            if (slotSize.sqrMagnitude > 0f && shapeSize.sqrMagnitude > 0f)
                            {
                                float scale = Mathf.Min(slotSize.x / shapeSize.x,
                                                        slotSize.y / shapeSize.y)
                                              * (1f - tracer.SizePadding);
                                if (scale > 0f)
                                {
                                    Debug.Log($"[FindMinScale] tracer: {tracer.name}, slotSize: {slotSize}, shapeSize: {shapeSize}, calculated scale: {scale}, sizePadding: {tracer.SizePadding}");
                                    minScale = Mathf.Min(minScale, scale);
                                    foundAny = true;
                                }
                            }
                        }
                    }
                }
            }

            return foundAny ? minScale : 1f;
        }

        private void ApplyUniformScale(List<List<SlotTracer>> rowList, float scale)
        {
            if (scale <= 0f || scale == float.MaxValue) return;
            HashSet<RectTransform> parentsToRebuild = new HashSet<RectTransform>();

            // Find the cell size C for this rowList
            float C = 250f;
            if (rowList.Count > 0 && rowList[0].Count > 0 && rowList[0][0] != null)
            {
                var firstTracer = rowList[0][0];
                var container = FindContainer(firstTracer.transform);
                if (container != null)
                {
                    float containerWidth = GetContainerWidth(container);
                    int maxWordLen = GetMaxWordLengthInSpawnList();
                    C = GetCalculatedCellSize(containerWidth, maxWordLen);
                }
            }

            float scaledSize = C;


            // Step 1: Scale shapes and resize cells (width + height to scaledSize)
            foreach (var row in rowList)
            {
                foreach (var tracer in row)
                {
                    if (tracer != null)
                    {
                        if (tracer.shape != null)
                        {
                            tracer.shape.transform.localScale = Vector3.one * scale;
                        }

                        var cellRt = tracer.GetComponent<RectTransform>();
                        if (cellRt != null)
                        {
                            cellRt.sizeDelta = new Vector2(scaledSize, scaledSize);

                            var cellLe = cellRt.GetComponent<LayoutElement>();
                            if (cellLe != null)
                            {
                                cellLe.preferredWidth = scaledSize;
                                cellLe.preferredHeight = scaledSize;
                            }

                            var parentRt = cellRt.parent as RectTransform;
                            if (parentRt != null)
                            {
                                parentsToRebuild.Add(parentRt);
                            }
                        }
                    }
                }
            }

            // Step 2: Scale HorizontalLayoutGroup character spacing and resize the slot rows
            foreach (var row in rowList)
            {
                if (row.Count == 0 || row[0] == null) continue;
                var cellRt = row[0].GetComponent<RectTransform>();
                if (cellRt == null) continue;

                var containerRt = cellRt.parent as RectTransform;
                if (containerRt == null) continue;

                var hlg = containerRt.GetComponent<HorizontalLayoutGroup>();

                // Find the row root (which starts with "Slot_") by walking up
                RectTransform rowRt = null;
                Transform curr = cellRt.parent;
                while (curr != null)
                {
                    if (curr.name.StartsWith("Slot_"))
                    {
                        rowRt = curr as RectTransform;
                        break;
                    }
                    curr = curr.parent;
                }

                if (rowRt != null)
                {
                    // Find the dynamic spacing for this specific row based on its name (multi-digit check)
                    float rowSpacing = 0f;
                    if (rowRt.name.StartsWith("Slot_Number_"))
                    {
                        string numPart = rowRt.name.Substring("Slot_Number_".Length);
                        if (numPart.Length > 1)
                        {
                            rowSpacing = -20f;
                        }
                    }
                    float scaledSpacing = rowSpacing * scale;

                    if (hlg != null)
                    {
                        hlg.spacing = scaledSpacing;
                    }

                    int cellCount = row.Count;
                    float totalWidth = cellCount * scaledSize + Mathf.Max(0, cellCount - 1) * scaledSpacing;

                    rowRt.sizeDelta = new Vector2(totalWidth, scaledSize);

                    var rowLe = rowRt.GetComponent<LayoutElement>();
                    if (rowLe != null)
                    {
                        rowLe.preferredWidth = totalWidth;
                        rowLe.preferredHeight = scaledSize;
                    }

                    var entryGroupRt = rowRt.parent as RectTransform;
                    if (entryGroupRt != null)
                    {
                        parentsToRebuild.Add(entryGroupRt);
                    }
                }
            }

            // Step 3: Recalculate unique EntryGroup dimensions based on their child slots
            HashSet<RectTransform> entryGroups = new HashSet<RectTransform>();
            foreach (var row in rowList)
            {
                if (row.Count > 0 && row[0] != null)
                {
                    var cellRt = row[0].GetComponent<RectTransform>();
                    if (cellRt != null)
                    {
                        RectTransform rowRt = null;
                        Transform curr = cellRt.parent;
                        while (curr != null)
                        {
                            if (curr.name.StartsWith("Slot_"))
                            {
                                rowRt = curr as RectTransform;
                                break;
                            }
                            curr = curr.parent;
                        }

                        if (rowRt != null && rowRt.parent != null)
                        {
                            var entryGroupRt = rowRt.parent as RectTransform;
                            if (entryGroupRt != null)
                            {
                                entryGroups.Add(entryGroupRt);
                            }
                        }
                    }
                }
            }

            foreach (var entryGroupRt in entryGroups)
            {
                if (entryGroupRt == null) continue;

                int childCount = 0;
                foreach (Transform child in entryGroupRt)
                {
                    if (child.name.StartsWith("Slot_"))
                    {
                        childCount++;
                    }
                }

                var groupLe = entryGroupRt.GetComponent<LayoutElement>();
                var scrollRect = entryGroupRt.GetComponentInParent<ScrollRect>();
                bool isVertical = scrollRect != null ? scrollRect.vertical : true;

                if (isVertical)
                {
                    float totalHeight = childCount * scaledSize + Mathf.Max(0, childCount - 1) * 10f;
                    entryGroupRt.sizeDelta = new Vector2(entryGroupRt.sizeDelta.x, totalHeight);
                    if (groupLe != null)
                    {
                        groupLe.preferredHeight = totalHeight;
                    }
                }
                else
                {
                    float totalWidth = 0f;
                    foreach (Transform child in entryGroupRt)
                    {
                        if (child.name.StartsWith("Slot_"))
                        {
                            var childRt = child as RectTransform;
                            if (childRt != null)
                            {
                                totalWidth += childRt.sizeDelta.x;
                            }
                        }
                    }
                    totalWidth += Mathf.Max(0, childCount - 1) * 10f;
                    entryGroupRt.sizeDelta = new Vector2(totalWidth, scaledSize);
                    if (groupLe != null)
                    {
                        groupLe.preferredWidth = totalWidth;
                        groupLe.preferredHeight = scaledSize;
                    }
                }
            }

            // Step 4: Force rebuild layout hierarchies to apply all updates instantly
            foreach (var parentRt in parentsToRebuild)
            {
                if (parentRt != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                }
            }
        }


        private void ApplyAllUniformScales()
        {
            ApplyUniformScale(_portraitRowTracers, _portraitMinScale);
            ApplyUniformScale(_landscapeRowTracers, _landscapeMinScale);
        }

        private void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (obj is Component)
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
            else if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        // ── Editor Helpers ────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEditor.EditorApplication.isCompiling) return;

            FindCharacterPrefabs();
            ApplyMode(tutorialModeActive);
        }

        [ContextMenu("Switch to Tutorial Mode")]
        private void EditorSetTutorial() { tutorialModeActive = true;  ApplyMode(true);  }

        [ContextMenu("Switch to Game Mode")]
        private void EditorSetGame()     { tutorialModeActive = false; ApplyMode(false); }

        [ContextMenu("Find Character Prefabs")]
        private void FindCharacterPrefabs()
        {
            numberPrefabs = new List<GameObject>();
            for (int i = 0; i <= 9; i++)
            {
                var path = $"Assets/Art/Tracing/Prefabs/Numbers/{i}-Number.prefab";
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) numberPrefabs.Add(prefab);
            }

            lowercasePrefabs = new List<GameObject>();
            for (char c = 'a'; c <= 'z'; c++)
            {
                var path = $"Assets/Art/Tracing/Prefabs/Lowercase/{c}-Letter.prefab";
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) lowercasePrefabs.Add(prefab);
            }

            uppercasePrefabs = new List<GameObject>();
            for (char c = 'A'; c <= 'Z'; c++)
            {
                var path = $"Assets/Art/Tracing/Prefabs/Uppercase/{c}-Letter.prefab";
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) uppercasePrefabs.Add(prefab);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"Loaded {numberPrefabs.Count} numbers, {lowercasePrefabs.Count} lowercase, {uppercasePrefabs.Count} uppercase prefabs.");
        }
#endif
    }
}
