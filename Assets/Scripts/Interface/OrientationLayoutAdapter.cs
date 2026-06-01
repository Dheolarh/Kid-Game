using UnityEngine;

namespace KidGame.Interface
{
    public class OrientationLayoutAdapter : MonoBehaviour
    {
        [Header("Layouts")]
        [Tooltip("The GameObject designed for portrait mode.")]
        [SerializeField] private GameObject portraitView;

        [Tooltip("The GameObject designed for landscape mode.")]
        [SerializeField] private GameObject landscapeView;

        private bool _lastLandscape;

        private void Awake()
        {
            _lastLandscape = !IsLandscape();
            Apply();
        }

        private void Update()
        {
            bool landscape = IsLandscape();
            if (landscape == _lastLandscape) return;

            _lastLandscape = landscape;
            Apply();
        }

        private void Apply()
        {
            bool landscape = IsLandscape();

            if (portraitView  != null) portraitView.SetActive(!landscape);
            if (landscapeView != null) landscapeView.SetActive(landscape);
        }

        private static bool IsLandscape() => Screen.width > Screen.height;

#if UNITY_EDITOR
        [ContextMenu("Preview → Portrait")]
        private void PreviewPortrait()
        {
            if (portraitView  != null) portraitView.SetActive(true);
            if (landscapeView != null) landscapeView.SetActive(false);
        }

        [ContextMenu("Preview → Landscape")]
        private void PreviewLandscape()
        {
            if (portraitView  != null) portraitView.SetActive(false);
            if (landscapeView != null) landscapeView.SetActive(true);
        }
#endif
    }
}
