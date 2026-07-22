using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KidGame.Mechanics.Counting;
using KidGame.Interface;

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
        [Tooltip("Ratio of cell height to use as top padding inside each slot row.")]
        [SerializeField, Range(0f, 0.3f)] private float slotTopPaddingRatio = 0.08f;

        [Header("Scroll Disabled Layout Settings")]
        [Tooltip("If true, scrolling is disabled and a custom layout is applied to fit elements in the viewport.")]
        [SerializeField] private bool disableScrollingLayout = false;
        [Tooltip("The number of characters/objects to spawn (1, 2, or 4) when scrolling is disabled.")]
        [SerializeField, Range(1, 4)] private int customSpawnCount = 1;

        [Header("Spell Mode Settings")]
        [Tooltip("If true, Spell Mode is active: the numerals appear fully traced, and kids must spell out the number names.")]
        [SerializeField] private bool spellModeActive = false;
        [Tooltip("Draggable AnswerCard prefab used to represent letter cards (shared for both orientations).")]
        [SerializeField] private GameObject spellAnswerCardPrefab;
        [Tooltip("AnswerDropZone prefab used to represent letter drop slots (shared for both orientations).")]
        [SerializeField] private GameObject spellDropZonePrefab;
        [Tooltip("Custom slot prefab used for Spell Mode (e.g. Spell Slot.prefab). Falls back to slotPrefab if unassigned.")]
        [SerializeField] private GameObject spellSlotPrefab;

        [Header("Spell Mode - Continue Button")]
        [Tooltip("Continue / Next button. Locked until spelling is complete.")]
        [SerializeField] private Button continueButton;

        [Header("Spell Mode - Answer Grid")]
        [Tooltip("The answer card tray / spelling UI root. Assign the root GameObject inside this prefab.")]
        [SerializeField] private GameObject answerGrid;

        [Header("Spell Mode - Answer Cards Container")]
        [Tooltip("Container where answer cards spawn for dragging.")]
        [SerializeField] private Transform answerCardsContainer;

        [Header("Dynamic Slots")]
        [Tooltip("Optional list of numbers to trace dynamically.")]
        [SerializeField] private List<string> valuesToTrace = new List<string>();

        [Header("Character Prefabs Lookup")]
        [SerializeField] private List<GameObject> numberPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> lowercasePrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> uppercasePrefabs = new List<GameObject>();

        [Header("Content Containers")]
        [SerializeField] private Transform tutorialContent;
        [SerializeField] private Transform gameContent;

        [Header("Mode GameObjects")]
        [SerializeField] private GameObject tutorialModeGo;
        [SerializeField] private GameObject gameModeGo;

        [Header("Tutorial Objective UI")]
        [Tooltip("Example TMP - shows the character large e.g. 0")]
        [SerializeField] private TMP_Text exampleText;
        [Tooltip("Description TMP - shows the description")]
        [SerializeField] private TMP_Text descriptionText;

        [Header("Test")]
        [Tooltip("Flip this in the Inspector at runtime to switch modes instantly.")]
        [SerializeField] private bool tutorialModeActive = true;

        // Private state

        private string _exampleText;
        private string _descriptionWord;
        private string _descriptionNumber;
        private string _customSentence;

        private List<List<SlotTracer>> _rowTracers = new List<List<SlotTracer>>();
        private float _minScale = 1f;

        public Button ContinueButton => continueButton;
        public GameObject AnswerGrid => answerGrid;

        public void Configure(bool spellModeActive, List<string> valuesToTrace, int customSpawnCount)
        {
            this.spellModeActive = spellModeActive;
            this.valuesToTrace = new List<string>(valuesToTrace);
            this.customSpawnCount = customSpawnCount;

            // Clear old tracers immediately to prevent premature completion checks
            _rowTracers.Clear();
            
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            StartCoroutine(InitializeRoundRoutine());
        }

        private IEnumerator InitializeRoundRoutine()
        {
            if (slotPrefab == null)
            {
                Debug.LogError("[TracingModeManager] Slot Prefab is not assigned.");
                yield break;
            }

            _rowTracers.Clear();
            

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
            Canvas.ForceUpdateCanvases();

            // Spawn into this orientation's tutorial and game containers.
            // These now run as coroutines that yield between rows, so the heavy
            // Instantiate/layout work for a level is spread across several frames
            // instead of happening in one blocking burst during scene activation
            // (the burst was invisible on desktop but caused visible stutter on
            // lower-end Android devices, including freezing the loading animation).
            yield return SpawnInto(tutorialContent, _rowTracers);
            yield return SpawnInto(gameContent, _rowTracers);


            // Wait for SlotTracer.Start() coroutines to finish spawning shapes:
            //   Frame 1: yield → SpawnShape
            //   Frame 2: yield → ColorizeStartDots
            yield return null;
            yield return null;

            // Calculate uniform scales for Portrait and Landscape independently
            _minScale = FindMinScale(_rowTracers);


            // Apply the uniform scales
            ApplyAllUniformScales();

            // Apply starting mode and populate objective text
            ApplyMode(tutorialModeActive);

            // Initialize orientation

            // Lock continue buttons at start until tracing/spelling tasks are completed (unless managed by GameFlowManager)
            if (continueButton != null) continueButton.interactable = (KidGame.Interface.GameFlowManager.Instance != null);


            // Perform initial sequencing update
            UpdateAllSequencing();

            if (spellModeActive)
            {
                StartCoroutine(ForceSpellModeCompletionRoutine());
            }
            else
            {
                CheckNormalModeComplete();
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
            foreach (var list in _rowTracers)
                UpdateSequencingState(list);

            foreach (var list in _rowTracers)
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
            if (tutorialModeGo)  tutorialModeGo .SetActive(isTutorial);
            if (gameModeGo)      gameModeGo     .SetActive(!isTutorial);

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
            if (exampleText)     exampleText.text     = _exampleText;
            if (descriptionText) descriptionText.text = desc;

            // Landscape
            if (exampleText)     exampleText.text     = _exampleText;
            if (descriptionText) descriptionText.text = desc;
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

            // Fallback: Check parent's (Viewport's) width if container is uninitialized
            if (container.parent != null)
            {
                var parentRt = container.parent.GetComponent<RectTransform>();
                if (parentRt != null)
                {
                    float pw = parentRt.rect.width;
                    if (pw > 100f) return pw;
                }
            }

            return isLandscape ? 1200f : 800f;
        }

        private float GetMaxWidthFactor()
        {
            float maxFactor = 0f;
            if (valuesToTrace == null || valuesToTrace.Count == 0) return slotCount;

            foreach (var val in valuesToTrace)
            {
                float factor = GetRowWidthFactor(val);
                if (factor > maxFactor)
                {
                    maxFactor = factor;
                }
            }
            return maxFactor > 0f ? maxFactor : slotCount;
        }

        private float GetRowWidthFactor(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return 0f;

            if (IsNumericExpression(val))
            {
                bool hasCommas = val.Contains(",");
                string cleanNum = val.Replace(",", "");
                if (cleanNum.Length == 0) return 0f;

                float cellFactorSum = 0f;
                foreach (char c in cleanNum)
                {
                    if (c == ' ')
                    {
                        cellFactorSum += 0.5f;
                    }
                    else if (c == '1' || c == 'I' || c == 'l' || c == 'i')
                    {
                        cellFactorSum += 0.80f;
                    }
                    else
                    {
                        cellFactorSum += 1.0f;
                    }
                }

                float spacingFactor = hasCommas ? 0f : -0.30f;
                float totalFactor = cellFactorSum + (cleanNum.Length - 1) * spacingFactor;
                return Mathf.Max(0.5f, totalFactor);
            }
            else
            {
                string[] words = val.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                float maxWordFactor = 0f;

                foreach (var w in words)
                {
                    string clean = FilterWord(w);
                    if (string.IsNullOrEmpty(clean)) continue;
                    if (clean.Equals("and", System.StringComparison.OrdinalIgnoreCase)) continue;

                    float cellFactorSum = 0f;
                    foreach (char c in clean)
                    {
                        if (c == '1' || c == 'I' || c == 'l' || c == 'i')
                        {
                            cellFactorSum += 0.80f;
                        }
                        else
                        {
                            cellFactorSum += 1.0f;
                        }
                    }

                    bool isWordNumber = IsDigitsOnly(clean);
                    float spacingFactor = -0.15f;
                    if (isWordNumber && clean.Length > 1)
                    {
                        spacingFactor = -0.30f;
                    }

                    float wordFactor = cellFactorSum + (clean.Length - 1) * spacingFactor;
                    if (wordFactor > maxWordFactor)
                    {
                        maxWordFactor = wordFactor;
                    }
                }
                return Mathf.Max(0.5f, maxWordFactor);
            }
        }

        private float GetCalculatedCellSize(float containerWidth, float maxWidthFactor)
        {
            if (maxWidthFactor <= 0f) return 250f;
            float maxSlotWidth = containerWidth - 80f;
            float C = maxSlotWidth / maxWidthFactor;
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

        private IEnumerator SpawnInto(Transform container, List<List<SlotTracer>> rowTracersList)
        {
            if (container == null) yield break;

            // Clear any existing children first
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                SafeDestroy(container.GetChild(i).gameObject);
            }

            // Determine which continue button belongs to this container and lock it
            Button activeContinueButton = continueButton;
            if (activeContinueButton != null)
            {
                activeContinueButton.interactable = (KidGame.Interface.GameFlowManager.Instance != null);
            }

            if (spellModeActive)
            {
                disableScrollingLayout = true;
            }

            if (disableScrollingLayout)
            {
                var containerVlg = container.GetComponent<VerticalLayoutGroup>();
                if (containerVlg != null) SafeDestroy(containerVlg);

                var containerHlg = container.GetComponent<HorizontalLayoutGroup>();
                if (containerHlg != null) SafeDestroy(containerHlg);

                var containerFitter = container.GetComponent<ContentSizeFitter>();
                if (containerFitter != null) SafeDestroy(containerFitter);

                var containerRt = container.GetComponent<RectTransform>();
                if (containerRt != null)
                {
                    containerRt.anchorMin = Vector2.zero;
                    containerRt.anchorMax = Vector2.one;
                    containerRt.offsetMin = Vector2.zero;
                    containerRt.offsetMax = Vector2.zero;
                }

                string rawVal = (valuesToTrace != null && valuesToTrace.Count > 0) ? valuesToTrace[0] : "";
                if (string.IsNullOrWhiteSpace(rawVal))
                {
                    GameObject rowGo = Instantiate(slotPrefab, container);
                    rowGo.name = "Slot_Default";
                    var rt = rowGo.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                    var tracer = rowGo.GetComponent<SlotTracer>();
                    if (tracer != null)
                    {
                        rowTracersList.Add(new List<SlotTracer> { tracer });
                    }
                    yield break;
                }

                List<string> entriesToSpawn = new List<string>();
                for (int i = 0; i < Mathf.Min(valuesToTrace.Count, customSpawnCount); i++)
                {
                    string raw = valuesToTrace[i].Trim();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        entriesToSpawn.Add(raw);
                    }
                }

                // Color palette for letter cards
                Color[] cardColors = new Color[]
                {
                    new Color(0.98f, 0.48f, 0.48f), // Soft Red
                    new Color(0.48f, 0.72f, 0.98f), // Soft Blue
                    new Color(0.48f, 0.98f, 0.72f), // Soft Green
                    new Color(0.98f, 0.85f, 0.48f), // Soft Yellow
                    new Color(0.85f, 0.48f, 0.98f), // Soft Purple
                    new Color(0.98f, 0.65f, 0.48f)  // Soft Orange
                };

                foreach (string entryVal in entriesToSpawn)
                {
                    if (string.IsNullOrWhiteSpace(entryVal)) continue;

                    bool isNumeric = IsNumericExpression(entryVal);
                    GameObject prefabToInstantiate = (spellModeActive && spellSlotPrefab != null) ? spellSlotPrefab : slotPrefab;
                    GameObject slotRowGo = Instantiate(prefabToInstantiate, container);
                    if (isNumeric)
                    {
                        slotRowGo.name = $"Slot_Number_{entryVal}";
                    }
                    else
                    {
                        slotRowGo.name = $"Slot_Word_{entryVal}";
                    }

                    var rowTracer = slotRowGo.GetComponent<SlotTracer>();
                    if (rowTracer != null) SafeDestroy(rowTracer);

                    RectTransform rowRt = slotRowGo.GetComponent<RectTransform>();
                    if (rowRt != null)
                    {
                        rowRt.anchorMin = Vector2.zero;
                        rowRt.anchorMax = Vector2.one;
                        rowRt.offsetMin = Vector2.zero;
                        rowRt.offsetMax = Vector2.zero;
                    }

                    var le = slotRowGo.GetComponent<LayoutElement>();
                    if (le == null) le = slotRowGo.AddComponent<LayoutElement>();

                    List<List<SlotTracer>> currentTracers = new List<List<SlotTracer>>();
                    PopulateSlotRowCharacters(slotRowGo, entryVal, isNumeric, 250f, currentTracers);

                    if (spellModeActive)
                    {
                        foreach (var list in currentTracers)
                        {
                            foreach (var tracer in list)
                            {
                                MakeShapeFullyTraced(tracer);
                            }
                        }
                    }

                    foreach (var list in currentTracers)
                    {
                        rowTracersList.Add(list);
                    }

                    // Let the current frame render before spawning the next row —
                    // spawning every row in one unbroken burst is what stalls the
                    // loading animation on lower-end devices during scene activation.
                    yield return null;
                }

                if (spellModeActive)
                {
                    // Spawn spelling UI parent
                    GameObject spellingUiGo = new GameObject("SpellingUI", typeof(RectTransform));
                    spellingUiGo.transform.SetParent(container, false);

                    var spellingRt = spellingUiGo.GetComponent<RectTransform>();
                    if (spellingRt != null)
                    {
                        spellingRt.anchorMin = new Vector2(0f, 0f);
                        spellingRt.anchorMax = new Vector2(1f, 0.6f);
                        spellingRt.offsetMin = Vector2.zero;
                        spellingRt.offsetMax = Vector2.zero;
                    }

                    // Spawn slots-only container inside SpellingUI (drop zones for answer letters)
                    GameObject slotsGridGo = new GameObject("AnswerSlotsGrid", typeof(RectTransform));
                    slotsGridGo.transform.SetParent(spellingUiGo.transform, false);
                    var slotsGridRt = slotsGridGo.GetComponent<RectTransform>();
                    if (slotsGridRt != null)
                    {
                        // Fill entire SpellingUI (cards come from external tray container, not here)
                        slotsGridRt.anchorMin = new Vector2(0f, 0f);
                        slotsGridRt.anchorMax = new Vector2(1f, 1f);
                        slotsGridRt.offsetMin = Vector2.zero;
                        slotsGridRt.offsetMax = Vector2.zero;
                    }
                    // Combine all entries to form spelling target words
                    List<string> rawSpellingWords = new List<string>();
                    foreach (string entry in entriesToSpawn)
                    {
                        string wordText = IsNumericExpression(entry) ? NumberToWords(int.Parse(entry)).ToUpper() : entry.ToUpper();
                        string[] parts = wordText.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        foreach (var p in parts)
                        {
                            if (p.Equals("AND", System.StringComparison.OrdinalIgnoreCase)) continue;
                            rawSpellingWords.Add(p);
                        }
                    }

                    int maxWordLen = 1;
                    foreach (string w in rawSpellingWords)
                    {
                        if (w.Length > maxWordLen) maxWordLen = w.Length;
                    }

                    float availableWidth = GetContainerWidth(container);
                    float sideMargin = 25f;
                    float totalWidthForSlots = Mathf.Max(200f, availableWidth - (sideMargin * 2f));

                    float slotSpacing = 10f;
                    float totalSpacing = (maxWordLen - 1) * slotSpacing;

                    float rawSlotSize = (totalWidthForSlots - totalSpacing) / maxWordLen;
                    float slotCellSize = Mathf.Clamp(rawSlotSize, 40f, 120f);

                    var slotsGridGroup = slotsGridGo.AddComponent<VerticalLayoutGroup>();
                    slotsGridGroup.childAlignment = TextAnchor.MiddleCenter;
                    slotsGridGroup.spacing = 50f;
                    slotsGridGroup.padding = new RectOffset((int)sideMargin, (int)sideMargin, 0, 0);
                    slotsGridGroup.childControlWidth = true;
                    slotsGridGroup.childControlHeight = false;
                    slotsGridGroup.childForceExpandWidth = true;
                    slotsGridGroup.childForceExpandHeight = false;

                    // Spawn word rows (each word takes one row)
                    foreach (string word in rawSpellingWords)
                    {
                        GameObject wordRowGo = new GameObject($"WordRow_{word}", typeof(RectTransform));
                        wordRowGo.transform.SetParent(slotsGridGo.transform, false);

                        var rowRt = wordRowGo.GetComponent<RectTransform>();
                        if (rowRt != null)
                        {
                            rowRt.anchorMin = new Vector2(0f, 0.5f);
                            rowRt.anchorMax = new Vector2(1f, 0.5f);
                            rowRt.sizeDelta = new Vector2(0f, slotCellSize);
                        }

                        var rowLe = wordRowGo.AddComponent<LayoutElement>();
                        rowLe.preferredHeight = slotCellSize;
                        rowLe.minHeight = slotCellSize;

                        var rowHlg = wordRowGo.AddComponent<HorizontalLayoutGroup>();
                        rowHlg.childAlignment = TextAnchor.MiddleCenter;
                        rowHlg.spacing = 10f;
                        rowHlg.childControlWidth = false;
                        rowHlg.childControlHeight = false;
                        rowHlg.childForceExpandWidth = false;
                        rowHlg.childForceExpandHeight = false;

                        foreach (char c in word)
                        {
                            if (spellDropZonePrefab != null)
                            {
                                GameObject zoneGo = Instantiate(spellDropZonePrefab, wordRowGo.transform);

                                var zoneRt = zoneGo.GetComponent<RectTransform>();
                                if (zoneRt != null)
                                {
                                    zoneRt.sizeDelta = new Vector2(slotCellSize, slotCellSize);
                                }
                                var zoneLe = zoneGo.GetComponent<LayoutElement>();
                                if (zoneLe == null) zoneLe = zoneGo.AddComponent<LayoutElement>();
                                zoneLe.preferredWidth = slotCellSize;
                                zoneLe.preferredHeight = slotCellSize;
                                zoneLe.minWidth = slotCellSize;
                                zoneLe.minHeight = slotCellSize;

                                var zone = zoneGo.GetComponent<AnswerDropZone>();
                                if (zone == null)
                                {
                                    zone = zoneGo.AddComponent<AnswerDropZone>();
                                }
                                zone.Setup((int)c, () => CheckSpellingComplete(), null, c.ToString());
                            }
                        }
                    }

                    // Spawn tray cards
                    string targetSpelling = string.Join("", rawSpellingWords);
                    List<char> trayLetters = new List<char>();
                    foreach (char c in targetSpelling)
                    {
                        trayLetters.Add(c);
                    }

                    // Add distractors
                    string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    for (int i = 0; i < 4; i++)
                    {
                        trayLetters.Add(alphabet[UnityEngine.Random.Range(0, alphabet.Length)]);
                    }

                    // Shuffle
                    for (int i = 0; i < trayLetters.Count; i++)
                    {
                        char temp = trayLetters[i];
                        int rIdx = UnityEngine.Random.Range(i, trayLetters.Count);
                        trayLetters[i] = trayLetters[rIdx];
                        trayLetters[rIdx] = temp;
                    }

                    // Spawn answer cards into the pre-assigned portrait or landscape container
                    Transform cardTrayContainer = answerCardsContainer;

                    if (cardTrayContainer != null && spellAnswerCardPrefab != null)
                    {
                        // Clear any pre-existing children in the tray container
                        for (int i = cardTrayContainer.childCount - 1; i >= 0; i--)
                        {
                            Transform child = cardTrayContainer.GetChild(i);
                            if (child != null && child.name != "SpellingUI")
                            {
                                SafeDestroy(child.gameObject);
                            }
                        }

                        foreach (char letter in trayLetters)
                        {
                            GameObject cardGo = Instantiate(spellAnswerCardPrefab, cardTrayContainer);
                            var card = cardGo.GetComponent<AnswerCard>();
                            if (card != null)
                            {
                                Color cardColor = cardColors[UnityEngine.Random.Range(0, cardColors.Length)];
                                card.Setup((int)letter, cardColor);
                            }
                        }
                    }
                }
                yield break;
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
                yield break;
            }

            // Adjust layout of the container
            AdjustContainerLayout(container);

            float availableContainerWidth = GetContainerWidth(container);
            float maxFactor = GetMaxWidthFactor();
            float C = GetCalculatedCellSize(availableContainerWidth, maxFactor);
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

                bool isNumeric = IsNumericExpression(val);

                if (isNumeric)
                {
                    bool hasCommas = val.Contains(",");
                    string cleanNum = val.Replace(",", "");
                    if (hasCommas)
                    {
                        SpawnWordSlot(entryGroupGo.transform, cleanNum, maxSlotWidth, C, rowTracersList, true);
                    }
                    else
                    {
                        SpawnNumberSlot(entryGroupGo.transform, cleanNum, rowTracersList, maxSlotWidth, C);
                    }

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
                        bool hasCommas = val.Contains(",");
                        SpawnWordSlot(entryGroupGo.transform, word, maxSlotWidth, C, rowTracersList, hasCommas);
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

                // Let the current frame render before spawning the next entry group.
                yield return null;
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

        private void SpawnWordSlot(Transform container, string word, float maxSlotWidth, float cellSize, List<List<SlotTracer>> rowTracersList, bool hasCommas)
        {
            GameObject rowGo = Instantiate(slotPrefab, container);
            if (hasCommas)
            {
                rowGo.name = $"Slot_CommaList_{word}";
            }
            else
            {
                rowGo.name = $"Slot_Word_{word}";
            }

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
            // Set spacing dynamically: smart spacing based on character count and narrow glyphs
            bool isNumber = IsNumericExpression(characters);
            bool isCommaList = rowGo.name.StartsWith("Slot_CommaList_");
            float spacing = 10f;
            if (isCommaList)
            {
                spacing = 0f;
            }
            else if (isNumber && characters.Length > 1)
            {
                if (characters.Length == 2)
                {
                    bool hasNarrow = characters.Contains("1") || characters.Contains("I") || characters.Contains("l");
                    spacing = hasNarrow ? -0.25f * cellSize : -0.10f * cellSize;
                }
                else
                {
                    spacing = -0.04f * cellSize;
                }
            }
            else if (!isNumber)
            {
                spacing = 10f;
            }

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
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = spacing;
            hlg.padding = new RectOffset(0, 0, 0, 0);

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
                if (c == ' ')
                {
                    GameObject spaceCellGo = new GameObject("Cell_Space", typeof(RectTransform));
                    spaceCellGo.transform.SetParent(targetParent, false);

                    float spaceWidth = cellSize * 0.5f;

                    RectTransform spaceCellRt = spaceCellGo.GetComponent<RectTransform>();
                    if (spaceCellRt != null)
                    {
                        spaceCellRt.sizeDelta = new Vector2(spaceWidth, cellSize);
                    }

                    var spaceCellLe = spaceCellGo.AddComponent<LayoutElement>();
                    spaceCellLe.preferredWidth = spaceWidth;
                    spaceCellLe.preferredHeight = cellSize;
                    continue;
                }

                var shapePrefab = GetPrefabForCharacter(c);
                if (shapePrefab == null)
                {
                    Debug.LogWarning($"[TracingModeManager] No shape prefab found for character '{c}'");
                    continue;
                }

                // Create a container GameObject for the character shape
                GameObject cellGo = new GameObject($"Cell_{shapePrefab.name}", typeof(RectTransform));
                cellGo.transform.SetParent(targetParent, false);

                // Check if the character is narrow (e.g. '1', 'I', 'l', 'i')
                bool isNarrow = (c == '1' || c == 'I' || c == 'l' || c == 'i');
                
                // Tighter layout bounds for multi-character numbers/words to bring glyphs close together
                float widthMultiplier = 1f;
                if (characters.Length > 1)
                {
                    widthMultiplier = isDigit ? (characters.Length > 2 ? 0.95f : 0.85f) : 0.85f;
                }
                float cellWidth = isNarrow ? (cellSize * 0.45f * widthMultiplier) : (cellSize * widthMultiplier);

                // Set cell container size statically to cellWidth x cellSize
                RectTransform cellRt = cellGo.GetComponent<RectTransform>();
                if (cellRt != null)
                {
                    cellRt.sizeDelta = new Vector2(cellWidth, cellSize);
                }

                var cellLe = cellGo.AddComponent<LayoutElement>();
                cellLe.preferredWidth = cellWidth;
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
                    CheckNormalModeComplete();
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

        private bool IsNumericExpression(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return false;
            foreach (char c in str)
            {
                if (c != ' ' && c != ',' && (c < '0' || c > '9'))
                    return false;
            }
            return true;
        }

        private string NumberToWords(int number)
        {
            if (number == 0)
                return "zero";

            if (number < 0)
                return "minus " + NumberToWords(Mathf.Abs(number));

            string words = "";

            if ((number / 1000000) > 0)
            {
                words += NumberToWords(number / 1000000) + " million ";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += NumberToWords(number / 1000) + " thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += NumberToWords(number / 100) + " hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += "and ";

                var unitsMap = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
                var tensMap = new[] { "zero", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

                if (number < 20)
                    words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += " " + unitsMap[number % 10];
                }
            }

            return System.Text.RegularExpressions.Regex.Replace(words.Trim(), @"\s+", " ");
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
                                float effHeight = slotSize.y * (1f - slotTopPaddingRatio);
                                bool isNarrow = false;
                                if (tracer.ShapePrefab != null)
                                {
                                    string name = tracer.ShapePrefab.name;
                                    isNarrow = name.StartsWith("1-") || name.StartsWith("I-") || name.StartsWith("l-") || name.StartsWith("i-");
                                }

                                float scale;
                                if (isNarrow)
                                {
                                    // For narrow characters, only restrict by height so they don't force all other shapes to shrink
                                    scale = (effHeight / shapeSize.y) * (1f - tracer.SizePadding);
                                }
                                else
                                {
                                    scale = Mathf.Min(slotSize.x / shapeSize.x,
                                                        effHeight / shapeSize.y)
                                                   * (1f - tracer.SizePadding);
                                }

                                if (scale > 0f)
                                {
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

        private bool IsLandscapeContainer(Transform container)
        {
            Transform curr = container;
            while (curr != null)
            {
                string nameLower = curr.name.ToLower();
                if (nameLower.Contains("landscape") || nameLower.Contains("lanscape")) return true;
                if (nameLower.Contains("portrait") || nameLower.Contains("potrait")) return false;
                curr = curr.parent;
            }
            return Screen.width > Screen.height;
        }

        private void MakeShapeFullyTraced(SlotTracer tracer)
        {
            if (tracer == null) return;
            tracer.EnsureShapeSpawned();

            if (tracer.shape != null)
            {
                tracer.shape.completed = true;
                tracer.shape.DisableTracingHand();

                foreach (var path in tracer.shape.paths)
                {
                    if (path == null) continue;
                    path.completed = true;
                    path.DisableStartCollider();
                    path.SetNumbersVisibility(false);

                    Image fillImg = null;
                    for (int i = 0; i < path.transform.childCount; i++)
                    {
                        var child = path.transform.GetChild(i);
                        if (child.name == "Fill" || child.CompareTag("Fill"))
                        {
                            fillImg = child.GetComponent<Image>();
                            break;
                        }
                    }
                    if (fillImg != null)
                    {
                        fillImg.fillAmount = 1f;
                        fillImg.color = tracer.TraceColor;
                    }
                }
            }
        }

        private List<AnswerDropZone> GetActiveSpellingZones()
        {
            List<AnswerDropZone> activeZones = new List<AnswerDropZone>();

            // Determine which game content is currently visible
            Transform activeContent = gameContent;

            if (activeContent != null)
            {
                // Search directly for AnswerDropZone children under any SpellingUI in this content
                var spellingUi = activeContent.Find("SpellingUI");
                if (spellingUi != null)
                {
                    var zones = spellingUi.GetComponentsInChildren<AnswerDropZone>(true);
                    activeZones.AddRange(zones);
                }
            }
            return activeZones;
        }

        private Button GetActiveContinueButton() => continueButton;

        private void CheckSpellingComplete()
        {
            var activeZones = GetActiveSpellingZones();
            if (activeZones.Count == 0) return;

            bool allCorrect = true;
            foreach (var zone in activeZones)
            {
                if (zone == null || !zone.IsAnswered)
                {
                    allCorrect = false;
                    break;
                }
            }

            if (allCorrect)
            {
                Debug.Log("[TracingModeManager] spelling completed successfully!");
                // Signal GameFlowManager that the round may now be complete
                GameFlowManager.Instance?.NotifyRoundStateChanged();
                StartCoroutine(SpellingSuccessRoutine());
            }
        }

        private IEnumerator SpellingSuccessRoutine()
        {
            yield return new WaitForSeconds(1.2f);

            // Unlock the continue button for whichever orientation is active
            var btn = GetActiveContinueButton();
            if (btn != null)
            {
                btn.interactable = true;
                var trigger = btn.GetComponent<SceneTransitionTrigger>();
                if (trigger != null)
                {
                    btn.onClick.Invoke();
                }
            }
        }

        private bool IsAllTracingComplete()
        {
            var activeList = _rowTracers;

            if (activeList.Count == 0) return false;

            foreach (var row in activeList)
            {
                foreach (var tracer in row)
                {
                    if (tracer == null) continue;
                    if (!tracer.IsCompleted)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void CheckNormalModeComplete()
        {
            if (spellModeActive) return;

            if (IsAllTracingComplete())
            {
                Debug.Log("[TracingModeManager] All shapes traced successfully!");
                // Signal GameFlowManager that the round may now be complete
                GameFlowManager.Instance?.NotifyRoundStateChanged();
                StartCoroutine(NormalModeSuccessRoutine());
            }
        }

        private IEnumerator NormalModeSuccessRoutine()
        {
            yield return new WaitForSeconds(1.2f);

            var btn = GetActiveContinueButton();
            if (btn != null)
            {
                btn.interactable = true;
                var trigger = btn.GetComponent<SceneTransitionTrigger>();
                if (trigger != null)
                {
                    btn.onClick.Invoke();
                }
            }
        }

        private IEnumerator ForceSpellModeCompletionRoutine()
        {
            // Wait 3 frames to ensure all SlotTracer and Shape Start() methods have fully run and initialized
            yield return null;
            yield return null;
            yield return null;

            foreach (var list in _rowTracers)
            {
                foreach (var tracer in list)
                {
                    MakeShapeFullyTraced(tracer);
                }
            }

            foreach (var list in _rowTracers)
            {
                foreach (var tracer in list)
                {
                    MakeShapeFullyTraced(tracer);
                }
            }
        }

        private void RescaleShapeForCell(SlotTracer tracer, float cellSize)
        {
            if (tracer == null || tracer.shape == null) return;
            var shapeRt = tracer.shape.GetComponent<RectTransform>();
            if (shapeRt != null)
            {
                float shapeSizeY = shapeRt.rect.height;
                if (shapeSizeY <= 0f) shapeSizeY = 350f; // fallback design height
                float scale = (cellSize / shapeSizeY) * (1f - tracer.SizePadding);
                tracer.shape.transform.localScale = Vector3.one * scale;
            }
        }

        private void ApplyScrollDisabledScale(List<List<SlotTracer>> rowList)
        {
            if (rowList.Count == 0 || rowList[0].Count == 0 || rowList[0][0] == null) return;

            var firstTracer = rowList[0][0];
            var container = FindContainer(firstTracer.transform);
            if (container == null) return;

            var containerRt = container.GetComponent<RectTransform>();
            if (containerRt == null) return;

            var parentRt = container.parent as RectTransform;
            if (parentRt != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
            }
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(containerRt);

            bool isLandscape = IsLandscapeContainer(container);
            float w = containerRt.rect.width;
            float h = containerRt.rect.height;

            if (w <= 0f) w = isLandscape ? Mathf.Max(Screen.width, Screen.height) : Mathf.Min(Screen.width, Screen.height);
            if (h <= 0f) h = isLandscape ? Mathf.Min(Screen.width, Screen.height) : Mathf.Max(Screen.width, Screen.height);

            int totalRows = rowList.Count;

            // Check if all rows are numbers
            bool isAllNumeric = true;
            for (int i = 0; i < totalRows; i++)
            {
                var row = rowList[i];
                if (row.Count > 0 && row[0] != null)
                {
                    RectTransform rowRt = null;
                    Transform curr = row[0].transform.parent;
                    while (curr != null)
                    {
                        if (curr.name.StartsWith("Slot_"))
                        {
                            rowRt = curr as RectTransform;
                            break;
                        }
                        curr = curr.parent;
                    }
                    if (rowRt != null && !rowRt.name.StartsWith("Slot_Number_"))
                    {
                        isAllNumeric = false;
                    }
                }
            }

            for (int rowIndex = 0; rowIndex < totalRows; rowIndex++)
            {
                var row = rowList[rowIndex];
                if (row.Count == 0 || row[0] == null) continue;

                var tracer0 = row[0];
                RectTransform rowRt = null;
                Transform curr = tracer0.transform.parent;
                while (curr != null)
                {
                    if (curr.name.StartsWith("Slot_"))
                    {
                        rowRt = curr as RectTransform;
                        break;
                    }
                    curr = curr.parent;
                }

                if (rowRt == null) continue;

                var tracerContainerRt = tracer0.transform.parent as RectTransform;
                if (tracerContainerRt != null)
                {
                    tracerContainerRt.anchorMin = Vector2.zero;
                    tracerContainerRt.anchorMax = Vector2.one;
                    tracerContainerRt.offsetMin = Vector2.zero;
                    tracerContainerRt.offsetMax = Vector2.zero;
                }

                float rowWidth = w;
                float rowHeight = h;

                float effectiveH = spellModeActive ? h * 0.4f : h;
                float yOffset = spellModeActive ? h * 0.6f : 0f;

                // Position the slot row (rowRt) based on totalRows and index
                if (totalRows == 1)
                {
                    float leftPad = w * 0.05f;
                    float rightPad = w * 0.05f;
                    float topPad = effectiveH * 0.05f;
                    float bottomPad = effectiveH * 0.05f;
                    rowWidth = w - (leftPad + rightPad);
                    rowHeight = effectiveH - (topPad + bottomPad);

                    float anchorY = (yOffset + effectiveH * 0.5f) / h;
                    rowRt.anchorMin = new Vector2(0.5f, anchorY);
                    rowRt.anchorMax = new Vector2(0.5f, anchorY);
                    rowRt.pivot = new Vector2(0.5f, 0.5f);
                    rowRt.anchoredPosition = Vector2.zero;
                    rowRt.sizeDelta = new Vector2(rowWidth, rowHeight);
                }
                else if (totalRows == 2)
                {
                    if (isLandscape && isAllNumeric)
                    {
                        // Align horizontally (side-by-side)
                        float colW = w / 2f;
                        rowWidth = colW * 0.85f;
                        rowHeight = effectiveH * 0.85f;

                        float anchorX = (rowIndex == 0) ? 0.25f : 0.75f;
                        float anchorY = (yOffset + effectiveH * 0.5f) / h;
                        rowRt.anchorMin = new Vector2(anchorX, anchorY);
                        rowRt.anchorMax = new Vector2(anchorX, anchorY);
                        rowRt.pivot = new Vector2(0.5f, 0.5f);
                        rowRt.anchoredPosition = Vector2.zero;
                        rowRt.sizeDelta = new Vector2(rowWidth, rowHeight);
                    }
                    else
                    {
                        // Align vertically (stacked)
                        float rowH = effectiveH / 2f;
                        rowWidth = w * 0.85f;
                        rowHeight = rowH * 0.85f;

                        float anchorY = (yOffset + effectiveH * (rowIndex == 0 ? 0.75f : 0.25f)) / h;
                        rowRt.anchorMin = new Vector2(0.5f, anchorY);
                        rowRt.anchorMax = new Vector2(0.5f, anchorY);
                        rowRt.pivot = new Vector2(0.5f, 0.5f);
                        rowRt.anchoredPosition = Vector2.zero;
                        rowRt.sizeDelta = new Vector2(rowWidth, rowHeight);
                    }
                }
                else // 4 rows (2 x 2 grid)
                {
                    float colW = w / 2f;
                    float rowH = effectiveH / 2f;
                    rowWidth = colW * 0.85f;
                    rowHeight = rowH * 0.85f;

                    float anchorX = (rowIndex % 2 == 0) ? 0.25f : 0.75f;
                    float anchorY = (yOffset + effectiveH * (rowIndex / 2 == 0 ? 0.75f : 0.25f)) / h;

                    rowRt.anchorMin = new Vector2(anchorX, anchorY);
                    rowRt.anchorMax = new Vector2(anchorX, anchorY);
                    rowRt.pivot = new Vector2(0.5f, 0.5f);
                    rowRt.anchoredPosition = Vector2.zero;
                    rowRt.sizeDelta = new Vector2(rowWidth, rowHeight);
                }

                var rowLe = rowRt.GetComponent<LayoutElement>();
                if (rowLe != null)
                {
                    rowLe.preferredWidth = rowWidth;
                    rowLe.preferredHeight = rowHeight;
                }

                // Size and scale the character cells inside this row
                int charCount = row.Count;
                float spacing = 10f;
                float availableRowWidth = rowWidth - Mathf.Max(0, charCount - 1) * spacing;
                float cellW = availableRowWidth / charCount;
                float cellSize = Mathf.Min(cellW, rowHeight);

                foreach (var tracer in row)
                {
                    if (tracer == null) continue;
                    var cellRt = tracer.GetComponent<RectTransform>();
                    if (cellRt != null)
                    {
                        cellRt.sizeDelta = new Vector2(cellSize, cellSize);
                        var cellLe = cellRt.GetComponent<LayoutElement>();
                        if (cellLe != null)
                        {
                            cellLe.preferredWidth = cellSize;
                            cellLe.preferredHeight = cellSize;
                        }
                    }
                    RescaleShapeForCell(tracer, cellSize);
                }
            }

            if (spellModeActive)
            {
                RectTransform spellingUiRt = containerRt.Find("SpellingUI") as RectTransform;
                if (spellingUiRt != null)
                {
                    spellingUiRt.anchorMin = new Vector2(0f, 0f);
                    spellingUiRt.anchorMax = new Vector2(1f, 0.6f);
                    spellingUiRt.offsetMin = Vector2.zero;
                    spellingUiRt.offsetMax = Vector2.zero;

                    // Resize the drop-zone slots grid — tray cards live in the user-assigned container
                    var slotsGridTransform = spellingUiRt.Find("AnswerSlotsGrid");
                    if (slotsGridTransform != null)
                    {
                        var vlg = slotsGridTransform.GetComponent<VerticalLayoutGroup>();
                        if (vlg != null)
                        {
                            vlg.spacing = 10f;
                        }
                    }
                }
            }
        }

        private void ApplyUniformScale(List<List<SlotTracer>> rowList, float scale)
        {
            if (disableScrollingLayout)
            {
                ApplyScrollDisabledScale(rowList);
                return;
            }

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
                    float maxFactor = GetMaxWidthFactor();
                    C = GetCalculatedCellSize(containerWidth, maxFactor);
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
                            bool isNarrow = false;
                            if (tracer.ShapePrefab != null)
                            {
                                string name = tracer.ShapePrefab.name;
                                isNarrow = name.StartsWith("1-") || name.StartsWith("I-") || name.StartsWith("l-") || name.StartsWith("i-");
                            }

                            float cellWidth = isNarrow ? scaledSize * 0.80f : scaledSize;
                            cellRt.sizeDelta = new Vector2(cellWidth, scaledSize);

                            var cellLe = cellRt.GetComponent<LayoutElement>();
                            if (cellLe != null)
                            {
                                cellLe.preferredWidth = cellWidth;
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
                    float scaledSpacing = 0f;
                    if (rowRt.name.StartsWith("Slot_Number_"))
                    {
                        string numPart = rowRt.name.Substring("Slot_Number_".Length);
                        if (numPart.Length > 1)
                        {
                            scaledSpacing = -0.30f * scaledSize;
                        }
                    }
                    else if (rowRt.name.StartsWith("Slot_CommaList_"))
                    {
                        scaledSpacing = 0f;
                    }
                    else if (rowRt.name.StartsWith("Slot_Word_"))
                    {
                        string wordPart = rowRt.name.Substring("Slot_Word_".Length);
                        if (IsDigitsOnly(wordPart) && wordPart.Length > 1)
                        {
                            scaledSpacing = -0.30f * scaledSize;
                        }
                        else
                        {
                            scaledSpacing = -0.15f * scaledSize;
                        }
                    }

                    if (hlg != null)
                    {
                        hlg.spacing = scaledSpacing;
                        hlg.padding = new RectOffset(0, 0, Mathf.RoundToInt(scaledSize * slotTopPaddingRatio), 0);
                    }

                    float totalWidth = 0f;
                    foreach (Transform child in containerRt)
                    {
                        var childRt = child as RectTransform;
                        if (childRt != null)
                        {
                            if (child.name == "Cell_Space")
                            {
                                float spaceWidth = scaledSize * 0.5f;
                                childRt.sizeDelta = new Vector2(spaceWidth, scaledSize);

                                var cellLe = childRt.GetComponent<LayoutElement>();
                                if (cellLe != null)
                                {
                                    cellLe.preferredWidth = spaceWidth;
                                    cellLe.preferredHeight = scaledSize;
                                }
                                totalWidth += spaceWidth;
                            }
                            else
                            {
                                totalWidth += childRt.sizeDelta.x;
                            }
                        }
                    }
                    totalWidth += Mathf.Max(0, containerRt.childCount - 1) * scaledSpacing;

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
            ApplyUniformScale(_rowTracers, _minScale);
            ApplyUniformScale(_rowTracers, _minScale);
            UpdateScrollLocking();
        }

        private void UpdateScrollLocking()
        {
            UpdateScrollLockingInternal();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(UpdateScrollLockingRoutine());
            }
        }

        private System.Collections.IEnumerator UpdateScrollLockingRoutine()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            UpdateScrollLockingInternal();
        }

        private void UpdateScrollLockingInternal()
        {
            UpdateScrollLockForContainer(tutorialContent);
            UpdateScrollLockForContainer(gameContent);

            UpdateScrollLockForContainer(answerCardsContainer);

        }

        private void UpdateScrollLockForContainer(Transform container)
        {
            if (container == null) return;

            var scrollRect = container.GetComponentInParent<ScrollRect>();
            if (scrollRect == null) return;

            var contentRt = container as RectTransform;
            var viewportRt = scrollRect.viewport;
            if (viewportRt == null)
            {
                viewportRt = scrollRect.GetComponent<RectTransform>();
            }

            if (contentRt != null && viewportRt != null)
            {
                // Force-rebuild all nested child layouts recursively first, ensuring ContentSizeFitter / LayoutGroup
                // components have fully computed their actual size before the parent contentRt layout is rebuilt.
                RebuildLayoutsRecursive(contentRt);

                // Force layout rebuild on both to get exact current dimensions
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRt);
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

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

                // Determine scrolling direction:
                // - answerCardsContainer scrolls HORIZONTALLY.
                // - answerCardsContainer scrolls VERTICALLY.
                // - For other containers, portrait is VERTICAL and landscape is HORIZONTAL.
                bool scrollVertical;
                if (container == answerCardsContainer)
                {
                    scrollVertical = false;
                }
                else if (container == answerCardsContainer)
                {
                    scrollVertical = true;
                }
                else
                {
                    scrollVertical = !isLandscape;
                }

                if (scrollVertical)
                {
                    scrollRect.vertical = (contentRt.rect.height > viewportRt.rect.height);
                    scrollRect.horizontal = false;
                }
                else
                {
                    scrollRect.horizontal = (contentRt.rect.width > viewportRt.rect.width);
                    scrollRect.vertical = false;
                }
            }
        }

        private void RebuildLayoutsRecursive(Transform t)
        {
            if (t == null) return;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child != null)
                {
                    RebuildLayoutsRecursive(child);
                    var childRt = child as RectTransform;
                    // Only force a rebuild if this node actually drives layout (LayoutGroup or
                    // ContentSizeFitter) — plain children have nothing for the rebuilder to recompute.
                    if (childRt != null &&
                        (child.GetComponent<LayoutGroup>() != null || child.GetComponent<ContentSizeFitter>() != null))
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(childRt);
                    }
                }
            }
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

        public bool IsRoundCompleted()
        {
            if (spellModeActive)
            {
                var zones = GetActiveSpellingZones();
                if (zones == null || zones.Count == 0) return false;
                foreach (var zone in zones)
                {
                    if (!zone.IsAnswered) return false;
                }
                return true;
            }
            else
            {
                return IsAllTracingComplete();
            }
        }

        // ── Editor Helpers ────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEditor.EditorApplication.isCompiling) return;

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
        }
#endif
    }
}