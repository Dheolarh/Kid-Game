using UnityEngine;

namespace KidGame
{
    [RequireComponent(typeof(Canvas))]
    public class CanvasAutoCamera : MonoBehaviour
    {
        private void Awake() => Assign();

        private void Update() => Assign();

        private void Assign()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || canvas.worldCamera != null) return;

#if UNITY_2023_1_OR_NEWER
            var cam = Camera.main ?? FindFirstObjectByType<Camera>();
#else
            var cam = Camera.main ?? FindObjectOfType<Camera>();
#endif
            if (cam != null)
                canvas.worldCamera = cam;
        }
    }
}
