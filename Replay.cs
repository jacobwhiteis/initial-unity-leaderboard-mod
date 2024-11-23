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

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod
    {

        // LOADING REPLAY STUFF //////////////////////////////////////////////////////////////////

        static async Task<byte[]> SendHttpRequestForReplayAsync(string url)
        {
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    if (response.IsSuccessStatusCode)
                    {
                        // Read the response content as a byte array
                        byte[] replayData = await response.Content.ReadAsByteArrayAsync();

                        MelonLogger.Msg($"Received replay data length: {replayData.Length}");

                        return replayData;
                    }
                    else
                    {
                        MelonLogger.Error($"HTTP Error: {response.StatusCode}");
                        return null;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                MelonLogger.Error($"HTTP Request Exception: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Unexpected exception in SendHttpRequestForReplayAsync: {ex.Message}");
                return null;
            }
        }

        // Patch for viewOnlineReplay in LeaderboardManager
        [HarmonyPatch(typeof(LeaderboardManager), "viewOnlineReplay")]
        public static class PatchViewOnlineReplay
        {
            static bool Prefix(LeaderboardManager __instance, string recordID, int track, bool altLayout, bool night, float timing)
            {
                MelonLogger.Msg("viewOnlineReplay Prefix called.");
                MelonLogger.Msg($"recordID: {recordID}, track: {track}, altLayout: {altLayout}, night: {night}, timing: {timing}");

                if (!__instance.timeAttackMenu.animating)
                {
                    __instance.timeAttackMenu.animating = true;
                    AudioManager.singleton.playConfirm();
                    ReplayLoader.replayToLoad = recordID;
                    ReplayLoader.isReplay = true;
                    ReplayLoader.isOnlineReplay = true;
                    ReplayLoader.replayTiming = timing;
                    EventLoader.isMultiplayer = false;
                    EventLoader.night = night;
                    EventLoader.reverseLayout = altLayout;

                    // Fade to black and load the track scene
                    Blocker.fadeToBlack();
                    // Assuming fadeToBlack is synchronous; if not, ensure that the scene loads after the fade
                    var tracks = SelectionManager.singleton.tracks;
                    AssetLoader.LoadScene("tracks/" + tracks[track].name, tracks[track].crc);
                }

                return false; // Skip the original method
            }
        }

        // Patch for ReplayLoader's Start method
        [HarmonyPatch(typeof(ReplayLoader), "Start")]
        public static class PatchReplayLoaderStart
        {
            static void Postfix(ReplayLoader __instance)
            {
                MelonLogger.Msg("ReplayLoader Start method called.");

                if (ReplayLoader.isOnlineReplay)
                {
                    MelonLogger.Msg("Custom replay loading initiated.");

                    // Proceed to fetch and load the replay data from your API
                    FetchAndLoadReplayData(__instance, ReplayLoader.replayToLoad);
                }
            }

            static async void FetchAndLoadReplayData(ReplayLoader replayLoader, string recordID)
            {
                // Build your API URL
                string apiKey = "YOUR_API_KEY"; // Replace with your API key
                string url = "https://initialunity.online/getGhost/?id=1936733";

                byte[] replayData = null;
                try
                {
                    replayData = await SendHttpRequestForReplayAsync(url);

                    if (replayData == null)
                    {
                        MelonLogger.Error("Failed to retrieve replay data: responseData is null.");
                        Enqueue(() => EventManager.singleton.goToMainMenu());
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Exception during Replay retrieval: {ex.Message}");
                    Enqueue(() => EventManager.singleton.goToMainMenu());
                    return;
                }

                // Process the replay data
                ProcessReplayData(replayLoader, replayData);

                // Invoke readingComplete on the main thread
                Enqueue(() =>
                {
                    replayLoader.readingComplete?.Invoke();
                });
            }

            static async Task<byte[]> SendHttpRequestForReplayAsync(string url)
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        // Send the HTTP request
                        HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                        if (response.IsSuccessStatusCode)
                        {
                            // Read the response content as a byte array
                            byte[] data = await response.Content.ReadAsByteArrayAsync();
                            return data;
                        }
                        else
                        {
                            MelonLogger.Error($"HTTP Error: {response.StatusCode}");
                            return null;
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    MelonLogger.Error($"HTTP Request Exception: {ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Unexpected exception in SendHttpRequestForReplayAsync: {ex.Message}");
                    return null;
                }
            }

            static void ProcessReplayData(ReplayLoader instance, byte[] replayData)
            {
                try
                {
                    using (MemoryStream memoryStream = new MemoryStream(replayData))
                    {
                        BinaryFormatter binaryFormatter = new BinaryFormatter();

                        // Deserialize the NetworkReplay object
                        var networkReplay = (ReplayLoader.NetworkReplay)binaryFormatter.Deserialize(memoryStream);

                        // Decompress the data
                        var readHeader = networkReplay.header;
                        var readData = (ReplayLoader.ReplayData)networkReplay.compressedData.getDecompressedObject();

                        // Set the fields via reflection
                        var readHeaderField = AccessTools.Field(typeof(ReplayLoader), "readHeader");
                        var readDataField = AccessTools.Field(typeof(ReplayLoader), "readData");
                        var readingField = AccessTools.Field(typeof(ReplayLoader), "reading");

                        readHeaderField.SetValue(instance, readHeader);
                        readDataField.SetValue(instance, readData);
                        readingField.SetValue(instance, false);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Exception during replay data processing: {ex.Message}");
                    Enqueue(() => EventManager.singleton.goToMainMenu());
                }
            }
        }
    }










    //// Harmony Patch for UnityWebRequest.SendWebRequest (Removed as per user request)
    //// Since we're exclusively using HttpClient, this patch is no longer necessary.
    //// If you have other usages of UnityWebRequest, consider removing or refactoring them accordingly.

    //// Define the LeaderboardRecord class
    //public class LeaderboardRecord
    //{
    //    public string driver_name { get; set; }
    //    public float timing { get; set; }
    //    public string id { get; set; } // Device Hardware ID
    //    public int track { get; set; }
    //    public int car { get; set; } // Added property for Car Choice
    //    public int layout { get; set; }
    //    public int condition { get; set; }
    //}

    //// Serializable Classes
    //[Serializable]
    //public class ReplayHeader
    //{
    //    public int version;
    //    public DateTime date;
    //    public bool isBattle;
    //    public float replayDuration;
    //    public string track = "";
    //    public bool altLayout;
    //    public bool night;
    //    public List<Car> cars;

    //    public ReplayHeader(int version, DateTime date, bool isBattle, float duration, string track, bool altLayout, bool night)
    //    {
    //        this.version = version;
    //        this.date = date;
    //        this.isBattle = isBattle;
    //        replayDuration = duration;
    //        this.track = track;
    //        this.altLayout = altLayout;
    //        this.night = night;
    //        cars = new List<Car>();
    //    }
    //}

    //[Serializable]
    //public class ReplayData
    //{
    //    public List<List<ReplaySystem.TimeSlice>> timeslices;

    //    public ReplayData()
    //    {
    //        timeslices = new List<List<ReplaySystem.TimeSlice>>();
    //    }
    //}

    //[Serializable]
    //public class CompressedData
    //{
    //    public byte[] compressed;

    //    public CompressedData(object toCompress)
    //    {
    //        BinaryFormatter binaryFormatter = new BinaryFormatter();
    //        using (MemoryStream memoryStream = new MemoryStream())
    //        {
    //            binaryFormatter.Serialize(memoryStream, toCompress);
    //            byte[] input = memoryStream.ToArray();
    //            compressed = CompressionHelper.CompressBytes(input);
    //        }
    //    }

    //    public object getDecompressedObject()
    //    {
    //        if (compressed == null)
    //        {
    //            return null;
    //        }
    //        byte[] array = CompressionHelper.DecompressBytes(compressed);
    //        using (MemoryStream memoryStream = new MemoryStream(array))
    //        {
    //            BinaryFormatter binaryFormatter = new BinaryFormatter();
    //            memoryStream.Seek(0L, SeekOrigin.Begin);
    //            return binaryFormatter.Deserialize(memoryStream);
    //        }
    //    }
    //}

    //[Serializable]
    //public class Car
    //{
    //    public string model;
    //    public string driver;

    //    public Car(string model, string driver)
    //    {
    //        this.model = model;
    //        this.driver = driver;
    //    }
    //}

    //[Serializable]
    //public struct NetworkReplay
    //{
    //    public ReplayHeader header;
    //    public CompressedData compressedData;
    //}

    //// CompressionHelper Class
    //public static class CompressionHelper
    //{
    //    // Compresses a byte array using DeflateStream
    //    public static byte[] CompressBytes(byte[] data)
    //    {
    //        using (var output = new MemoryStream())
    //        {
    //            using (var compressStream = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal))
    //            {
    //                compressStream.Write(data, 0, data.Length);
    //            }
    //            return output.ToArray();
    //        }
    //    }

    //    // Decompresses a byte array using DeflateStream
    //    public static byte[] DecompressBytes(byte[] data)
    //    {
    //        using (var input = new MemoryStream(data))
    //        using (var decompressStream = new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress))
    //        using (var output = new MemoryStream())
    //        {
    //            decompressStream.CopyTo(output);
    //            return output.ToArray();
    //        }
    //    }
    //}
}