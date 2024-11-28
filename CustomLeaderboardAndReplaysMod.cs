
#pragma warning disable IDE0051

using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using System.Text;
using System.Reflection;

// Top of Akagi UH: -281.6374 172.5851 74.3028
// Top of Usui UH: 635.8738 192.6996 742.4423

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {

        private static readonly HttpClient httpClient = new();
        private static readonly Queue<Action> _executionQueue = new();
        private static readonly object _queueLock = new();

        private const string API_KEY = "yNrGPe5fnx1sMuNGXRV4o7JUyTonjAoH2G1ky6X3";

        public override void OnApplicationStart()
        {
            //MakeMethodPublic();
            // Any other initialization logic.
        }



        //public static void MakeMethodPublic()
        //{
        //    var type = typeof(ReplayLoader);
        //    var method = type.GetMethod("loadOnlineReplay", BindingFlags.NonPublic | BindingFlags.Instance);



        //    if (method != null)
        //    {
        //        // Change the access modifier to public
        //        MethodInfo methodToModify = method;
        //        FieldInfo attributes = typeof(MethodInfo).GetField("m_methodAttributes", BindingFlags.NonPublic | BindingFlags.Instance);
        //        if (attributes != null)
        //        {
        //            // Clear the private flag and add the public flag
        //            attributes.SetValue(methodToModify, (MethodAttributes)((MethodAttributes)attributes.GetValue(methodToModify) & ~MethodAttributes.Private) | MethodAttributes.Public);
        //        }
        //    }
        //    else
        //    {
        //        MelonLogger.Msg("Method loadOnlineReplay not found via reflection.");
        //    }

        //    var methods = typeof(ReplayLoader).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        //    foreach (var m in methods)
        //    {
        //        MelonLogger.Msg($"Method found: {m.Name}, IsPrivate: {m.IsPrivate}, IsPublic: {m.IsPublic}");
        //    }
        //}


        public override void OnInitializeMelon()
        {
            Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("CustomLeaderboardAndReplayMod initialized.");
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




    // Patching the ProgressTracker to prevent unwanted behavior
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