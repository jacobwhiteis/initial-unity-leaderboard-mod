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
using UnityEngine.Networking;

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod
    {
        private const string BASE_API_ADDRESS = "https://o2hm4g1w50.execute-api.us-east-1.amazonaws.com/prod";
        private const string WRITE_RECORD_SUBADDRESS = "/writeRecord";

        // Patches UnityWebRequest to add the API key to untouched requests
        [HarmonyPatch(typeof(UnityWebRequest), "SendWebRequest")]
        public static class UnityWebRequestPatch
        {
            static void Prefix(UnityWebRequest __instance)
            {
                __instance.SetRequestHeader("x-api-key", API_KEY);
            }
        }


        // Patch the address the game sends requests to
        [HarmonyPatch(typeof(Utils), "getServerAddress")]
        public static class PatchServerAddress
        {
            static bool Prefix(ref string __result)
            {
                __result = BASE_API_ADDRESS;
                return false;
            }
        }


        // Patch the subaddress for submission requests in Uploader
        [HarmonyPatch(typeof(Uploader), "Start")]
        public static class UploaderPatch
        {
            static void Prefix(Uploader __instance)
            {
                __instance.subAddress = "/writeRecord";
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
                        driver_name = __instance.carController.driver,
                        timing = __instance.raceTimer,
                        hardwareId = SystemInfo.deviceUniqueIdentifier,
                        track = EventLoader.singleton.trackIndex,
                        car = __instance.carChoice,
                        layout = __instance.reverse ? 1 : 0,
                        condition = Weather.singleton.night ? 1 : 0,
                    });

                    // Call API using custom task queue
                    CustomLeaderboardAndReplayMod.Enqueue(async () =>
                    {
                        try
                        {
                            string s3Url = await SubmitLeaderboardRecordAsync(jsonPayload);
                            if (!string.IsNullOrEmpty(s3Url))
                            {
                                await UploadReplayDataAsync(s3Url, replayData);
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
                var request = new HttpRequestMessage(HttpMethod.Post, BASE_API_ADDRESS + WRITE_RECORD_SUBADDRESS);
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
        public static async Task UploadReplayDataAsync(string s3Url, byte[] replayData)
        {
            try
            {
                // Create the content for the PUT request

                // FAKE BYTE DATA FOR NOW
                var content = new ByteArrayContent(new byte[0]);

                //var content = new ByteArrayContent(replayData);


                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream"); // Must match the presigned URL's Content-Type

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


        // Patch class for testing on game startup
        [HarmonyPatch(typeof(MainMenu), "Start")]
        public static class GameStartupPatch
        {
            // Postfix method to run after InitializeGame, used for testing
            static void Postfix()
            {
                //MelonLogger.Msg("Trying to test s3 upload");
                //// Start the test upload asynchronously

                //CustomLeaderboardAndReplayMod.Enqueue(async () =>
                //{
                //    try
                //    {
                //        string result = await TestUploadAsync();
                //        MelonLogger.Msg($"Result is: {result}");
                //    }
                //    catch (Exception ex)
                //    {
                //        Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception while submit leaderboard record or upload replay data: {ex.Message}");
                //    }
                //});

                //Task.Run(async () => await TestUploadAsync());
                //MelonLogger.Msg("Done testing");
            }


            // Async method to perform the test upload
            //private static async Task<string> TestUploadAsync()
            //{
            //    MelonLogger.Msg("in the async");

            //    // Replace with your actual presigned S3 URL
            //    string presignedUrl = "https://initial-unity-leaderboard-replays.s3.amazonaws.com/6d94c894-097b-4714-8b16-bcc8348e66fb.iureplay?AWSAccessKeyId=ASIAUMMESYJBANUV2IG5&Signature=sa7E3RTrC9hUP4F0tdJ9zqQ1Dtg%3D&x-amz-security-token=IQoJb3JpZ2luX2VjEAMaCXVzLWVhc3QtMSJGMEQCIFtG%2FzGqm%2F%2BE13AnbUvR7BDt%2FN7L6lwPuFfY0b8or1bKAiAiUwpiJ8db005%2F4EndkKDdsGRyZymNSiddwP%2FniopCdyqHAwic%2F%2F%2F%2F%2F%2F%2F%2F%2F%2F8BEAAaDDMwMTQ2MjYzNTA3NCIMncdCACR5WzQDGlvlKtsCZIgubZJpESoSd%2FmRbnO1%2F%2B3Iy29rXjgqehcdTggeJl7QpFw6xUQ5Mac7DpZuAj3AbWh3QZqRP%2F76JUG7cR8vGytcKz1XJifYGANzz9C0V8fbApLrRxyk19B3ldy0Ow4UGBdNLKkqjloLSzgrlhSjsXO2%2F2rym5DvQY%2FYN9sRS3m%2FwA5ptwZHl9yJjXT4yLXLF%2FJ9u1pHnXNi4gA%2BexGuXDcjCs5hjT2%2Bddc%2FhxnpZlJqDyhV6Asg1C6SsW77tsxMVsKNwXe9KX%2B8fUZ1Zf1KvrDC0KwRA4F1gLdqqXN70hwVIjsvVZX2lyvx4Nd0htg6PoJjEdhyVajRSet3xikcMHj2qAt3pnhjuZVEIjTYqh1xEuaaucBy0R0yE6N%2BzvrwJaaeg6sR47uL2gHe1WU8WPQq8VoIuZHG5uxzstgTC1UZR0BjzJNoO2bPvSBeh2HaehVwDvEhHK61DGUwpLz6uQY6nwHWzQRjQ9iM4x4kLYLsJGytCDwu%2FV%2FCPt5GD%2FI6KBo%2FxS0giOCwtnnipKNNVBMZOnIKQ9OBjQH6wqH1QJu2xXfnRszLuDM60quoNcpC4QdOMjpXl7rqEkVeCOxS6k2de4OT%2FrU%2FHizMBbPMoN%2B%2Fw1MRYKiIR7OUkDTI9lasEYDHucQsDUL7GQRChYnx5TGUTOMZ2W%2Fjar6SiRSVzJmqFsk%3D&Expires=1732160921";

            //    MelonLogger.Msg($"presigned url is {presignedUrl}");

            //    // Create some test data (e.g., a simple string converted to bytes)
            //    byte[] testData = Encoding.UTF8.GetBytes("This is a test replay data.");

            //    // Create the content with the correct Content-Type
            //    var content = new ByteArrayContent(testData);
            //    //content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            //    //httpClient.DefaultRequestHeaders.Add("content-type", "application/octet-stream");

            //    try
            //    {
            //        // Perform the PUT request to upload the test data
            //        var response = await httpClient.PutAsync(presignedUrl, content);
            //        MelonLogger.Msg("After the PutAsync call");
            //        //if (response.IsSuccessStatusCode)
            //        //{
            //        //    MelonLogger.Msg("Test upload to S3 succeeded.");
            //        //}
            //        //else
            //        //{
            //        //    string responseBody = await response.Content.ReadAsStringAsync();
            //        //    MelonLogger.Error($"Test upload to S3 failed: {response.StatusCode}, Details: {responseBody}");
            //        //}
            //    }
            //    catch (Exception ex)
            //    {
            //        MelonLogger.Error($"Exception during test upload to S3: {ex.Message}");
            //    }

            //    return "TestUploadAsync return value";
            //}
        }
    }
}