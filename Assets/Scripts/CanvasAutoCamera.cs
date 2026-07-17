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
            Assign();
        }

        private void Update() => Assign();

        private void Assign()
        {
            if (_canvas == null) return;

            if (_canvas.worldCamera != null)
            {
                enabled = false;
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var cam = Camera.main ?? FindFirstObjectByType<Camera>();
#else
            var cam = Camera.main ?? FindObjectOfType<Camera>();
#endif
            if (cam != null)
            {
                _canvas.worldCamera = cam;
                enabled = false;
            }
        }
    }
}