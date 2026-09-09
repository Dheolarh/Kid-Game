using UnityEngine;
using System;
using System.Collections.Generic;
using KidGame.Permissions;

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

        private void Start()
        {
            Debug.Log("[LocalNotificationManager] Start — calling ScheduleAllDynamicNotifications.");
            ScheduleAllDynamicNotifications();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Debug.Log($"[LocalNotificationManager] OnApplicationFocus(hasFocus={hasFocus})");
            if (!hasFocus)
            {
                ScheduleAllDynamicNotifications();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            Debug.Log($"[LocalNotificationManager] OnApplicationPause(pauseStatus={pauseStatus})");
            if (pauseStatus)
            {
                ScheduleAllDynamicNotifications();
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
                                Debug.LogWarning("[LocalNotificationManager] SCHEDULE_EXACT_ALARM not granted. " +
                                    "Redirecting to alarm permission settings so notifications fire on time.");
                                using (var intent = new AndroidJavaObject(
                                    "android.content.Intent",
                                    "android.settings.REQUEST_SCHEDULE_EXACT_ALARM"))
                                {
                                    activity.Call("startActivity", intent);
                                }
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
            Debug.Log("[LocalNotificationManager] CancelAllNotifications — cancelling managed IDs (1001–1074).");
#if UNITY_ANDROID
            try
            {
                // Cancel only the managed daily notification IDs (7 days × 4 slots = IDs 1001–1074)
                // instead of wiping ALL scheduled notifications. This preserves the test notification
                // (ID 9999) which would otherwise be cancelled when OnApplicationPause triggers
                // ScheduleAllDynamicNotifications → CancelAllNotifications.
                for (int dayOffset = 0; dayOffset < 7; dayOffset++)
                {
                    int idBase = 1000 + (dayOffset * 10);
                    AndroidNotificationCenter.CancelScheduledNotification(idBase + 1);
                    AndroidNotificationCenter.CancelScheduledNotification(idBase + 2);
                    AndroidNotificationCenter.CancelScheduledNotification(idBase + 3);
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

            string playerName = PlayerPrefs.GetString("SingleWordName", "Player");
            if (string.IsNullOrEmpty(playerName)) playerName = "Player";

            string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
            string lastLoginStr = PlayerPrefs.GetString("Streak_LastLoginDate", "");
            int lastClaimedDay = PlayerPrefs.GetInt("Streak_LastClaimedDay", 0);
            int streakDays = PlayerPrefs.GetInt("Streak_DaysCount", 1);

            bool isStreakKeptToday = (lastLoginStr == todayStr && lastClaimedDay >= streakDays);

            DateTime now = DateTime.Now;

            // Schedule for the next 7 days in advance
            // Even if the user doesn't open the app for a full week, the OS will continue firing 4 notifications/day
            for (int dayOffset = 0; dayOffset < 7; dayOffset++)
            {
                DateTime dayBase = DateTime.Today.AddDays(dayOffset);

                // Define 4 daily time windows
                DateTime morningTime = new DateTime(dayBase.Year, dayBase.Month, dayBase.Day, 8, 0, 0);
                DateTime afternoonTime = new DateTime(dayBase.Year, dayBase.Month, dayBase.Day, 12, 30, 0);
                DateTime eveningTime = new DateTime(dayBase.Year, dayBase.Month, dayBase.Day, 16, 30, 0);
                DateTime streakSaverTime = new DateTime(dayBase.Year, dayBase.Month, dayBase.Day, 18, 0, 0);

                // Today: use streak-reminder messages only if streak is broken.
                // Future days: always use learner messages (we can't know future streak state).
                bool useLearnerMessages = (dayOffset > 0) || isStreakKeptToday;
                int idBase = 1000 + (dayOffset * 10);

                // 1. Morning (8:00 AM)
                if (morningTime > now)
                {
                    var msg = useLearnerMessages 
                        ? GetRandomMessage(MorningLearnerMessages, playerName)
                        : GetRandomMessage(StreakReminderMessages, playerName);
                    ScheduleNotificationAt(msg.Title, msg.Body, morningTime, idBase + 1);
                }

                // 2. Afternoon (12:30 PM)
                if (afternoonTime > now)
                {
                    var msg = useLearnerMessages
                        ? GetRandomMessage(AfternoonLearnerMessages, playerName)
                        : GetRandomMessage(StreakReminderMessages, playerName);
                    ScheduleNotificationAt(msg.Title, msg.Body, afternoonTime, idBase + 2);
                }

                // 3. Evening (4:30 PM)
                if (eveningTime > now)
                {
                    var msg = useLearnerMessages
                        ? GetRandomMessage(EveningLearnerMessages, playerName)
                        : GetRandomMessage(StreakReminderMessages, playerName);
                    ScheduleNotificationAt(msg.Title, msg.Body, eveningTime, idBase + 3);
                }

                // 4. Streak Saver (6:00 PM)
                if (streakSaverTime > now)
                {
                    var msg = GetRandomMessage(StreakSaverMessages, playerName);
                    ScheduleNotificationAt(msg.Title, msg.Body, streakSaverTime, idBase + 4);
                }
            }
        }

        public static void ScheduleTestNotification(string title, string bodyText, int delaySeconds = 5)
        {
            Debug.Log($"[LocalNotificationManager] ScheduleTestNotification: '{title}' in {delaySeconds}s (ID=9999). HasPermission={DevicePermissionManager.HasNotificationPermission()}, IsEnabled={DevicePermissionManager.IsNotificationEnabled()}");
            ScheduleNotificationAt(title, bodyText, DateTime.Now.AddSeconds(delaySeconds), 9999);
        }

        private static void ScheduleNotificationAt(string title, string bodyText, DateTime fireTime, int id)
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
                    ShowInForeground = true,
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
                    ShowInForeground = true,
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
                    Sound = "notification.mp3",
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
                msg.Title.Replace("{playername}", playerName),
                msg.Body.Replace("{playername}", playerName)
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
            new NotificationMessage("Breakfast & Math Time! 🥞", "Join your mascot for morning math fun!"),
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
            new NotificationMessage("Afternoon Quiz Time! 💡", "Show your mascot how smart you are, {playername}!"),
            new NotificationMessage("Beat the Clock {playername}! ⏰", "A 2-minute math game is waiting for you!"),
            new NotificationMessage("Hey Master {playername}! 🥇", "Time for your afternoon brain workout!"),
            new NotificationMessage("Fun Afternoon {playername}! 🎈", "Tracing numbers is super fun today!"),
            new NotificationMessage("Afternoon Smile {playername}! 😊", "Your mascot missed you today!"),
            new NotificationMessage("High Five {playername}! 🖐️", "Let's do some super fast counting!")
        };

        // 3. Evening Learner Messages (4:00 PM - 5:00 PM)
        private static readonly List<NotificationMessage> EveningLearnerMessages = new List<NotificationMessage>()
        {
            new NotificationMessage("Evening Math Play! 🌆", "Hey {playername}, wind down with a quick math game!"),
            new NotificationMessage("Sunset Puzzles {playername}! 🌇", "Finish your day with awesome stars!"),
            new NotificationMessage("Playtime Math {playername}! 🧸", "Before dinner, let's solve some numbers!"),
            new NotificationMessage("Hey Super Star {playername}! ⭐", "Show us your evening math skills!"),
            new NotificationMessage("Evening Challenge! 🎯", "Can {playername} complete today's daily goal?"),
            new NotificationMessage("Good Evening {playername}! 🌙", "Your buddy wants to play one last game!"),
            new NotificationMessage("Wrap Up the Day! 🏆", "Earn your stars, {playername}!"),
            new NotificationMessage("Relax & Play {playername}! 🛋️", "Fun puzzles are ready for you tonight!"),
            new NotificationMessage("Evening Sparkle {playername}! ✨", "Let's make learning glow tonight!"),
            new NotificationMessage("Smart Cookie {playername}! 🍪", "A quick math game before bedtime story!"),
            new NotificationMessage("Nightfall Fun {playername}! 🌌", "Top the game with evening points!"),
            new NotificationMessage("Hey {playername}! 🎨", "Draw and trace your favorite numbers!"),
            new NotificationMessage("Evening Champ {playername}! 👑", "You're doing amazing today!"),
            new NotificationMessage("Almost Bedtime {playername}! 🛌", "One quick puzzle before rest time!"),
            new NotificationMessage("Great Job Today {playername}! 🌟", "Come claim your evening bonus stars!")
        };

        // 4. Evening Streak Saver Messages (around 6:00 PM)
        private static readonly List<NotificationMessage> StreakSaverMessages = new List<NotificationMessage>()
        {
            new NotificationMessage("Don't Lose Your Streak {playername}! 🔥", "You have a daily streak going! Play now to keep it!"),
            new NotificationMessage("Save Your Streak {playername}! ⏰", "Only a few hours left to keep your streak alive!"),
            new NotificationMessage("Streak Warning {playername}! ⚠️", "Log in now to claim today's streak reward!"),
            new NotificationMessage("Keep It Going {playername}! 🚀", "Your streak count is waiting for you!"),
            new NotificationMessage("Quick Streak Check {playername}! 🏆", "Don't let your hard work reset to 0!"),
            new NotificationMessage("Hey {playername}! 🔥", "Claim today's streak reward before midnight!"),
            new NotificationMessage("Protect Your Streak {playername}! 🛡️", "1 minute of play saves your streak!"),
            new NotificationMessage("Streak Emergency {playername}! 🚨", "Play now to keep your daily streak!"),
            new NotificationMessage("Keep the Fire Alive {playername}! 🕯️", "Your daily streak is waiting for you!"),
            new NotificationMessage("Almost Time {playername}! ⏳", "Log in now to save your daily streak!"),
            new NotificationMessage("Streak Protector {playername}! 🦸", "Save your daily streak today!"),
            new NotificationMessage("Don't Miss Out {playername}! 🎁", "Daily streak rewards are ready!"),
            new NotificationMessage("Hey Champ {playername}! 🥇", "One quick game to extend your streak!"),
            new NotificationMessage("Keep the Flame {playername}! 🔥", "Play now and keep your streak safe!"),
            new NotificationMessage("Night Streak Alert {playername}! 🌙", "Claim your streak before the day ends!")
        };

        // 5. Unkept / Broken Streak Reminders (Replaces Morning, Afternoon, Evening when streak is unkept!)
        private static readonly List<NotificationMessage> StreakReminderMessages = new List<NotificationMessage>()
        {
            new NotificationMessage("Your Streak is Saved {playername}! 🔥", "Log in now to start fresh and rebuild your streak!"),
            new NotificationMessage("We Miss You {playername}! 😢", "Come back to Numeracy and restart your daily streak!"),
            new NotificationMessage("Start a New Streak {playername}! 🚀", "Today is a great day to begin a new streak!"),
            new NotificationMessage("Your Mascot Misses You {playername}! 🐶", "Let's start a brand new streak today!"),
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
