using System.Collections;
using UnityEngine;

namespace KidGame
{
    [RequireComponent(typeof(Canvas))]
    public class CanvasAutoCamera : MonoBehaviour
    {
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            TryAssign();
        }

        private void Start()
        {
            // If Awake failed (camera not ready yet), keep retrying each frame
            if (!TryAssign())
                StartCoroutine(RetryUntilFound());
        }

        /// <summary>Returns true if camera was successfully assigned (or wasn't needed).</summary>
        private bool TryAssign()
        {
            if (_canvas.renderMode != RenderMode.ScreenSpaceCamera) return true;
            if (_canvas.worldCamera != null) return true;

            _canvas.worldCamera = Camera.main;
            return _canvas.worldCamera != null;
        }

        private IEnumerator RetryUntilFound()
        {
            while (_canvas.worldCamera == null)
            {
                yield return null;          // wait one frame, try again
                _canvas.worldCamera = Camera.main;
            }

            if (_canvas.worldCamera == null)
                Debug.LogWarning("[CanvasAutoCamera] No Camera tagged 'MainCamera' found. " +
                                 "Make sure your main camera has the 'MainCamera' tag.");
        }
    }
}
