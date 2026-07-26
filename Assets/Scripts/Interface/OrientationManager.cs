using UnityEngine;

namespace KidGame.Interface
{
    /// <summary>
    /// Manages screen orientation rules and device capability filtering.
    /// - Non-Game scenes: locked strictly to Portrait.
    /// - Small mobile devices (phones): locked to Portrait everywhere.
    /// - Tablets / Large devices: can play Game scene in Landscape or Portrait.
    /// - Transition into Game scene: grace period while curtain closed, then locked on curtain open.
    /// - End Game Panel: ALWAYS locked to Portrait mode.
    /// </summary>
    public static class OrientationManager
    {
        public static bool IsPortrait { get; private set; } = true;
        public static bool IsLocked { get; private set; } = false;

        /// <summary>
        /// Detects if the physical device is a Tablet or Large Screen device.
        /// Mobile phones return false (and will be locked to Portrait always).
        /// </summary>
        public static bool IsTabletDevice()
        {
#if UNITY_IOS
            return UnityEngine.iOS.Device.generation.ToString().Contains("iPad");
#else
            if (Screen.dpi <= 0) return false;
            float widthInches = Screen.width / Screen.dpi;
            float heightInches = Screen.height / Screen.dpi;
            float diagonalInches = Mathf.Sqrt(widthInches * widthInches + heightInches * heightInches);
            return diagonalInches >= 6.8f;
#endif
        }

        /// <summary>
        /// Locks screen orientation strictly to Portrait (for non-Game scenes & End Game Panel).
        /// </summary>
        public static void LockToPortrait()
        {
            IsPortrait = true;
            IsLocked = true;
            Screen.orientation = ScreenOrientation.Portrait;
            Debug.Log("[OrientationManager] Orientation locked strictly to PORTRAIT.");
        }

        /// <summary>
        /// Called when initiating transition into the Game scene (curtain closed).
        /// Gives grace period to rotate device if on Tablet; forces Portrait if on Phone.
        /// </summary>
        public static void AllowTransitionRotationGracePeriod()
        {
            if (!IsTabletDevice())
            {
                LockToPortrait();
                return;
            }

            IsLocked = false;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Debug.Log("[OrientationManager] Grace period active (Tablet detected): AutoRotation enabled during transition.");
        }

        /// <summary>
        /// Called right as the curtain is about to open to reveal the Game scene.
        /// Locks orientation to the current screen orientation for the remainder of gameplay.
        /// </summary>
        public static void LockGameplayOrientation()
        {
            if (!IsTabletDevice())
            {
                LockToPortrait();
                return;
            }

            IsPortrait = Screen.height >= Screen.width;
            IsLocked = true;

            if (IsPortrait)
            {
                Screen.orientation = ScreenOrientation.Portrait;
                Debug.Log("[OrientationManager] Gameplay locked to PORTRAIT.");
            }
            else
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                Debug.Log("[OrientationManager] Gameplay locked to LANDSCAPE.");
            }
        }

        /// <summary>
        /// Legacy fallback / unlock helper.
        /// </summary>
        public static void LockToCurrentOrientation()
        {
            LockGameplayOrientation();
        }

        /// <summary>
        /// Unlocks orientation back to Portrait.
        /// </summary>
        public static void Unlock()
        {
            LockToPortrait();
        }
    }
}
