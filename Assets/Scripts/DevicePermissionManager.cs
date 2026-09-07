using UnityEngine;
using System.Collections;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif

namespace KidGame.Permissions
{
    /// <summary>
    /// Centralized device permission manager for Android & iOS.
    /// Handles requesting notification and device permissions across platforms safely.
    /// Opens native device App Settings if permission was previously denied.
    /// </summary>
    public class DevicePermissionManager : MonoBehaviour
    {
        public static DevicePermissionManager Instance { get; private set; }

        private const string PrefKey_NotificationEnabled = "Setting_NotificationEnabled";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Returns true if OS Notification permission is granted on Android / iOS.
        /// </summary>
        public static bool HasNotificationPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = version.GetStatic<int>("SDK_INT");
                    if (sdkInt < 33)
                    {
                        // POST_NOTIFICATIONS is only a runtime permission on Android 13 (API 33)+.
                        // On Android 12 and below, notifications are enabled by default.
                        return true;
                    }
                }
                return Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DevicePermissionManager] HasNotificationPermission exception: {ex.Message}");
                return true;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                // Use iOSNotificationCenter (Unity.Notifications.iOS) — the same API used for scheduling.
                // The old UnityEngine.iOS.NotificationServices API is deprecated and does NOT share
                // permission state with iOSNotificationCenter. Querying it always returns None.
                var settings = iOSNotificationCenter.GetNotificationSettings();
                return settings.AuthorizationStatus == AuthorizationStatus.Authorized
                    || settings.AuthorizationStatus == AuthorizationStatus.Provisional;
            }
            catch
            {
                return true;
            }
#else
            return true; // Return true in Editor for testing
#endif
        }

        /// <summary>
        /// Requests notification permissions for Android (POST_NOTIFICATIONS) and iOS (Alert, Badge, Sound).
        /// </summary>
        public static void RequestNotificationPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = version.GetStatic<int>("SDK_INT");
                    if (sdkInt < 33)
                    {
                        return; // Not needed on Android 12 and below
                    }
                }
                if (!Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
                {
                    Debug.Log("[DevicePermissionManager] Requesting Android POST_NOTIFICATIONS permission...");
                    Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DevicePermissionManager] Android notification permission error: {ex.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS permission is handled automatically on launch by Unity.Notifications.iOS
            // (UnityNotificationRequestAuthorizationOnAppLaunch = true in NotificationsSettings.asset).
            // This is a no-op on iOS — permission requesting is async and managed by the package.
            Debug.Log("[DevicePermissionManager] iOS notification permission is managed by iOSNotificationCenter on launch.");
#else
            Debug.Log("[DevicePermissionManager] RequestNotificationPermission called (Editor / Non-Mobile Platform).");
#endif
        }

        /// <summary>
        /// Opens the native device Application Details Settings page (Android Intent / iOS App Settings)
        /// so the user can manually enable notification permissions.
        /// </summary>
        public static void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        string packageName = currentActivity.Call<string>("getPackageName");
                        using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS"))
                        {
                            using (var uri = new AndroidJavaClass("android.net.Uri").CallStatic<AndroidJavaObject>("fromParts", "package", packageName, null))
                            {
                                intent.Call<AndroidJavaObject>("setData", uri);
                                currentActivity.Call("startActivity", intent);
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DevicePermissionManager] Failed to open Android App Settings: {ex.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                Application.OpenURL("app-settings:");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DevicePermissionManager] Failed to open iOS App Settings: {ex.Message}");
            }
#else
            Debug.Log("[DevicePermissionManager] OpenAppSettings called (Editor / Non-Mobile Platform).");
#endif
        }

        /// <summary>
        /// Returns whether notifications are enabled in user settings and OS permissions.
        /// </summary>
        public static bool IsNotificationEnabled()
        {
            int savedSetting = PlayerPrefs.GetInt(PrefKey_NotificationEnabled, 1);
            return savedSetting == 1 && HasNotificationPermission();
        }

        /// <summary>
        /// Sets user preference for notifications.
        /// If turning ON and OS permission is missing, requests permission or opens App Settings.
        /// </summary>
        public static bool SetNotificationEnabledSetting(bool enable)
        {
            if (!enable)
            {
                PlayerPrefs.SetInt(PrefKey_NotificationEnabled, 0);
                PlayerPrefs.Save();
                return false;
            }
            else
            {
                if (HasNotificationPermission())
                {
                    PlayerPrefs.SetInt(PrefKey_NotificationEnabled, 1);
                    PlayerPrefs.Save();
                    return true;
                }
                else
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    try
                    {
                        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            int sdkInt = version.GetStatic<int>("SDK_INT");
                            if (sdkInt >= 33)
                            {
                                // POST_NOTIFICATIONS is async — use callbacks so we only save '1' after the user
                                // actually grants permission. Opening App Settings immediately after requesting
                                // would race with (and cover) the permission dialog.
                                var callbacks = new PermissionCallbacks();
                                callbacks.PermissionGranted += permName =>
                                {
                                    Debug.Log("[DevicePermissionManager] POST_NOTIFICATIONS granted by user.");
                                    PlayerPrefs.SetInt(PrefKey_NotificationEnabled, 1);
                                    PlayerPrefs.Save();
                                    Notifications.LocalNotificationManager.ScheduleAllDynamicNotifications();
                                };
                                callbacks.PermissionDenied += permName =>
                                {
                                    Debug.Log("[DevicePermissionManager] POST_NOTIFICATIONS denied. Directing user to App Settings...");
                                    PlayerPrefs.SetInt(PrefKey_NotificationEnabled, 0);
                                    PlayerPrefs.Save();
                                    OpenAppSettings();
                                };
                                callbacks.PermissionDeniedAndDontAskAgain += permName =>
                                {
                                    Debug.Log("[DevicePermissionManager] POST_NOTIFICATIONS permanently denied. Opening App Settings...");
                                    PlayerPrefs.SetInt(PrefKey_NotificationEnabled, 0);
                                    PlayerPrefs.Save();
                                    OpenAppSettings();
                                };
                                Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS", callbacks);
                                // Return false now; the callbacks above will handle saving '1' if granted.
                                return false;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[DevicePermissionManager] SetNotificationEnabledSetting error: {ex.Message}");
                    }
#endif
#if UNITY_IOS && !UNITY_EDITOR
                    // On iOS, the permission dialog is shown automatically on first launch by the package.
                    // If the user reaches here with no permission, they denied it — send them to Settings.
                    Debug.Log("[DevicePermissionManager] iOS notification permission denied. Directing user to App Settings.");
                    OpenAppSettings();
#endif
                    // Keep setting 0 until OS permission is confirmed granted
                    PlayerPrefs.SetInt(PrefKey_NotificationEnabled, 0);
                    PlayerPrefs.Save();
                    return false;
                }
            }
        }
    }
}
