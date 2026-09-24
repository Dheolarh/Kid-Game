using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using KidGame.Permissions;
using KidGame.Notifications;

#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif

/// <summary>
/// Attach to any GameObject in a dev/debug scene.
/// Wire up the four Button references in the Inspector.
/// Do a Development Build and open Android Logcat — filter by tag [Notif].
/// </summary>
public class NotificationDebugTester : MonoBehaviour
{
    [Header("Buttons — wire in Inspector")]
    public Button btnDiagnose;       // Prints full diagnosis to Logcat
    public Button btnTest5s;         // Schedules a test notification in 5 seconds
    public Button btnScheduleAll;    // Cancels + reschedules the full 7-day set
    public Button btnCancelAll;      // Cancels all managed notifications

    [Header("Optional — status text on screen")]
    public TextMeshProUGUI statusText;          // Drag a UI Text here to see results on device

    private void Start()
    {
        if (btnDiagnose)   btnDiagnose.onClick.AddListener(RunDiagnosis);
        if (btnTest5s)     btnTest5s.onClick.AddListener(ScheduleTest5s);
        if (btnScheduleAll)btnScheduleAll.onClick.AddListener(ScheduleAll);
        if (btnCancelAll)  btnCancelAll.onClick.AddListener(CancelAll);

        Log("NotificationDebugTester ready.");
    }

    // ─────────────────────────────────────────────
    // BUTTON 1 — Full Diagnosis
    // ─────────────────────────────────────────────
    public void RunDiagnosis()
    {
        Log("════════ NOTIFICATION DIAGNOSIS ════════");

        // 1. PlayerPrefs setting
        int savedSetting = PlayerPrefs.GetInt("Setting_NotificationEnabled", -1);
        Log($"[Prefs] Setting_NotificationEnabled = {savedSetting}  (1=on, 0=off, -1=never set)");

        // 2. DevicePermissionManager checks
        bool hasPermission = DevicePermissionManager.HasNotificationPermission();
        bool isEnabled     = DevicePermissionManager.IsNotificationEnabled();
        Log($"[Permission] HasNotificationPermission = {hasPermission}");
        Log($"[Permission] IsNotificationEnabled     = {isEnabled}");

#if UNITY_ANDROID && !UNITY_EDITOR
        // 3. SDK version
        try
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            int sdk = version.GetStatic<int>("SDK_INT");
            Log($"[Android] SDK_INT = {sdk}  (33=Android13, 31=Android12, 26=Android8)");

            // 4. POST_NOTIFICATIONS (Android 13+)
            if (sdk >= 33)
            {
                bool postNotifGranted = Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS");
                Log($"[Android] POST_NOTIFICATIONS granted = {postNotifGranted}");
                if (!postNotifGranted)
                    Log("[Android] ⚠ POST_NOTIFICATIONS not granted — notifications will NOT fire on Android 13+");
            }
            else
            {
                Log("[Android] SDK < 33 — POST_NOTIFICATIONS not required");
            }

            // 5. Exact alarm permission (Android 12+)
            if (sdk >= 31)
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var alarmMgr    = activity.Call<AndroidJavaObject>("getSystemService", "alarm");
                bool exactOk = alarmMgr.Call<bool>("canScheduleExactAlarms");
                Log($"[Android] canScheduleExactAlarms = {exactOk}");
                if (!exactOk)
                    Log("[Android] ⚠ Exact alarms not granted — notifications may be delayed by minutes/hours on Go devices");
            }

            // 6. Notification channel
            using var ctx = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            using var nm  = ctx.Call<AndroidJavaObject>("getSystemService", "notification");
            using var ch  = nm.Call<AndroidJavaObject>("getNotificationChannel", LocalNotificationManager.ChannelId);
            if (ch != null)
            {
                int importance = ch.Call<int>("getImportance");
                // IMPORTANCE_NONE=0, LOW=2, DEFAULT=3, HIGH=4, MAX=5
                string importanceLabel = importance switch { 0=>"NONE(blocked)", 2=>"LOW", 3=>"DEFAULT", 4=>"HIGH", 5=>"MAX", _ => importance.ToString() };
                Log($"[Android] Channel '{LocalNotificationManager.ChannelId}' found. Importance = {importanceLabel}");
                if (importance == 0)
                    Log("[Android] ⚠ Channel is BLOCKED by the user in system settings — no notifications will fire");
            }
            else
            {
                Log($"[Android] ⚠ Channel '{LocalNotificationManager.ChannelId}' NOT FOUND — channel was never registered");
            }

            // 7. Drawable icons
            using var resources = ctx.Call<AndroidJavaObject>("getResources");
            using var pkgName   = ctx.Call<AndroidJavaObject>("getPackageName");
            string pkg = pkgName.Call<string>("toString");
            int icon0Id = resources.Call<int>("getIdentifier", "icon_0", "drawable", pkg);
            int icon1Id = resources.Call<int>("getIdentifier", "icon_1", "drawable", pkg);
            Log($"[Android] Drawable 'icon_0' resource ID = {icon0Id}  (0 = NOT FOUND — notification will be dropped)");
            Log($"[Android] Drawable 'icon_1' resource ID = {icon1Id}  (0 = NOT FOUND — large icon missing, non-fatal)");
            if (icon0Id == 0)
                Log("[Android] ⚠ SmallIcon 'icon_0' missing — THIS SILENTLY KILLS NOTIFICATIONS on most Android versions");
        }
        catch (Exception ex)
        {
            Log($"[Android] Diagnosis exception: {ex.Message}");
        }
#else
        Log("[Platform] Running in Editor or iOS — Android checks skipped");
#endif

        // 8. Streak / player prefs
        string playerName   = PlayerPrefs.GetString("SingleWordName", PlayerPrefs.GetString("PlayerName", "(not set)"));
        string lastLogin    = PlayerPrefs.GetString("Streak_LastLoginDate", "(not set)");
        string todayStr     = DateTime.Today.ToString("yyyy-MM-dd");
        bool streakKept     = lastLogin == todayStr;
        Log($"[Prefs] PlayerName = '{playerName}'");
        Log($"[Prefs] Streak_LastLoginDate = '{lastLogin}'  (today = {todayStr}, kept = {streakKept})");

        Log("════════ END DIAGNOSIS ════════");
        SetStatus("Diagnosis done — check Logcat");
    }

    // ─────────────────────────────────────────────
    // BUTTON 2 — Schedule test in 5 seconds
    // ─────────────────────────────────────────────
    public void ScheduleTest5s()
    {
        Log("[Test] Scheduling test notification in 5 seconds (ID 9999)...");
        Log($"[Test] IsNotificationEnabled = {DevicePermissionManager.IsNotificationEnabled()}");
        Log($"[Test] HasPermission = {DevicePermissionManager.HasNotificationPermission()}");

        // Bypass the IsEnabled guard so we can test scheduling independently
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var notification = new AndroidNotification()
            {
                Title        = "Test Notification",
                Text         = "If you see this on Android, notifications work!",
                FireTime     = DateTime.Now.AddSeconds(5),
                SmallIcon    = "icon_0",
                LargeIcon    = "icon_1",
                ShowInForeground = true,
                ShowTimestamp    = true,
                IntentData       = "test"
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(
                notification, LocalNotificationManager.ChannelId, 9999);
            Log("[Test] AndroidNotificationCenter.SendNotificationWithExplicitID called — background the app now!");
        }
        catch (Exception ex)
        {
            Log($"[Test] Schedule exception: {ex.Message}");
        }
#else
        // Editor / iOS fallback — uses the manager
        LocalNotificationManager.ScheduleTestNotification("Test Notification", "If you see this, notifications work!", 5);
        Log("[Test] ScheduleTestNotification called (Editor/iOS path) — background the app now!");
#endif
        SetStatus("Test scheduled — background the app, wait 5s");
    }

    // ─────────────────────────────────────────────
    // BUTTON 3 — Full reschedule
    // ─────────────────────────────────────────────
    public void ScheduleAll()
    {
        Log("[ScheduleAll] Calling ScheduleAllDynamicNotifications...");
        LocalNotificationManager.ScheduleAllDynamicNotifications();
        Log("[ScheduleAll] Done.");
        SetStatus("All notifications rescheduled");
    }

    // ─────────────────────────────────────────────
    // BUTTON 4 — Cancel all
    // ─────────────────────────────────────────────
    public void CancelAll()
    {
        Log("[CancelAll] Calling CancelAllNotifications...");
        LocalNotificationManager.CancelAllNotifications();
        Log("[CancelAll] Done.");
        SetStatus("All notifications cancelled");
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────
    private void Log(string msg)
    {
        Debug.Log($"[Notif] {msg}");
    }

    private void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
    }
}
