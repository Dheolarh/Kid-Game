using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using KidGame.Permissions;
using KidGame.Interface;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace KidGame.Notifications
{
    /// <summary>
    /// Advanced Dynamic Local Notification System for Android & iOS.
    /// Manages time-based Learning Notifications (Morning 7-9 AM, Afternoon 11 AM-2 PM, Evening 4-5 PM)
    /// and Streak Saver Notifications (around 6 PM).
    /// If a streak was unkept/missed, automatically swaps out learner notifications with Streak Reminders
    /// until the player logs back in. Features 15+ dynamic text variants per category.
    /// </summary>
    public class LocalNotificationManager : MonoBehaviour
    {
        public static LocalNotificationManager Instance { get; private set; }

        // Channel ID bumped to v2 because Android permanently caches a channel's sound after first creation.
        // Changing the sound on an existing channel ID is silently ignored — a new ID forces recreation.
        public const string ChannelId = "numeracy_daily_notifications_channel_v2";
        public const string ChannelName = "Numeracy Daily Reminders";

        // Raw resource URI for the custom notification sound (Assets/Plugins/Android/res/raw/notification.mp3).
        // Format: android.resource://[applicationId]/raw/[filename_without_extension]
        private static string NotificationSoundUri => $"android.resource://{Application.identifier}/raw/notification";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.Log("[LocalNotificationManager] Duplicate instance detected — destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[LocalNotificationManager] Awake — Instance set. Initializing notification channel.");

            InitializeNotificationChannel();
        }

        private float _lastResumeCheckTime = -10f;
        private string _lastHandledNotificationId = "";

        private void Start()
        {
            Debug.Log("[LocalNotificationManager] Start — checking launch notification and scheduling notifications.");
            CheckNotificationEntry();
            ScheduleAllDynamicNotifications();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Debug.Log($"[LocalNotificationManager] OnApplicationFocus(hasFocus={hasFocus})");
            if (!hasFocus)
            {
                ScheduleAllDynamicNotifications();
            }
            else
            {
                OnAppResumeOrFocus();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            Debug.Log($"[LocalNotificationManager] OnApplicationPause(pauseStatus={pauseStatus})");
            if (pauseStatus)
            {
                ScheduleAllDynamicNotifications();
            }
            else
            {
                OnAppResumeOrFocus();
            }
        }

        private void OnAppResumeOrFocus()
        {
            if (Time.realtimeSinceStartup - _lastResumeCheckTime < 1.0f) return;
            _lastResumeCheckTime = Time.realtimeSinceStartup;

            Debug.Log("[LocalNotificationManager] App resumed / gained focus — checking notification entry & daily streak.");

            CheckNotificationEntry();
            CheckDailyStreakOnResume();
            ScheduleAllDynamicNotifications();
        }

        private void CheckNotificationEntry()
        {
#if UNITY_IOS && !UNITY_EDITOR
            StartCoroutine(CheckiOSNotificationResponseCoroutine());
#elif UNITY_ANDROID && !UNITY_EDITOR
            CheckAndroidNotificationResponse();
#endif
        }

#if UNITY_IOS
        private IEnumerator CheckiOSNotificationResponseCoroutine()
        {
            var op = iOSNotificationCenter.QueryLastRespondedNotification();
            yield return op;

            if (op.State == QueryLastRespondedNotificationState.HaveRespondedNotification)
            {
                var notification = op.Notification;
                if (notification != null)
                {
                    string id = notification.Identifier ?? "";
                    if (id != _lastHandledNotificationId)
                    {
                        _lastHandledNotificationId = id;
                        Debug.Log($"[LocalNotificationManager] Responded to iOS notification: ID={id}, Title='{notification.Title}'");
                        HandleNotificationOpened();
                    }
                }
            }
        }
#endif

#if UNITY_ANDROID
        private void CheckAndroidNotificationResponse()
        {
            try
            {
                var intentData = AndroidNotificationCenter.GetLastNotificationIntent();
                if (intentData != null)
                {
                    string id = $"android_{intentData.Id}";
                    if (id != _lastHandledNotificationId)
                    {
                        _lastHandledNotificationId = id;
                        Debug.Log($"[LocalNotificationManager] Responded to Android notification: ID={intentData.Id}, Channel='{intentData.Channel}'");
                        HandleNotificationOpened();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] Android notification intent check error: {ex.Message}");
            }
        }
#endif

        private void HandleNotificationOpened()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            Debug.Log($"[LocalNotificationManager] HandleNotificationOpened in scene: '{currentScene}'");

            if (currentScene != "Main")
            {
                // User opened app via notification while in Level or Game scene -> transition back to Main
                if (SceneTransitionManager.Instance != null)
                {
                    SceneTransitionManager.Instance.LoadSceneWithTransition("Main");
                }
                else
                {
                    SceneManager.LoadScene("Main");
                }
            }
            else
            {
                CheckDailyStreakOnResume();
            }
        }

        public static void CheckDailyStreakOnResume()
        {
            // If user hasn't finished initial registration, don't trigger streak prematurely
            if (PlayerPrefs.GetInt("HasCompletedProfile", 0) == 0 && !PlayerPrefs.HasKey("Streak_LastLoginDate"))
            {
                return;
            }

            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == "Main")
            {
                var menuMgr = MenuScreenManager.Instance;
                if (menuMgr == null)
                {
                    menuMgr = FindObjectOfType<MenuScreenManager>(true);
                }

                bool isSplashActive = (menuMgr != null && menuMgr.IsSplashScreenActive);
                if (isSplashActive)
                {
                    Debug.Log("[LocalNotificationManager] Splash is active in Main scene — splash completion will present streak.");
                    return;
                }

                StreakPanelController.CheckStreak();
            }
            else
            {
                StreakPanelController.UpdateStreakPrefsOnly();
            }
        }

        private void InitializeNotificationChannel()
        {
            Debug.Log("[LocalNotificationManager] InitializeNotificationChannel — starting.");
#if UNITY_ANDROID
            try
            {
                var channel = new AndroidNotificationChannel()
                {
                    Id = ChannelId,
                    Name = ChannelName,
                    Importance = Importance.High,
                    Description = "Reminds you to play daily, learn maths, and keep your streak alive.",
                };
                AndroidNotificationCenter.RegisterNotificationChannel(channel);
                Debug.Log($"[LocalNotificationManager] Channel '{ChannelId}' registered with Importance.High.");

#if !UNITY_EDITOR
                // The Unity package doesn't expose a sound property on AndroidNotificationChannel,
                // so we apply the custom sound directly via the Android Java API after registration.
                // Only takes effect on first channel creation — Android permanently locks the sound
                // after the channel is registered (which is why we bumped to channel ID v2).
                try
                {
                    using var context = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                        .GetStatic<AndroidJavaObject>("currentActivity");
                    using var nm = context.Call<AndroidJavaObject>("getSystemService", "notification");
                    Debug.Log("[LocalNotificationManager] Got NotificationManager from system service.");
                    using var javaChannel = nm.Call<AndroidJavaObject>("getNotificationChannel", ChannelId);
                    if (javaChannel != null)
                    {
                        Debug.Log($"[LocalNotificationManager] Found Java channel '{ChannelId}'. Applying custom sound URI: {NotificationSoundUri}");
                        using var uri = new AndroidJavaClass("android.net.Uri")
                            .CallStatic<AndroidJavaObject>("parse", NotificationSoundUri);
                        using var audioAttribs = new AndroidJavaObject(
                            "android.media.AudioAttributes$Builder")
                            .Call<AndroidJavaObject>("setUsage", 5)       // USAGE_NOTIFICATION
                            .Call<AndroidJavaObject>("setContentType", 4) // CONTENT_TYPE_SONIFICATION
                            .Call<AndroidJavaObject>("build");
                        javaChannel.Call("setSound", uri, audioAttribs);
                        nm.Call("createNotificationChannel", javaChannel);
                        Debug.Log("[LocalNotificationManager] Custom notification sound applied via Java API.");
                    }
                    else
                    {
                        Debug.LogWarning("[LocalNotificationManager] Java channel object was null — sound not applied.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LocalNotificationManager] Could not set custom channel sound: {ex.Message}\n{ex.StackTrace}");
                }
#endif
                Debug.Log("[LocalNotificationManager] Unity Android Notification Channel registered.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] Unity Android Notification Channel exception: {ex.Message}\n{ex.StackTrace}");
            }

#if !UNITY_EDITOR
            // On Android 12+ (API 31+), SCHEDULE_EXACT_ALARM must be granted by the user.
            // Without it, FireTime is treated as inexact and may fire hours late on Go devices.
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = version.GetStatic<int>("SDK_INT");
                    Debug.Log($"[LocalNotificationManager] Exact alarm check: SDK_INT={sdkInt}");
                    if (sdkInt >= 31)
                    {
                        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var alarmManager = activity.Call<AndroidJavaObject>("getSystemService", "alarm"))
                        {
                            bool canScheduleExact = alarmManager.Call<bool>("canScheduleExactAlarms");
                            Debug.Log($"[LocalNotificationManager] canScheduleExactAlarms = {canScheduleExact}");
                            if (!canScheduleExact)
                            {
                                // Log only — do NOT redirect to settings here.
                                // Redirecting on first launch hijacks the user before they see the app,
                                // and can interrupt channel registration. On Android 12+, inexact alarms
                                // are still delivered (just potentially delayed by a few minutes), which
                                // is acceptable for daily reminder notifications.
                                Debug.LogWarning("[LocalNotificationManager] SCHEDULE_EXACT_ALARM not granted. " +
                                    "Notifications will fire with inexact timing (acceptable for daily reminders).");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] Exact alarm permission check error: {ex.Message}\n{ex.StackTrace}");
            }
#endif
#endif
        }

        public static void CancelAllNotifications()
        {
            Debug.Log("[LocalNotificationManager] CancelAllNotifications — cancelling managed IDs (1001–1073).");
#if UNITY_ANDROID
            try
            {
                // Cancel only the managed daily notification IDs (7 days × 3 slots = IDs 1001–1073)
                // instead of wiping ALL scheduled notifications. This preserves the test notification
                // (ID 9999) which would otherwise be cancelled when OnApplicationPause triggers
                // ScheduleAllDynamicNotifications → CancelAllNotifications.
                for (int dayOffset = 0; dayOffset < 7; dayOffset++)
                {
                    int idBase = 1000 + (dayOffset * 10);
                    AndroidNotificationCenter.CancelScheduledNotification(idBase + 1);
                    AndroidNotificationCenter.CancelScheduledNotification(idBase + 2);
                    AndroidNotificationCenter.CancelScheduledNotification(idBase + 3);
                    // Slot +4 (the old 6:00 PM streak saver) is retired, but cancel it anyway so
                    // schedules created by a previous build are cleaned up on upgrade.
                    AndroidNotificationCenter.CancelScheduledNotification(idBase + 4);
                }
                Debug.Log("[LocalNotificationManager] Android: Managed notification IDs cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] CancelAllNotifications error: {ex.Message}");
            }
#endif
#if UNITY_IOS
            try
            {
                for (int dayOffset = 0; dayOffset < 7; dayOffset++)
                {
                    int idBase = 1000 + (dayOffset * 10);
                    iOSNotificationCenter.RemoveScheduledNotification($"notification_{idBase + 1}");
                    iOSNotificationCenter.RemoveScheduledNotification($"notification_{idBase + 2}");
                    iOSNotificationCenter.RemoveScheduledNotification($"notification_{idBase + 3}");
                    // Retired slot — removed for upgrade safety (see Android note above).
                    iOSNotificationCenter.RemoveScheduledNotification($"notification_{idBase + 4}");
                }
                Debug.Log("[LocalNotificationManager] iOS: Managed notification IDs cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] CancelAllNotifications error: {ex.Message}");
            }
#endif
        }

        public static void ScheduleAllDynamicNotifications()
        {
            bool isEnabled = DevicePermissionManager.IsNotificationEnabled();
            Debug.Log($"[LocalNotificationManager] ScheduleAllDynamicNotifications called. IsNotificationEnabled={isEnabled}");
            if (!isEnabled)
            {
                Debug.Log("[LocalNotificationManager] Notifications disabled in settings or missing OS permission. Skipping schedule.");
                return;
            }

            CancelAllNotifications();

            string playerName = PlayerPrefs.GetString("SingleWordName", "");
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = PlayerPrefs.GetString("PlayerName", "");
                if (!string.IsNullOrEmpty(playerName))
                {
                    playerName = playerName.Trim().Split(' ')[0];
                }
            }
            if (string.IsNullOrEmpty(playerName)) playerName = "Player";

            string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
            string lastLoginStr = PlayerPrefs.GetString("Streak_LastLoginDate", "");

            // Streak already claimed today: every remaining slot today is cancelled. The next day reverts
            // to learner messages until the evening, when an unclaimed streak becomes a streak reminder.
            bool isStreakKeptToday = (lastLoginStr == todayStr);

            // Streak broken: the player missed at least one full day, so the run is over. Once that has
            // happened every notification becomes a streak reminder (all three slots, every day) until
            // they come back and log in again.
            //   gap 0 = already logged in today      -> not broken
            //   gap 1 = logged in yesterday          -> streak alive, just hasn't played yet today
            //   gap 2+ = missed a whole day          -> broken
            // A missing/unparseable date means a brand-new install, which counts as not broken.
            bool streakBroken = false;
            if (!string.IsNullOrEmpty(lastLoginStr) &&
                DateTime.TryParse(lastLoginStr, out DateTime lastLoginDate))
            {
                streakBroken = (DateTime.Today - lastLoginDate.Date).TotalDays > 1;
            }

            DateTime now = DateTime.Now;

            // Schedule the next 7 days. Only "today" can be planned accurately — future days are
            // re-planned each time the app opens (OnApplicationFocus / OnApplicationPause), so for those
            // we assume the worst case (no login). If the player does open the app, this whole schedule
            // is torn down and rebuilt.
            for (int dayOffset = 0; dayOffset < 7; dayOffset++)
            {
                DateTime dayBase = DateTime.Today.AddDays(dayOffset);
                bool isToday = (dayOffset == 0);

                // Three slots per day. The 4:30 PM evening slot was removed at the designer's request —
                // the 6:00 PM slot covers the evening.
                DateTime morningTime = new DateTime(dayBase.Year, dayBase.Month, dayBase.Day, 8, 0, 0);
                DateTime afternoonTime = new DateTime(dayBase.Year, dayBase.Month, dayBase.Day, 12, 30, 0);
                DateTime eveningTime = new DateTime(dayBase.Year, dayBase.Month, dayBase.Day, 18, 0, 0);

                // Today: everything is cancelled if the streak was already claimed.
                // Future days: scheduled as if unclaimed; the next app-open re-plans them.
                if (isToday && isStreakKeptToday) continue;

                int idBase = 1000 + (dayOffset * 10);

                // A broken streak turns every slot into a streak reminder. Otherwise only the evening
                // escalates once the player has had a morning and an afternoon prompt.
                var morningList   = streakBroken ? StreakReminderMessages : MorningLearnerMessages;
                var afternoonList = streakBroken ? StreakReminderMessages : AfternoonLearnerMessages;

                // 1. Morning (8:00 AM)
                if (morningTime > now)
                {
                    var msg = GetRandomMessage(morningList, playerName);
                    ScheduleNotificationAt(msg.Title, msg.Body, morningTime, idBase + 1);
                }

                // 2. Afternoon (12:30 PM)
                if (afternoonTime > now)
                {
                    var msg = GetRandomMessage(afternoonList, playerName);
                    ScheduleNotificationAt(msg.Title, msg.Body, afternoonTime, idBase + 2);
                }

                // 3. Evening (6:00 PM) — streak reminder. There is no evening learner notification;
                // by this point the player has had a morning and an afternoon prompt.
                if (eveningTime > now)
                {
                    var msg = GetRandomMessage(StreakReminderMessages, playerName);
                    ScheduleNotificationAt(msg.Title, msg.Body, eveningTime, idBase + 3);
                }
            }
        }

        public static void ScheduleTestNotification(string title, string bodyText, int delaySeconds = 5)
        {
            Debug.Log($"[LocalNotificationManager] ScheduleTestNotification: '{title}' in {delaySeconds}s (ID=9999). HasPermission={DevicePermissionManager.HasNotificationPermission()}, IsEnabled={DevicePermissionManager.IsNotificationEnabled()}");
            ScheduleNotificationAt(title, bodyText, DateTime.Now.AddSeconds(delaySeconds), 9999, true);
        }

        private static void ScheduleNotificationAt(string title, string bodyText, DateTime fireTime, int id, bool showInForeground = false)
        {
#if UNITY_ANDROID
            try
            {
                var notification = new AndroidNotification()
                {
                    Title = title,
                    Text = bodyText,
                    FireTime = fireTime,
                    SmallIcon = "icon_0",
                    LargeIcon = "icon_1",
                    ShowInForeground = showInForeground,
                    ShowTimestamp = true,
                    IntentData = "open_app"
                };
                AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, id);
                Debug.Log($"[LocalNotificationManager] Scheduled Android Notification #{id} for {fireTime}: '{title}'");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] Android Schedule error: {ex.Message}");
            }
#elif UNITY_IOS
            try
            {
                // Snap fireTime to at least 1 second in the future to guard against a race where
                // DateTime.Now advances between the caller's (fireTime > now) check and here,
                // producing a zero or negative TimeSpan that iOS rejects with an exception.
                TimeSpan interval = fireTime - DateTime.Now;
                if (interval.TotalSeconds < 1.0)
                    interval = TimeSpan.FromSeconds(1);

                var timeTrigger = new iOSNotificationTimeIntervalTrigger()
                {
                    TimeInterval = interval,
                    Repeats = false
                };

                var notification = new iOSNotification()
                {
                    Identifier = $"notification_{id}",
                    Title = title,
                    Body = bodyText,
                    ShowInForeground = showInForeground,
                    // PresentationOption.Alert is deprecated since iOS 14.
                    // Use Banner (heads-up display) + List (notification centre) + Sound + Badge.
                    ForegroundPresentationOption = (PresentationOption.Banner
                        | PresentationOption.List
                        | PresentationOption.Sound
                        | PresentationOption.Badge),
                    CategoryIdentifier = "category_a",
                    ThreadIdentifier = "thread1",
                    // Custom sound: file must exist in the app bundle root.
                    // Assets/Plugins/iOS/notification.mp3 is copied there by Unity at Xcode export time.
                    // iOS requires sounds to be <= 30 seconds; longer files fall back to the default sound.
                    // Note: the property is SoundName (SoundType was the old, pre-2.x name).
                    SoundName = "notification.mp3",
                    Trigger = timeTrigger,
                };
                iOSNotificationCenter.ScheduleNotification(notification);
                Debug.Log($"[LocalNotificationManager] Scheduled iOS Notification #{id} for {fireTime}: '{title}'");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] iOS Schedule error: {ex.Message}");
            }
#else
            Debug.Log($"[LocalNotificationManager] Scheduled Notification #{id} for {fireTime} (Editor Simulation): [{title}] - {bodyText}");
#endif
        }

        private struct NotificationMessage
        {
            public string Title;
            public string Body;
            public NotificationMessage(string t, string b) { Title = t; Body = b; }
        }

        private static NotificationMessage GetRandomMessage(List<NotificationMessage> list, string playerName)
        {
            if (list == null || list.Count == 0) return new NotificationMessage("Numeracy", "Time to learn maths!");
            int randIndex = UnityEngine.Random.Range(0, list.Count);
            var msg = list[randIndex];
            return new NotificationMessage(
                ReplacePlayerName(msg.Title, playerName),
                ReplacePlayerName(msg.Body, playerName)
            );
        }

        private static string ReplacePlayerName(string template, string playerName)
        {
            if (string.IsNullOrEmpty(template)) return "";
            return System.Text.RegularExpressions.Regex.Replace(
                template,
                @"\{playername\}",
                playerName,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

        #region 15+ Dynamic Notification Variants per Category

        // 1. Morning Learner Messages (7:00 AM - 9:00 AM)
        private static readonly List<NotificationMessage> MorningLearnerMessages = new List<NotificationMessage>()
        {
            new NotificationMessage("Wakey Wakey {playername}! 😴", "It's time to learn maths and unlock fun numbers!"),
            new NotificationMessage("Good Morning, {playername}! ☀️", "Start your day with a fun math puzzle!"),
            new NotificationMessage("Rise & Shine {playername}! ⏰", "Your math adventure is waiting for you!"),
            new NotificationMessage("Morning Champion {playername}! 🏆", "Let's count some stars together today!"),
            new NotificationMessage("Sun's Up {playername}! 🌅", "A quick math game makes the day bright!"),
            new NotificationMessage("Hey {playername}! 🎒", "Ready for your morning math power-up?"),
            new NotificationMessage("Early Bird {playername}! 🐦", "Early learners score the most stars today!"),
            new NotificationMessage("Top of the Morning {playername}! 🌟", "Let me show you a cool new math trick!"),
            new NotificationMessage("Breakfast & Math Time! 🥞", "Join Instructor Bee for morning math fun!"),
            new NotificationMessage("Ready to Play {playername}? 🎈", "Your numbers are waiting to be matched!"),
            new NotificationMessage("Morning Brain Boost! 💡", "Hey {playername}, let's solve 3 quick puzzles!"),
            new NotificationMessage("Hello Sunshine {playername}! ☀️", "Math time is happy time. Come play!"),
            new NotificationMessage("Bright Morning {playername}! 🌈", "Can you beat your score this morning?"),
            new NotificationMessage("Time for Fun {playername}! 🚀", "Blast off into today's math lesson!"),
            new NotificationMessage("Good Day {playername}! 🎨", "Color your morning with math magic!")
        };

        // 2. Afternoon Learner Messages (11:00 AM - 2:00 PM)
        private static readonly List<NotificationMessage> AfternoonLearnerMessages = new List<NotificationMessage>()
        {
            new NotificationMessage("Afternoon Math Break! 🥪", "Hey {playername}, take a break and play a puzzle!"),
            new NotificationMessage("Midday Fun {playername}! 🎮", "Time for your quick afternoon math game!"),
            new NotificationMessage("Lunchtime Puzzles! 🍎", "Feed your brain with fun numbers, {playername}!"),
            new NotificationMessage("Hey {playername}! ⚡", "Keep your learning power going with math!"),
            new NotificationMessage("Halfway Through the Day! ☀️", "{playername}, let's collect some extra stars!"),
            new NotificationMessage("Math Break Time {playername}! 🧩", "Solve a puzzle and level up today!"),
            new NotificationMessage("Afternoon Power! 🚀", "{playername}, your buddy is ready to play!"),
            new NotificationMessage("Ready for Math {playername}? 🎯", "Quick math game before afternoon play!"),
            new NotificationMessage("Star Collector {playername}! ⭐", "More stars are waiting for you right now!"),
            new NotificationMessage("Afternoon Quiz Time! 💡", "Show Instructor Bee how smart you are, {playername}!"),
            new NotificationMessage("Beat the Clock {playername}! ⏰", "A 2-minute math game is waiting for you!"),
            new NotificationMessage("Hey Master {playername}! 🥇", "Time for your afternoon brain workout!"),
            new NotificationMessage("Fun Afternoon {playername}! 🎈", "Tracing numbers is super fun today!"),
            new NotificationMessage("Afternoon Smile {playername}! 😊", "Instructor Bee missed you today!"),
            new NotificationMessage("High Five {playername}! 🖐️", "Let's do some super fast counting!")
        };

        // 3. Unkept / Broken Streak Reminders (replaces morning and afternoon when the streak is broken,
        //    and is always used for the 6:00 PM slot)
        private static readonly List<NotificationMessage> StreakReminderMessages = new List<NotificationMessage>()
        {
            new NotificationMessage("Your Streak is Saved {playername}! 🔥", "Log in now to start fresh and rebuild your streak!"),
            new NotificationMessage("We Miss You {playername}! 😢", "Come back to Numeracy and restart your daily streak!"),
            new NotificationMessage("Start a New Streak {playername}! 🚀", "Today is a great day to begin a new streak!"),
            new NotificationMessage("Instructor Bee Misses You {playername}! 🐝", "Let's start a brand new streak today!"),
            new NotificationMessage("Rebuild Your Streak {playername}! 🏰", "Claim today's Day 1 reward now!"),
            new NotificationMessage("Hey {playername}! 🌟", "Your daily streak reward is waiting to be claimed!"),
            new NotificationMessage("Fresh Start {playername}! 🌱", "Begin your new daily streak right now!"),
            new NotificationMessage("Don't Give Up {playername}! 💪", "Restart your streak and collect cool rewards!"),
            new NotificationMessage("Come Back & Play {playername}! 🎮", "A new streak reward is ready for you!"),
            new NotificationMessage("Ready to Bounce Back {playername}? ⚽", "Start your new streak today!"),
            new NotificationMessage("Hey Super Learner {playername}! ⭐", "Your next streak reward is waiting!"),
            new NotificationMessage("New Day, New Streak {playername}! ☀️", "Log in to claim today's streak reward!"),
            new NotificationMessage("We Kept a Gift for You! 🎁", "Hey {playername}, restart your streak now!"),
            new NotificationMessage("Back in Action {playername}! ⚡", "Let's build your longest streak ever!"),
            new NotificationMessage("Math Awaits {playername}! 📚", "Start a new streak and unlock fun sprites!")
        };

        #endregion
    }
}