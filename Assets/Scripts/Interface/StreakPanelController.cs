using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using KidGame.Audio;

namespace KidGame.Interface
{
    /// <summary>
    /// Controller for the daily Streak System.
    /// Manages Panel A (Main Grid Overview) and Panel B (Daily Reward Popup Overlay).
    /// Sequence order: Panel B pops up FIRST with Parent object animation, then Panel A appears after Panel B is gone.
    /// </summary>
    public class StreakPanelController : MonoBehaviour
    {
        public static StreakPanelController Instance { get; private set; }

        [Header("Main Root & Panel References")]
        [Tooltip("The main parent GameObject of the Streak System.")]
        [SerializeField] private GameObject streakRootObject;

        [Tooltip("Panel A - The Main Streak Overview Grid Panel.")]
        [SerializeField] private GameObject panelA;
        [SerializeField] private TMP_Text panelATitleText;
        [SerializeField] private Transform panelAGridContent;
        [SerializeField] private Button panelANextButton;

        [Tooltip("Panel B - The Reward Popup Overlay Panel.")]
        [SerializeField] private GameObject panelB;
        [Tooltip("Panel B Parent object that gets animated (contains Days Object and VFX).")]
        [SerializeField] private GameObject panelBParentObject;
        [SerializeField] private GameObject panelBDaysObject;
        [SerializeField] private GameObject panelBVfx;

        [Header("Reward Sprites")]
        [Tooltip("List of reward sprites to display for streak days.")]
        [SerializeField] private List<Sprite> rewardSprites = new List<Sprite>();

        [Header("Day Slot Template")]
        [Tooltip("Template GameObject inside Days Grid Content for each day slot.")]
        [SerializeField] private GameObject daySlotPrefab;

        [Header("Settings")]
        [SerializeField] private float firstRunDelay = 1.0f;
        [SerializeField] private float rewardPopupDuration = 2.0f;

        private Coroutine _streakSequenceCoroutine;
        private bool _isSequenceRunning = false;
        private Vector3 _parentOriginalScale = Vector3.one;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (panelBParentObject != null && panelBParentObject.transform.localScale != Vector3.zero)
            {
                _parentOriginalScale = panelBParentObject.transform.localScale;
            }
            else if (panelBDaysObject != null && panelBDaysObject.transform.localScale != Vector3.zero)
            {
                _parentOriginalScale = panelBDaysObject.transform.localScale;
            }

            if (panelANextButton != null)
            {
                panelANextButton.onClick.AddListener(OnNextButtonClicked);
            }

            // Ensure streak panels start hidden
            if (streakRootObject != null) streakRootObject.SetActive(false);
            if (panelA != null) panelA.SetActive(false);
            if (panelB != null) panelB.SetActive(false);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Main" && _isSequenceRunning)
            {
                StopAllCoroutines();
                _isSequenceRunning = false;
                if (streakRootObject != null) streakRootObject.SetActive(false);
            }
        }

        public void TriggerFirstTimeRegistrationStreak()
        {
            StartCoroutine(FirstTimeRegistrationSequenceCoroutine());
        }

        private IEnumerator FirstTimeRegistrationSequenceCoroutine()
        {
            yield return new WaitForSeconds(firstRunDelay);

            if (SceneManager.GetActiveScene().name == "Main")
            {
                CheckAndShowStreak(forceShow: true);
            }
        }

        [ContextMenu("Force Test Streak Popup")]
        public void ForceTestStreak()
        {
            CheckAndShowStreak(forceShow: true);
        }

        /// <summary>
        /// Main method to check streak date, update streak count, and show panels.
        /// </summary>
        public void CheckAndShowStreak(bool forceShow = false)
        {
            gameObject.SetActive(true);
            if (streakRootObject != null) streakRootObject.SetActive(true);

            if (_isSequenceRunning) return;

            string todayStr = System.DateTime.Today.ToString("yyyy-MM-dd");
            string lastLoginStr = PlayerPrefs.GetString("Streak_LastLoginDate", "");
            int streakDays = PlayerPrefs.GetInt("Streak_DaysCount", 1);
            int lastClaimedDay = PlayerPrefs.GetInt("Streak_LastClaimedDay", 0);

            // Daily Login / Streak Day calculation
            if (string.IsNullOrEmpty(lastLoginStr))
            {
                streakDays = 1;
                PlayerPrefs.SetString("Streak_LastLoginDate", todayStr);
                PlayerPrefs.SetInt("Streak_DaysCount", streakDays);
                PlayerPrefs.SetInt("PlayerStreak", streakDays);
                PlayerPrefs.SetInt("StreakDays", streakDays);
                PlayerPrefs.Save();
            }
            else if (lastLoginStr != todayStr)
            {
                System.DateTime lastLoginDate;
                if (System.DateTime.TryParse(lastLoginStr, out lastLoginDate))
                {
                    double daysPassed = (System.DateTime.Today - lastLoginDate.Date).TotalDays;
                    if (daysPassed == 1)
                    {
                        streakDays++;
                    }
                    else if (daysPassed > 1)
                    {
                        streakDays = 1;
                        lastClaimedDay = 0;
                    }
                }
                else
                {
                    streakDays = 1;
                }

                PlayerPrefs.SetString("Streak_LastLoginDate", todayStr);
                PlayerPrefs.SetInt("Streak_DaysCount", streakDays);
                PlayerPrefs.SetInt("PlayerStreak", streakDays);
                PlayerPrefs.SetInt("StreakDays", streakDays);
                PlayerPrefs.Save();
            }

            // Check if today's streak reward needs to be claimed
            if (lastClaimedDay < streakDays || lastClaimedDay == 0 || forceShow)
            {
                if (_streakSequenceCoroutine != null) StopCoroutine(_streakSequenceCoroutine);
                _streakSequenceCoroutine = StartCoroutine(PlayStreakSequence(streakDays));
            }
            else
            {
                // Already claimed for today: do NOT show Panel A or Panel B automatically
                Debug.Log("[StreakPanelController] Today's streak reward has already been claimed. Skipping streak panels.");
                if (panelA != null) panelA.SetActive(false);
                if (panelB != null) panelB.SetActive(false);
                if (streakRootObject != null) streakRootObject.SetActive(false);
            }
        }

        private IEnumerator PlayStreakSequence(int currentDay)
        {
            _isSequenceRunning = true;

            string playerName = PlayerPrefs.GetString("SingleWordName", "Player").ToUpper();

            if (streakRootObject != null) streakRootObject.SetActive(true);

            // Ensure Panel A is hidden initially (Panel A will only appear AFTER Panel B closes!)
            if (panelA != null)
            {
                panelA.SetActive(false);
            }

            // Step 1: Open Panel B FIRST
            GameObject animTarget = (panelBParentObject != null) ? panelBParentObject : (panelBDaysObject != null ? panelBDaysObject : panelB);

            if (panelB != null)
            {
                panelB.SetActive(true);
                panelB.transform.localScale = Vector3.one;

                // Set reward sprite in Days Object inside Panel B
                Sprite targetSprite = GetRewardSpriteForDay(currentDay);
                if (panelBDaysObject != null)
                {
                    SpriteRenderer sr = panelBDaysObject.GetComponent<SpriteRenderer>();
                    if (sr == null) sr = panelBDaysObject.GetComponentInChildren<SpriteRenderer>(true);

                    Image img = panelBDaysObject.GetComponent<Image>();
                    if (img == null) img = panelBDaysObject.GetComponentInChildren<Image>(true);

                    if (sr != null) sr.sprite = targetSprite;
                    if (img != null) img.sprite = targetSprite;
                }

                if (panelBVfx != null)
                {
                    panelBVfx.SetActive(true);
                }

                // Animate Parent object popup in Panel B
                if (animTarget != null)
                {
                    animTarget.SetActive(true);
                    animTarget.transform.DOKill();
                    animTarget.transform.localScale = Vector3.zero;
                    animTarget.transform.DOScale(_parentOriginalScale, 0.4f).SetEase(Ease.OutBack);
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayVictory1Sfx();
                }
            }

            // Step 2: Wait rewardPopupDuration seconds
            yield return new WaitForSeconds(rewardPopupDuration);

            // Step 3: Close Panel B (Animate Parent down, then deactivate Panel B)
            if (panelB != null)
            {
                if (animTarget != null)
                {
                    animTarget.transform.DOKill();
                    animTarget.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
                }

                yield return new WaitForSeconds(0.35f);

                panelB.SetActive(false);
                if (panelBVfx != null) panelBVfx.SetActive(false);
                if (animTarget != null) animTarget.transform.localScale = _parentOriginalScale;
            }

            yield return new WaitForSeconds(0.2f);

            // Step 4: NOW Open Panel A (Main Grid Overview Panel) AFTER Panel B is gone!
            if (panelA != null)
            {
                panelA.SetActive(true);
                panelA.transform.DOKill();
                panelA.transform.localScale = Vector3.zero;
                panelA.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);

                if (panelATitleText != null)
                {
                    panelATitleText.text = $"IT'S DAY {currentDay}\n{playerName}";
                }

                // Hide Next button initially
                if (panelANextButton != null)
                {
                    panelANextButton.gameObject.SetActive(false);
                    panelANextButton.transform.localScale = Vector3.zero;
                }

                // Populate previous claimed days
                PopulateGridContent(currentDay - 1);
            }

            yield return new WaitForSeconds(0.4f);

            // Step 5: Pop up today's reward sprite inside Panel A's grid slot for currentDay
            int lastClaimed = PlayerPrefs.GetInt("Streak_LastClaimedDay", 0);
            if (lastClaimed < currentDay)
            {
                PlayerPrefs.SetInt("Streak_LastClaimedDay", currentDay);
                PlayerPrefs.Save();
            }

            Transform todaySlot = AddOrUpdateGridSlot(currentDay, isNewClaim: true);
            if (todaySlot != null)
            {
                ScrollToSlot(todaySlot as RectTransform);
                yield return new WaitForSeconds(0.15f);

                todaySlot.localScale = Vector3.zero;
                todaySlot.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayDialoguePopSfx();
                }
            }

            yield return new WaitForSeconds(0.3f);

            // Step 6: Pop up Next button on Panel A
            if (panelANextButton != null)
            {
                panelANextButton.gameObject.SetActive(true);
                panelANextButton.transform.DOKill();
                panelANextButton.transform.localScale = Vector3.zero;
                panelANextButton.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
            }

            _isSequenceRunning = false;
        }

        private void PopulateGridContent(int claimedUpToDay)
        {
            if (panelAGridContent == null) return;

            for (int i = 1; i <= claimedUpToDay; i++)
            {
                AddOrUpdateGridSlot(i, isNewClaim: false);
            }
        }

        private Transform AddOrUpdateGridSlot(int dayNumber, bool isNewClaim)
        {
            if (panelAGridContent == null) return null;

            string slotName = $"DaySlot_{dayNumber}";
            Transform existingSlot = panelAGridContent.Find(slotName);

            if (existingSlot == null)
            {
                if (daySlotPrefab != null)
                {
                    GameObject newSlotObj = Instantiate(daySlotPrefab, panelAGridContent);
                    newSlotObj.name = slotName;
                    existingSlot = newSlotObj.transform;
                }
                else
                {
                    GameObject newSlotObj = new GameObject(slotName, typeof(RectTransform), typeof(Image));
                    newSlotObj.transform.SetParent(panelAGridContent, false);
                    existingSlot = newSlotObj.transform;
                }
            }

            // Set sprite
            Sprite targetSprite = GetRewardSpriteForDay(dayNumber);
            Image imgSlot = null;
            SpriteRenderer srSlot = null;

            // Search children first so we apply sprite to child icon Image rather than root frame Image
            foreach (Transform child in existingSlot)
            {
                imgSlot = child.GetComponent<Image>();
                srSlot = child.GetComponent<SpriteRenderer>();
                if (imgSlot != null || srSlot != null) break;
            }

            // Fallback to root component if no child has Image or SpriteRenderer
            if (imgSlot == null && srSlot == null)
            {
                imgSlot = existingSlot.GetComponent<Image>();
                srSlot = existingSlot.GetComponent<SpriteRenderer>();
            }

            if (srSlot != null)
            {
                srSlot.sprite = targetSprite;
                srSlot.enabled = true;
            }
            if (imgSlot != null)
            {
                imgSlot.sprite = targetSprite;
                imgSlot.enabled = true;
            }

            if (!isNewClaim)
            {
                existingSlot.localScale = Vector3.one;
            }

            return existingSlot;
        }

        private void ScrollToSlot(RectTransform targetSlot)
        {
            if (targetSlot == null || panelAGridContent == null) return;

            ScrollRect scrollRect = panelAGridContent.GetComponentInParent<ScrollRect>();
            if (scrollRect == null) return;

            Canvas.ForceUpdateCanvases();

            RectTransform gridRt = panelAGridContent as RectTransform;
            RectTransform viewport = scrollRect.viewport;
            if (viewport == null) viewport = scrollRect.transform as RectTransform;

            float contentHeight = gridRt.rect.height;
            float viewportHeight = viewport.rect.height;

            if (contentHeight <= viewportHeight)
            {
                scrollRect.verticalNormalizedPosition = 1.0f;
                return;
            }

            // Get target slot position in grid content local space
            Vector3 localPos = gridRt.InverseTransformPoint(targetSlot.position);
            float targetY = Mathf.Abs(localPos.y);

            float scrollableRange = contentHeight - viewportHeight;
            float targetCenterY = targetY - (viewportHeight * 0.5f);

            float normalizedY = 1.0f - Mathf.Clamp01(targetCenterY / scrollableRange);

            scrollRect.velocity = Vector2.zero;
            scrollRect.StopMovement();

            // Smoothly scroll to target row
            scrollRect.DOVerticalNormalizedPos(normalizedY, 0.4f).SetEase(Ease.OutQuad);
        }

        private Sprite GetRewardSpriteForDay(int dayNumber)
        {
            if (rewardSprites == null || rewardSprites.Count == 0) return null;
            int index = (dayNumber - 1) % rewardSprites.Count;
            return rewardSprites[index];
        }

        private void OnNextButtonClicked()
        {
            // Note: ButtonClickSfx component on panelANextButton already handles click SFX on pointer down

            // Animate Panel A closing and hide root
            if (panelA != null)
            {
                panelA.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    if (panelA != null) panelA.SetActive(false);
                    if (streakRootObject != null) streakRootObject.SetActive(false);
                });
            }
            else if (streakRootObject != null)
            {
                streakRootObject.SetActive(false);
            }
        }
    }
}
