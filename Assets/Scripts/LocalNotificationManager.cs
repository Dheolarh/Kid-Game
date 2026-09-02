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

        public const string ChannelId = "numeracy_daily_notifications_channel";
        public const string ChannelName = "Numeracy Daily Reminders";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeNotificationChannel();
        }

        private void InitializeNotificationChannel()
        {
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
                Debug.Log("[LocalNotificationManager] Unity Android Notification Channel registered.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] Unity Android Notification Channel exception: {ex.Message}");
            }
#endif
        }

        public static void CancelAllNotifications()
        {
#if UNITY_ANDROID
            try { AndroidNotificationCenter.CancelAllScheduledNotifications(); } catch {}
#endif
#if UNITY_IOS
            try { iOSNotificationCenter.RemoveAllScheduledNotifications(); } catch {}
#endif
        }

        public static void ScheduleAllDynamicNotifications()
        {
            if (!DevicePermissionManager.IsNotificationEnabled())
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

            // Define Time Windows for Notifications
            // 1. Morning (8:00 AM)
            DateTime morningTime = GetNextOccurrence(8, 0);
            // 2. Afternoon (12:30 PM)
            DateTime afternoonTime = GetNextOccurrence(12, 30);
            // 3. Evening (4:30 PM)
            DateTime eveningTime = GetNextOccurrence(16, 30);
            // 4. Streak Saver (6:00 PM)
            DateTime streakSaverTime = GetNextOccurrence(18, 0);

            if (isStreakKeptToday)
            {
                // Streak is active/kept today -> Use Learning Notifications
                var morningMsg = GetRandomMessage(MorningLearnerMessages, playerName);
                ScheduleNotificationAt(morningMsg.Title, morningMsg.Body, morningTime, id: 1001);

                var afternoonMsg = GetRandomMessage(AfternoonLearnerMessages, playerName);
                ScheduleNotificationAt(afternoonMsg.Title, afternoonMsg.Body, afternoonTime, id: 1002);

                var eveningMsg = GetRandomMessage(EveningLearnerMessages, playerName);
                ScheduleNotificationAt(eveningMsg.Title, eveningMsg.Body, eveningTime, id: 1003);

                var streakSaverMsg = GetRandomMessage(StreakSaverMessages, playerName);
                ScheduleNotificationAt(streakSaverMsg.Title, streakSaverMsg.Body, streakSaverTime, id: 1004);
            }
            else
            {
                // Streak is unkept / missed -> Swap out ALL slots to Streak Reminder Notifications!
                var morningStreakMsg = GetRandomMessage(StreakReminderMessages, playerName);
                ScheduleNotificationAt(morningStreakMsg.Title, morningStreakMsg.Body, morningTime, id: 1001);

                var afternoonStreakMsg = GetRandomMessage(StreakReminderMessages, playerName);
                ScheduleNotificationAt(afternoonStreakMsg.Title, afternoonStreakMsg.Body, afternoonTime, id: 1002);

                var eveningStreakMsg = GetRandomMessage(StreakReminderMessages, playerName);
                ScheduleNotificationAt(eveningStreakMsg.Title, eveningStreakMsg.Body, eveningTime, id: 1003);

                var streakSaverMsg = GetRandomMessage(StreakSaverMessages, playerName);
                ScheduleNotificationAt(streakSaverMsg.Title, streakSaverMsg.Body, streakSaverTime, id: 1004);
            }
        }

        private static DateTime GetNextOccurrence(int hour, int minute)
        {
            DateTime target = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, hour, minute, 0);
            if (target <= DateTime.Now)
            {
                target = target.AddDays(1);
            }
            return target;
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
                    SmallIcon = "app_icon",
                    LargeIcon = "app_icon"
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
                var timeTrigger = new iOSNotificationTimeIntervalTrigger()
                {
                    TimeInterval = fireTime - DateTime.Now,
                    Repeats = false
                };

                var notification = new iOSNotification()
                {
                    Identifier = $"notification_{id}",
                    Title = title,
                    Body = bodyText,
                    ShowInForeground = true,
                    ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
                    CategoryIdentifier = "category_a",
                    ThreadIdentifier = "thread1",
                    Trigger = timeTrigger,
                };
                iOSNotificationCenter.ScheduleNotification(notification);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalNotificationManager] iOS Schedule error: {ex.Message}");
            }
#else
            Debug.Log($"[LocalNotificationManager] Scheduled Notification #{id} for {fireTime} (Editor Simulation): [{title}] - {bodyText}");
#endif
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ScheduleAllDynamicNotifications();
            }
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
