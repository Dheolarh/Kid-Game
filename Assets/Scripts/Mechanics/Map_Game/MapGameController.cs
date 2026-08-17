using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using KidGame.Mechanics.Counting;
using KidGame.Mechanics.NumberRecall;

namespace KidGame.Mechanics.Map
{
    /// <summary>
    /// Controller attached to Map Game prefabs.
    /// Manages the Next Button, answer card tray spawning, and PremadeRecallSlot integration.
    /// </summary>
    public class MapGameController : MonoBehaviour
    {
        [Header("UI & Next Button")]
        [Tooltip("The Next/Continue button to advance to the next round or level.")]
        [SerializeField] private Button nextButton;

        [Header("Answer Grid & Cards")]
        [Tooltip("The AnswerCard draggable prefab instantiated into the answer grid tray.")]
        [SerializeField] private GameObject answerCardPrefab;
        [Tooltip("The parent container (Answer Grid / Tray) where answer cards spawn.")]
        [SerializeField] private Transform answersContainer;
        [Tooltip("If true, shuffles answer card order in the tray.")]
        [SerializeField] private bool shuffleAnswerCards = true;

        [Header("Premade Recall Mechanics Reference")]
        [Tooltip("Optional reference to PremadeRecallSlot. If unassigned, automatically finds component in hierarchy.")]
        [SerializeField] private PremadeRecallSlot premadeRecallSlot;

        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.30f, 0.24f),   // red
            new Color(0.20f, 0.60f, 0.86f),   // blue
            new Color(0.95f, 0.61f, 0.07f),   // orange
            new Color(0.15f, 0.68f, 0.38f),   // green
            new Color(0.61f, 0.35f, 0.71f),   // purple
            new Color(0.10f, 0.74f, 0.61f),   // teal
        };

        private readonly List<AnswerCard> _cards = new List<AnswerCard>();
        private bool _isCompleted = false;

        public Button NextButton => nextButton;
        public PremadeRecallSlot RecallSlot => premadeRecallSlot;
        public GameObject AnswerCardPrefab { get => answerCardPrefab; set => answerCardPrefab = value; }
        public Transform AnswersContainer { get => answersContainer; set => answersContainer = value; }

        private void Awake()
        {
            if (premadeRecallSlot == null)
            {
                premadeRecallSlot = GetComponent<PremadeRecallSlot>();
                if (premadeRecallSlot == null)
                {
                    premadeRecallSlot = GetComponentInChildren<PremadeRecallSlot>(true);
                }
            }

            if (answersContainer == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    string nameLower = child.name.ToLower();
                    if (nameLower.Contains("answer grid") || nameLower.Contains("answergrid") || nameLower.Contains("answers container") || nameLower.Contains("tray"))
                    {
                        answersContainer = child;
                        break;
                    }
                }
            }
        }

        private void Start()
        {
            SetupMapRound();
        }

        public void SetupMapRound()
        {
            _isCompleted = false;

            if (nextButton != null)
            {
                nextButton.interactable = false;
            }

            if (premadeRecallSlot != null)
            {
                // Setup premade recall slot and get required answer values
                List<int> requiredAnswers = premadeRecallSlot.Setup(OnAllAnswersSolved);

                // Spawn answer cards in answersContainer
                if (answersContainer != null && answerCardPrefab != null && requiredAnswers != null && requiredAnswers.Count > 0)
                {
                    SpawnAnswerCards(requiredAnswers);
                }
            }
        }

        private void SpawnAnswerCards(List<int> requiredAnswers)
        {
            // Clear existing spawned cards in answersContainer
            foreach (Transform child in answersContainer)
            {
                Destroy(child.gameObject);
            }
            _cards.Clear();

            var displayValues = new List<int>(requiredAnswers);

            if (shuffleAnswerCards)
            {
                displayValues = displayValues.OrderBy(x => UnityEngine.Random.value).ToList();
            }

            for (int i = 0; i < displayValues.Count; i++)
            {
                int val = displayValues[i];
                Color color = Palette[i % Palette.Length];

                var cardGo = Instantiate(answerCardPrefab, answersContainer);
                var card = cardGo.GetComponent<AnswerCard>();
                if (card != null)
                {
                    card.Setup(val, color);
                    _cards.Add(card);
                }
            }

            var rtAnswers = answersContainer.GetComponent<RectTransform>();
            if (rtAnswers != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rtAnswers);
            }
        }

        private void OnAllAnswersSolved()
        {
            _isCompleted = true;

            if (nextButton != null)
            {
                nextButton.interactable = true;
            }

            KidGame.Interface.GameFlowManager.Instance?.NotifyRoundStateChanged();
        }

        /// <summary>
        /// Called by GameFlowManager to check if the map round tasks are fully completed.
        /// </summary>
        public bool IsRoundCompleted()
        {
            if (premadeRecallSlot != null)
            {
                return premadeRecallSlot.IsCompleted();
            }
            return _isCompleted;
        }
    }
}
