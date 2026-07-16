using UnityEngine;

namespace KidGame.Interface
{
    /// <summary>
    /// Detects and locks screen orientation once per session (on first level load).
    /// Unlocked when the player returns to the home scene.
    /// </summary>
    public static class OrientationManager
    {
        /// <summary>True if the session is locked to portrait; false if landscape.</summary>
        public static bool IsPortrait { get; private set; } = true;

        /// <summary>True once orientation has been locked for this session.</summary>
        public static bool IsLocked { get; private set; } = false;

        /// <summary>
        /// Detects current orientation, locks Screen.orientation, and stores the result.
        /// Calling this more than once is a no-op — orientation is locked for the whole session.
        /// </summary>
        public static void LockToCurrentOrientation()
        {
            if (IsLocked) return;

            // Detect current orientation
            IsPortrait = Screen.height >= Screen.width;
            IsLocked   = true;

            if (IsPortrait)
            {
                Screen.orientation = ScreenOrientation.Portrait;
                Debug.Log("[OrientationManager] Session locked to PORTRAIT.");
            }
            else
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                Debug.Log("[OrientationManager] Session locked to LANDSCAPE.");
            }
        }

        /// <summary>
        /// Restores auto-rotation. Call this when the player returns to the home scene.
        /// </summary>
        public static void Unlock()
        {
            if (!IsLocked) return;
            IsLocked = false;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Debug.Log("[OrientationManager] Orientation unlocked — auto-rotation restored.");
        }
    }
}
