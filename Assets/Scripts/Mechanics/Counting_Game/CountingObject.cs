using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace KidGame.Mechanics.Counting
{
    /// <summary>
    /// Attach to any counting-game object icon.
    /// Plays a satisfying punch-scale animation when the child taps it.
    /// Added automatically by CountingSlot.Setup() — no manual prefab setup needed.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CountingObject : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("How much the object scales up on tap (fraction of original size).")]
        [SerializeField] private float punchStrength = 0.35f;
        [Tooltip("How long the punch animation lasts.")]
        [SerializeField] private float punchDuration = 0.28f;
        [Tooltip("Oscillation count during punch.")]
        [SerializeField] private int   punchVibrato  = 6;

        private void OnDestroy() => DOTween.Kill(transform);

        public void OnPointerDown(PointerEventData eventData)
        {
            // Kill any running punch so rapid taps restart cleanly
            DOTween.Kill(transform);
            transform.DOPunchScale(Vector3.one * punchStrength, punchDuration, punchVibrato, 0.5f);
        }
    }
}
