#pragma warning disable IDE0051

using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using System.Text;
using System.Text.Json;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using Il2CppTMPro;
using Il2CppValve.VR.InteractionSystem;
using System.IO;
using System.IO.Compression;

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {
        // Patches the settings menu to raise characterLimit (length verification is done at name submission)
        [HarmonyPatch(typeof(SettingsMenu), "Start")]
        public static class PatchDriverNameLength
        {
            static void Prefix(SettingsMenu __instance)
            {
                TMP_InputField driverNameInput = __instance.driverNameInput;
                driverNameInput.textComponent.alignment = TextAlignmentOptions.Center;
                __instance.driverNameInput.characterLimit = 100;
            }
        }

        // Patches the driver name field to allow longer names and exclude hex code colors from character limit
        [HarmonyPatch(typeof(SettingsMenu), "submitDriverName")]
        public static class PatchSubmitDriverName
        {
            static bool Prefix(SettingsMenu __instance, ref string name)
            {
                if (name.Length > 0)
                {
                    // Regex to match hex color codes of the format <#xxxxxx>
                    string pattern = @"<#[0-9a-fA-F]{6}>";

                    // Remove all occurrences of hex color codes
                    string cleanedString = Regex.Replace(name, pattern, "");

                    int characterCount = cleanedString.Length;

                    GameObject driverNamePanel = __instance.driverNamePanel;
    

                    if (characterCount > 25)
                    {
                        // Find the parent panel for the driver name input

                        // Check if the text already exists
                        Transform existingTextTransform = driverNamePanel.transform.Find("NameTooLongText");

                        if (existingTextTransform != null)
                        {
                            // Update the existing text if it is found
                            TextMeshProUGUI existingTextMesh = existingTextTransform.GetComponent<TextMeshProUGUI>();
                            if (existingTextMesh != null)
                            {
                                existingTextMesh.text = $"Driver name has a maximum of 25 characters, yours has {characterCount}."; // Update the text
                                return false;
                            }
                        }


                        // Create a new GameObject for the additional text
                        GameObject additionalTextObject = new GameObject("NameTooLongText");

                        // Add the TextMeshProUGUI component
                        TextMeshProUGUI textMesh = additionalTextObject.AddComponent<TextMeshProUGUI>();

                        // Set the font, alignment, and text
                        textMesh.text = $"Driver name has a maximum of 25 characters, yours has {characterCount}";
                        textMesh.font = __instance.driverNameInput.textComponent.font; // Use the same font as the input
                        textMesh.fontSize = 24;
                        textMesh.alignment = TextAlignmentOptions.Center;

                        // Position the text under the input field
                        RectTransform rectTransform = additionalTextObject.GetComponent<RectTransform>();
                        rectTransform.SetParent(driverNamePanel.transform, false);
                        rectTransform.anchoredPosition = new Vector2(0, -120); // Adjust position below the input field
                        rectTransform.sizeDelta = new Vector2(500, 50); // Set the size

                        // Optional: Add styling like color
                        textMesh.color = Color.red;

                        return false;
                    }
                    else
                    {
                        MelonLogger.Msg("here");
                        // Check if the text exists to destroy it
                        Transform existingTextTransform = driverNamePanel.transform.Find("NameTooLongText");

                        TextMeshProUGUI existingTextMesh = existingTextTransform.GetComponent<TextMeshProUGUI>();
                        existingTextMesh.text = "";

                        // Let OG method do its thing
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
        }


        // Patches UnityWebRequest to add the API key and change the request address
        [HarmonyPatch(typeof(UnityWebRequest), "SendWebRequest")]
        public static class PatchUnityWebRequest
        {
            static void Prefix(UnityWebRequest __instance)
            {
                __instance.SetRequestHeader(Constants.ApiKeyHeader, Constants.ApiKey);

                // Replace original URL with new one
                if (__instance.url.Equals(Constants.OldWriteRecordAddress))
                {
                    __instance.url = __instance.url.Replace(Constants.OldWriteRecordAddress, Constants.NewWriteRecordAddress);
                }
                else if (__instance.url.StartsWith(Constants.OldGetRecordsAddress))
                {
                    __instance.url = __instance.url.Replace(Constants.OldGetRecordsAddress, Constants.NewGetRecordsAddress);
                }
            }
        }


        // Patch ghostSavedAndPrepped() with custom logic for uploading leaderboard record and replay
        [HarmonyPatch(typeof(EventManager), "ghostSavedAndPrepped")]
        public static class PatchGhostSavedAndPrepped
        {
            static bool Prefix(EventManager __instance)
            {
                MelonLogger.Msg("in ghostSavedAndPrepped prefix");

                // Check if the uploadGroup and uploadingText already exist
                if (Uploader.instance.uploadGroup == null || Uploader.instance.uploadingText == null)
                {
                    MelonLogger.Error("Uploader UI components are missing.");
                    return false;
                }

                // Set the text to "Sending Record..."
                Uploader.instance.uploadingText.text = "Sending Record...";
                Uploader.instance.uploadGroup.alpha = 1f;



                MelonLogger.Msg("out of uploader stuff");
                var replayData = ReplayLoader.instance.getPreparedReplay();

                if (!__instance.carController.isTrollCar && AntiWallride.systemEnabled)
                {
                    // In driver name, replace any hashes (for hex code colors) with __HASH__
                    // So as not to screw up DynamoDB composite indexes
                    string cleanedDriverName = __instance.carController.driver.Replace("#", "__HASH__");

                    // Create JSON payload to submit record
                    string jsonPayload = JsonSerializer.Serialize(new
                    {
                        driverName = cleanedDriverName,
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
                                Uploader.instance.uploadingText.text = "New Record Sent!";
                                await Task.Delay(TimeSpan.FromSeconds(7));
                                Uploader.instance.uploadGroup.alpha = 0f;
                            }
                            else
                            {

                                Uploader.instance.uploadingText.text = "Server Unavailable";
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
                var request = new HttpRequestMessage(HttpMethod.Post, Constants.NewWriteRecordAddress);
                request.Headers.Add(Constants.ApiKeyHeader, Constants.ApiKey);

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
                // Compress the replay JSON
                byte[] replayData = Encoding.UTF8.GetBytes(replayJson); // Convert JSON to bytes
                byte[] compressedReplayData = CompressReplay(replayData); // Compress the replay data

                // Create the content for the PUT request
                var content = new ByteArrayContent(compressedReplayData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-gzip"); // Set content type to GZip

                // Send the PUT request to upload replay data
                var response = await httpClient.PutAsync(s3Url, content);

                if (response.IsSuccessStatusCode)
                {
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("Replay data successfully uploaded.");
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Failed to upload replay data: {response.StatusCode}, Details: {errorBody}");
                }
            }
            catch (Exception ex)
            {
                Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception during UploadReplayDataAsync: {ex.Message}");
            }
        }


        public static byte[] CompressReplay(byte[] replayData)
        {
            using (var compressedStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(compressedStream, System.IO.Compression.CompressionLevel.Optimal))
                {
                    gzipStream.Write(replayData, 0, replayData.Length);
                }
                return compressedStream.ToArray();
            }
        }

    }
}