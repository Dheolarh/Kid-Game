using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using KidGame.Audio;

namespace KidGame.Mechanics.Counting
{
    public class CountableObjectGroup : MonoBehaviour
    {
        [Tooltip("List of objects to be counted in order. If empty, automatically grabs direct child GameObjects.")]
        [SerializeField] private List<GameObject> countableObjects = new List<GameObject>();

        [Header("Animation Settings")]
        [Tooltip("Scale multiplier during the pop-up effect.")]
        [SerializeField] private float popScaleMultiplier = 1.35f;

        [Tooltip("Total duration for the pop up and down animation.")]
        [SerializeField] private float popDuration = 0.25f;

        [Header("Setup Settings")]
        [Tooltip("If true, automatically populates countableObjects from direct children if the list is empty.")]
        [SerializeField] private bool autoPopulateFromChildren = true;

        /// <summary>
        /// Action fired when any object in the group is tapped.
        /// Parameters: (int numberValue, GameObject tappedObject, int index)
        /// </summary>
        public event Action<int, GameObject, int> OnObjectTapped;

        private readonly Dictionary<GameObject, Vector3> _originalScales = new Dictionary<GameObject, Vector3>();

        private void Awake()
        {
            InitializeGroup();
        }

        private void Start()
        {
            InitializeGroup();
        }

        /// <summary>
        /// Initializes the group, setting up pointer handlers and caching original scales.
        /// </summary>
        public void InitializeGroup()
        {
            // Ensure container graphic itself doesn't block raycasts for items
            var containerGraphic = GetComponent<Graphic>();
            if (containerGraphic != null)
            {
                containerGraphic.raycastTarget = false;
            }

            bool needsAutoPopulate = autoPopulateFromChildren || countableObjects == null || countableObjects.Count == 0;

            if (!needsAutoPopulate && countableObjects != null)
            {
                // Validate that all referenced objects belong to this container
                for (int i = 0; i < countableObjects.Count; i++)
                {
                    if (countableObjects[i] == null || !countableObjects[i].transform.IsChildOf(transform))
                    {
                        needsAutoPopulate = true;
                        break;
                    }
                }
            }

            if (needsAutoPopulate)
            {
                countableObjects = new List<GameObject>();
                foreach (Transform child in transform)
                {
                    if (child.gameObject.activeSelf && child.GetComponent<AnswerDropZone>() == null && child.GetComponent<LetterAnswerSlot>() == null && child.GetComponent<KidGame.Mechanics.NumberRecall.RecallAnswerSlot>() == null)
                    {
                        countableObjects.Add(child.gameObject);
                    }
                }
            }

            if (countableObjects == null) return;

            _originalScales.Clear();

            for (int i = 0; i < countableObjects.Count; i++)
            {
                GameObject obj = countableObjects[i];
                if (obj == null) continue;

                // Cache original local scale
                if (!_originalScales.ContainsKey(obj))
                {
                    _originalScales[obj] = obj.transform.localScale;
                }

                // Attach tap handlers to obj and all graphics inside obj
                var graphics = obj.GetComponentsInChildren<Graphic>(true);
                if (graphics != null && graphics.Length > 0)
                {
                    foreach (var g in graphics)
                    {
                        if (g == null) continue;
                        g.raycastTarget = true;
                        var childHandler = g.GetComponent<CountableItemHandler>();
                        if (childHandler == null) childHandler = g.gameObject.AddComponent<CountableItemHandler>();
                        childHandler.Setup(this, i, obj);
                    }
                }
                else
                {
                    var handler = obj.GetComponent<CountableItemHandler>();
                    if (handler == null) handler = obj.AddComponent<CountableItemHandler>();
                    handler.Setup(this, i, obj);
                }
            }
        }

        /// <summary>
        /// Called when an item in the group at the given index is tapped.
        /// </summary>
        public void OnItemTapped(int index, GameObject obj)
        {
            if (index < 0) return;

            GameObject targetObj = obj;
            if (countableObjects != null && index < countableObjects.Count && countableObjects[index] != null)
            {
                targetObj = countableObjects[index];
            }

            if (targetObj == null) return;

            int numberValue = index + 1;

            // 1. Play Number Sound (e.g. index 0 -> 1, index 1 -> 2, etc.)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayNumberVoice(numberValue.ToString());
            }

            // 2. Play Pop Up & Down Animation on the target object
            PlayPopAnimation(targetObj);

            // 3. Notify subscribers
            OnObjectTapped?.Invoke(numberValue, targetObj, index);
        }

        /// <summary>
        /// Plays pop up and down scale animation on the specified GameObject transform.
        /// </summary>
        public void PlayPopAnimation(GameObject obj)
        {
            if (obj == null) return;

            Transform t = obj.transform;
            t.DOKill();

            if (!_originalScales.TryGetValue(obj, out Vector3 baseScale))
            {
                baseScale = Vector3.one;
            }

            Vector3 targetPopScale = baseScale * popScaleMultiplier;
            float halfDuration = popDuration * 0.5f;

            t.DOScale(targetPopScale, halfDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    t.DOScale(baseScale, halfDuration).SetEase(Ease.InQuad);
                });
        }

        /// <summary>
        /// Dynamically updates the list of countable objects at runtime.
        /// </summary>
        public void SetObjects(List<GameObject> newObjects)
        {
            countableObjects = newObjects;
            InitializeGroup();
        }

        /// <summary>
        /// Returns the count of registered objects.
        /// </summary>
        public int Count => countableObjects != null ? countableObjects.Count : 0;
    }

    /// <summary>
    /// Helper component attached to each countable object / graphic to capture pointer clicks/taps.
    /// </summary>
    public class CountableItemHandler : MonoBehaviour, IPointerClickHandler
    {
        private CountableObjectGroup _group;
        private int _index;
        private GameObject _targetObject;

        public void Setup(CountableObjectGroup group, int index, GameObject targetObject = null)
        {
            _group = group;
            _index = index;
            _targetObject = targetObject != null ? targetObject : gameObject;

            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }

            var btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(HandleClick);
                btn.onClick.AddListener(HandleClick);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            HandleClick();
        }

        private void HandleClick()
        {
            if (_group != null)
            {
                _group.OnItemTapped(_index, _targetObject != null ? _targetObject : gameObject);
            }
        }
    }
}
