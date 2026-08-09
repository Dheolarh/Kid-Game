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

        [Header("Counting Voice Config")]
        [Tooltip("The specific number value spoken when tapped. If 0 or negative, spoken audio is skipped.")]
        [SerializeField] private int countValue = 0;

        public int CountValue => countValue;

        /// <summary>
        /// Configures the number value spoken when this object is tapped.
        /// </summary>
        public void SetCountValue(int value)
        {
            countValue = value;
        }

        private void OnDestroy() => DOTween.Kill(transform);

        public void OnPointerDown(PointerEventData eventData)
        {
            // Kill any running punch so rapid taps restart cleanly
            DOTween.Kill(transform);
            transform.DOPunchScale(Vector3.one * punchStrength, punchDuration, punchVibrato, 0.5f);

            // Play counting object tap SFX
            KidGame.Audio.AudioManager.Instance?.PlayCountObjectSfx();

            // Play spoken number voice if assigned
            if (countValue > 0)
            {
                KidGame.Audio.AudioManager.Instance?.PlayNumberVoice(countValue.ToString());
            }
        }

    }
}
