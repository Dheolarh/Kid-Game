using UnityEngine;

namespace KidGame.Interface
{
    /// <summary>
    /// Manages screen orientation rules across all game scenes and transitions.
    /// The entire game is strictly locked to PORTRAIT mode across all devices.
    /// Auto-rotation and landscape transitions are completely disabled.
    /// </summary>
    public static class OrientationManager
    {
        public static bool IsPortrait => true;
        public static bool IsLocked => true;

        public static bool IsTabletDevice()
        {
            return false;
        }

        /// <summary>
        /// Locks screen orientation strictly to Portrait everywhere.
        /// </summary>
        public static void LockToPortrait()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Debug.Log("[OrientationManager] Orientation locked to PORTRAIT.");
        }

        public static void EnableAutoRotation()
        {
            LockToPortrait();
        }

        public static void AllowTransitionRotationGracePeriod()
        {
            LockToPortrait();
        }

        public static void LockGameplayOrientation()
        {
            LockToPortrait();
        }

        public static void LockToCurrentOrientation()
        {
            LockToPortrait();
        }

        public static void Unlock()
        {
            LockToPortrait();
        }
    }
}
