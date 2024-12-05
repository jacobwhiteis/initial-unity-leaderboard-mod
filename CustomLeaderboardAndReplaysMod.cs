
#pragma warning disable IDE0051

using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

// Top of Akagi UH: -281.6374 172.5851 74.3028
// Top of Usui UH: 635.8738 192.6996 742.4423
// Bottom of Tsuchi DH: 1852.613 -245.3421 -1716.671
// Bottom of Akagi DH: -2327.301 -251.8515 -449.1758
// Bottom of Usui DH: -1431.378 -25.7216 -221.3933

// Invisible wall past finish line on Akagi DH is blocker 20 

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {
        private static readonly HttpClient httpClient = new();
        private static readonly Queue<Action> _executionQueue = new();
        private static readonly object _queueLock = new();

        // Static field that gets set to assist with uploading replays to leaderboard
        private static string uploadReplayJson;


        public override void OnInitializeMelon()
        {
            Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("CustomLeaderboardAndReplayMod initialized.");

            // Create the mod replay and ghost folders if not present
            string rootFolder = Utils.getRootFolder();
            string ghostPath = Path.Combine(rootFolder, "ModGhosts");
            string replayPath = Path.Combine(rootFolder, "ModReplays");

            if (!Directory.Exists(ghostPath))
            {
                Directory.CreateDirectory(ghostPath);
                MelonLogger.Msg($"Directory created: {ghostPath}");
            }
            if (!Directory.Exists(replayPath))
            {
                Directory.CreateDirectory(replayPath);
                MelonLogger.Msg($"Directory created: {replayPath}");
            }
        }


        // Method to enqueue actions to be executed on the main thread
        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            lock (_queueLock)
            {
                _executionQueue.Enqueue(() =>
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception in Enqueued Action: {ex.Message}");
                    }
                });
            }
        }

        public override void OnUpdate()
        {
            // Execute any queued actions on the main thread
            lock (_queueLock)
            {
                while (_executionQueue.Count > 0)
                {
                    var action = _executionQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception during task execution: {ex.Message}");
                    }
                }
            }
        }
    }


    // Patching the ProgressTracker to allow teleporting during testing
    [HarmonyPatch(typeof(ProgressTracker), "Start")]
    public class ProgressTrackerStartCancel
    {
        static bool Prefix() { return false; }
    }


    [HarmonyPatch(typeof(ProgressTracker), "FixedUpdate")]
    public class ProgressTrackerFixedUpdateCancel
    {
        static bool Prefix() { return false; }
    }


    [HarmonyPatch(typeof(ProgressTracker), "Update")]
    public class ProgressTrackerUpdateCancel
    {
        static bool Prefix() { return false; }
    }

}