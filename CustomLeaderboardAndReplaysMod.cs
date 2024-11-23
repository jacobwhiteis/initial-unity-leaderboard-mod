using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using ModNamespace;

// Top of Akagi UH: -281.6374 172.5851 74.3028

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {

        private static readonly HttpClient httpClient = new();
        private static readonly Queue<Action> _executionQueue = new();
        private static readonly object _queueLock = new();

        private const string API_KEY = "yNrGPe5fnx1sMuNGXRV4o7JUyTonjAoH2G1ky6X3";


        public override void OnInitializeMelon()
        {
            Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("CustomLeaderboardAndReplayMod initialized.");

            // Prevent IL2CPP stripping by referencing the types
            var preventStripping1 = typeof(NetworkReplay);
            var preventStripping2 = typeof(ReplayHeader);
            var preventStripping3 = typeof(ReplayData);
            var preventStripping4 = typeof(CompressedData);
            var preventStripping5 = typeof(Car);
            var preventStripping6 = typeof(ReplaySystem.TimeSlice);
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



    // Harmony Patch for UnityWebRequest.SendWebRequest (Removed as per user request)
    // Since we're exclusively using HttpClient, this patch is no longer necessary.
    // If you have other usages of UnityWebRequest, consider removing or refactoring them accordingly.

    // Define the LeaderboardRecord class
    public class LeaderboardRecord
    {
        public string driver_name { get; set; }
        public float timing { get; set; }
        public string id { get; set; } // Device Hardware ID
        public int track { get; set; }
        public int car { get; set; } // Added property for Car Choice
        public int layout { get; set; }
        public int condition { get; set; }
    }

    // Serializable Classes
    [Serializable]
    public class ReplayHeader
    {
        public int version;
        public DateTime date;
        public bool isBattle;
        public float replayDuration;
        public string track = "";
        public bool altLayout;
        public bool night;
        public List<Car> cars;

        public ReplayHeader(int version, DateTime date, bool isBattle, float duration, string track, bool altLayout, bool night)
        {
            this.version = version;
            this.date = date;
            this.isBattle = isBattle;
            replayDuration = duration;
            this.track = track;
            this.altLayout = altLayout;
            this.night = night;
            cars = new List<Car>();
        }
    }

    [Serializable]
    public class ReplayData
    {
        public List<List<ReplaySystem.TimeSlice>> timeslices;

        public ReplayData()
        {
            timeslices = new List<List<ReplaySystem.TimeSlice>>();
        }
    }

    [Serializable]
    public class CompressedData
    {
        public byte[] compressed;

        public CompressedData(object toCompress)
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                binaryFormatter.Serialize(memoryStream, toCompress);
                byte[] input = memoryStream.ToArray();
                compressed = CompressionHelper.CompressBytes(input);
            }
        }

        public object getDecompressedObject()
        {
            if (compressed == null)
            {
                return null;
            }
            byte[] array = CompressionHelper.DecompressBytes(compressed);
            using (MemoryStream memoryStream = new MemoryStream(array))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                memoryStream.Seek(0L, SeekOrigin.Begin);
                return binaryFormatter.Deserialize(memoryStream);
            }
        }
    }

    [Serializable]
    public class Car
    {
        public string model;
        public string driver;

        public Car(string model, string driver)
        {
            this.model = model;
            this.driver = driver;
        }
    }

    [Serializable]
    public struct NetworkReplay
    {
        public ReplayHeader header;
        public CompressedData compressedData;
    }

    // CompressionHelper Class
    public static class CompressionHelper
    {
        // Compresses a byte array using DeflateStream
        public static byte[] CompressBytes(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var compressStream = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal))
                {
                    compressStream.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }

        // Decompresses a byte array using DeflateStream
        public static byte[] DecompressBytes(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var decompressStream = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                decompressStream.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}