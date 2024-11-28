#pragma warning disable IDE0051

using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using UnityEngine.Networking;
using static MelonLoader.MelonLogger;

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {
        private const string BASE_LEADERBOARD_API_ADDRESS = "https://o2hm4g1w50.execute-api.us-east-1.amazonaws.com/prod";
        private const string WRITE_RECORD_SUBADDRESS = "/writeRecord";


        // Patches UnityWebRequest to add the API key to untouched requests
        [HarmonyPatch(typeof(UnityWebRequest), "SendWebRequest")]
        public static class UnityWebRequestAPIKeyPatch
        {
            static void Prefix(UnityWebRequest __instance)
            {
                MelonLogger.Msg($"In prefix, url is {__instance.url}");
                __instance.SetRequestHeader("x-api-key", API_KEY);
                // Example: Replace specific URL with a custom one
                if (__instance.url.Equals("https://initialunity.online/postRecord"))
                {
                    __instance.url = __instance.url.Replace("https://initialunity.online/postRecord", "https://o2hm4g1w50.execute-api.us-east-1.amazonaws.com/prod/writeRecord");
                    MelonLogger.Msg($"Modified URL: {__instance.url}");
                }
                else if (__instance.url.StartsWith("https://initialunity.online/getRecords"))
                {
                    __instance.url = __instance.url.Replace("https://initialunity.online/getRecords", "https://o2hm4g1w50.execute-api.us-east-1.amazonaws.com/prod/getRecords");
                    MelonLogger.Msg($"Modified URL: {__instance.url}");
                }
                else if (__instance.url.StartsWith("https://initialunity.online/getGhost"))
                {
                    __instance.url = "https://dyyukzzk19.execute-api.us-east-1.amazonaws.com/prod/getGhost";
                    MelonLogger.Msg($"Modified URL: {__instance.url}");
                }
            }
        }


        // Patch ghostSavedAndPrepped() with custom logic for uploading leaderboard record and replay
        [HarmonyPatch(typeof(EventManager), "ghostSavedAndPrepped")]
        public static class EventManagerPatch
        {
            static bool Prefix(EventManager __instance)
            {
                var replayData = ReplayLoader.instance.getPreparedReplay();
                Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("IN PREFIX FOR EVENT MANAGER GHOSTSAVEDANDPREPPED");

                if (!__instance.carController.isTrollCar && AntiWallride.systemEnabled)
                {
                    // Create JSON payload to submit record
                    string jsonPayload = JsonSerializer.Serialize(new
                    {
                        driverName = __instance.carController.driver,
                        timing = __instance.raceTimer,
                        deviceId = SystemInfo.deviceUniqueIdentifier,
                        track = EventLoader.singleton.trackIndex,
                        direction = __instance.reverse ? 1 : 0,
                        car = __instance.carChoice,
                        timeOfDay = Weather.singleton.night ? 1 : 0,
                    });

                    // Call API using custom task queue
                    CustomLeaderboardAndReplayMod.Enqueue(async () =>
                    {
                        try
                        {
                            string s3Url = await SubmitLeaderboardRecordAsync(jsonPayload);
                            if (!string.IsNullOrEmpty(s3Url))
                            {
                                await UploadReplayDataAsync(s3Url, uploadReplayJson);
                            }
                            else
                            {
                                Melon<CustomLeaderboardAndReplayMod>.Logger.Error("Failed to retrieve S3 URL. Replay data upload skipped.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception occurred while submitting leaderboard record or uploading replay data: {ex.Message}");
                        }
                    });
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("Done with sendNewRecord()");
                }
                return false;
            }
        }


        // Submits leaderboard record and returns presigned S3 URL from HTTP response
        private static async Task<string> SubmitLeaderboardRecordAsync(string jsonPayload)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, BASE_LEADERBOARD_API_ADDRESS + WRITE_RECORD_SUBADDRESS);
                request.Headers.Add("x-api-key", API_KEY);

                // Set the request content
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Send the request
                var response = await httpClient.SendAsync(request);

                // Handle the response
                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Msg($"Success! Response Body: {responseBody}");

                    // Parse JSON to get the S3 URL
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(responseBody))
                        {
                            if (doc.RootElement.TryGetProperty("s3_url", out JsonElement s3UrlElement))
                            {
                                string s3Url = s3UrlElement.GetString();
                                Melon<CustomLeaderboardAndReplayMod>.Logger.Msg($"Presigned S3 URL: {s3Url}");
                                return s3Url;
                            }
                            else
                            {
                                Melon<CustomLeaderboardAndReplayMod>.Logger.Error("Response JSON does not contain 's3_url' field.");
                                return null;
                            }
                        }
                    }
                    catch (JsonException je)
                    {
                        Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"JSON Parsing Exception: {je.Message}");
                        return null;
                    }
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Msg($"Error: {response.StatusCode}, Details: {errorBody}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception during SubmitLeaderboardRecordAsync: {ex.Message}");
                return null;
            }
        }


        // Uploads replay data to the S3 URL
        public static async Task UploadReplayDataAsync(string s3Url, string replayJson)
        {
            try
            {
                // Create the content for the PUT request
                var content = new StringContent(replayJson, Encoding.UTF8, "application/json");

                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");


                // Send the PUT request to upload replay data
                var response = await httpClient.PutAsync(s3Url, content);

                if (response.IsSuccessStatusCode)
                {
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("Replay data successfully uploaded to S3.");
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Failed to upload replay data to S3: {response.StatusCode}, Details: {errorBody}");
                }
            }
            catch (Exception ex)
            {
                Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception during UploadReplayDataAsync: {ex.Message}");
            }
        }

    }
}