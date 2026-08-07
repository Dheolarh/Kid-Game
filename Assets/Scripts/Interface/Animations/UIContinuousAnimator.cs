using UnityEngine;

namespace GoldenPenny.Animations
{
    public enum UIAnimationType
    {
        None,
        PingPongScale
        // Add more animation types here as needed
    }

    
    public class UIContinuousAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        public UIAnimationType animationType = UIAnimationType.PingPongScale;
        
        [Header("Ping Pong Scale Settings")]
        public float scaleSpeed = 2f;
        public float scaleMagnitude = 0.1f;
        public Vector3 baseScale = Vector3.one;

        private float _timeCounter = 0f;

        private void Start()
        {
            // If baseScale wasn't customized in the inspector, initialize it to the current scale
            if (baseScale == Vector3.one && transform.localScale != Vector3.one)
            {
                baseScale = transform.localScale;
            }
        }

        private void Update()
        {
            if (animationType == UIAnimationType.None) return;

            _timeCounter += Time.deltaTime * scaleSpeed;

            switch (animationType)
            {
                case UIAnimationType.PingPongScale:
                    ApplyPingPongScale();
                    break;
            }
        }

        private void ApplyPingPongScale()
        {
            // Mathf.Sin creates a smooth oscillation between -1 and 1
            float scaleOffset = Mathf.Sin(_timeCounter) * scaleMagnitude;
            
            transform.localScale = baseScale + new Vector3(scaleOffset, scaleOffset, scaleOffset);
        }
    }
}
