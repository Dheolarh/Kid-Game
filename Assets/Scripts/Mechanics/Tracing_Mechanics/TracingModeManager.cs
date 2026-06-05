using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            if (slotPrefab == null)
            {
                Debug.LogError("[TracingModeManager] Slot Prefab is not assigned.");
                yield break;
            }

            // Read display info from the prefab's SlotTracer BEFORE instantiating
            var prefabTracer = slotPrefab.GetComponent<SlotTracer>();
            if (prefabTracer != null)
            {
                _exampleText       = prefabTracer.exampleText;
                _descriptionWord   = prefabTracer.descriptionWord;
                _descriptionNumber = prefabTracer.descriptionNumber;
                _customSentence    = prefabTracer.customDescriptionSentence;
            }

            // Spawn into all four containers
            SpawnInto(portraitTutorialContent);
            SpawnInto(portraitGameContent);
            SpawnInto(landscapeTutorialContent);
            SpawnInto(landscapeGameContent);

            // Wait for SlotTracer.Start() coroutines to finish spawning shapes:
            //   Frame 1: yield → SpawnShape
            //   Frame 2: yield → ColorizeStartDots
            yield return null;
            yield return null;

            // Apply starting mode and populate objective text
            ApplyMode(tutorialModeActive);
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

        // ── Spawning ──────────────────────────────────────────────────────────

        private void SpawnInto(Transform container)
        {
            if (container == null) return;

            for (int i = 0; i < slotCount; i++)
                Instantiate(slotPrefab, container);
        }

        // ── Editor Helpers ────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                ApplyMode(tutorialModeActive);
        }

        [ContextMenu("Switch to Tutorial Mode")]
        private void EditorSetTutorial() { tutorialModeActive = true;  ApplyMode(true);  }

        [ContextMenu("Switch to Game Mode")]
        private void EditorSetGame()     { tutorialModeActive = false; ApplyMode(false); }
#endif
    }
}
