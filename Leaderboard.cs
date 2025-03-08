#pragma warning disable IDE0051
#pragma warning disable IDE0037

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
using Il2CppMono.Net;

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {
        // Patches the settings menu to raise characterLimit (length verification is done at name submission)
        //[HarmonyPatch(typeof(SettingsMenu), "Start")]
        //public static class PatchDriverNameLength
        //{
        //    static void Prefix(SettingsMenu __instance)
        //    {
        //        TMP_InputField driverNameInput = __instance.driverNameInput;
        //        driverNameInput.textComponent.alignment = TextAlignmentOptions.Center;
        //        __instance.driverNameInput.characterLimit = 100;
        //    }
        //}

        // Patches the driver name field to allow longer names and exclude hex code colors from character limit
        //[HarmonyPatch(typeof(SettingsMenu), "submitDriverName")]
        //public static class PatchSubmitDriverName
        //{
        //    static bool Prefix(SettingsMenu __instance, ref string name)
        //    {
        //        if (name.Length > 0)
        //        {
        //            // Regex to match hex color codes of the format <#xxxxxx>
        //            string pattern = @"<#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?>";

        //            // Remove all occurrences of hex color codes
        //            string cleanedString = Regex.Replace(name, pattern, "");

        //            int characterCount = cleanedString.Length;

        //            GameObject driverNamePanel = __instance.driverNamePanel;
    

        //            if (characterCount > 25)
        //            {
        //                // Find the parent panel for the driver name input

        //                // Check if the text already exists
        //                Transform existingTextTransform = driverNamePanel.transform.Find("NameTooLongText");

        //                if (existingTextTransform != null)
        //                {
        //                    // Update the existing text if it is found
        //                    TextMeshProUGUI existingTextMesh = existingTextTransform.GetComponent<TextMeshProUGUI>();
        //                    if (existingTextMesh != null)
        //                    {
        //                        existingTextMesh.text = $"Driver name has a maximum of 25 characters, yours has {characterCount}."; // Update the text
        //                        return false;
        //                    }
        //                }


        //                // Create a new GameObject for the additional text
        //                GameObject additionalTextObject = new GameObject("NameTooLongText");

        //                // Add the TextMeshProUGUI component
        //                TextMeshProUGUI textMesh = additionalTextObject.AddComponent<TextMeshProUGUI>();

        //                // Set the font, alignment, and text
        //                textMesh.text = $"Driver name has a maximum of 25 characters, yours has {characterCount}";
        //                textMesh.font = __instance.driverNameInput.textComponent.font; // Use the same font as the input
        //                textMesh.fontSize = 24;
        //                textMesh.alignment = TextAlignmentOptions.Center;

        //                // Position the text under the input field
        //                RectTransform rectTransform = additionalTextObject.GetComponent<RectTransform>();
        //                rectTransform.SetParent(driverNamePanel.transform, false);
        //                rectTransform.anchoredPosition = new Vector2(0, -120); // Adjust position below the input field
        //                rectTransform.sizeDelta = new Vector2(500, 50); // Set the size

        //                // Optional: Add styling like color
        //                textMesh.color = Color.red;

        //                return false;
        //            }
        //            else
        //            {
        //                // Check if the text exists to destroy it
        //                Transform existingTextTransform = driverNamePanel.transform.Find("NameTooLongText");

        //                TextMeshProUGUI existingTextMesh = existingTextTransform.GetComponent<TextMeshProUGUI>();
        //                existingTextMesh.text = "";

        //                // Let OG method do its thing
        //                return true;
        //            }
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }
        //}


        // Patches UnityWebRequest to add the API key and change the request address
        [HarmonyPatch(typeof(UnityWebRequest), "SendWebRequest")]
        public static class PatchUnityWebRequest
        {
            static void Prefix(UnityWebRequest __instance)
            {
                __instance.SetRequestHeader(Constants.ApiKeyHeader, BuildAPIKey());

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
        
        public class RecordInformation
        {
            public string DriverName { get; set; }
            public float Timing { get; set; }
            public string DeviceId { get; set; }
            public int Track { get; set; }
            public int Direction { get; set; }
            public int Car { get; set; }
            public int TimeOfDay { get; set; }
            public float Sector1 { get; set; }
            public float Sector2 { get; set; }
            public float Sector3 { get; set; }
            public float Sector4 { get; set; }
            public string Date { get; set; }
            public string ModVersion { get; set; }
        }


        // Patch ghostSavedAndPrepped() with custom logic for uploading leaderboard record and replay
        [HarmonyPatch(typeof(EventManager), "ghostSavedAndPrepped")]
        public static class PatchGhostSavedAndPrepped
        {
            static bool Prefix(EventManager __instance)
            {

                // Check if the uploadGroup and uploadingText already exist
                if (Uploader.instance.uploadGroup == null || Uploader.instance.uploadingText == null)
                {

                    return false;
                }

                Uploader.instance.uploadingText.text = "Sending Record...";
                Uploader.instance.uploadGroup.alpha = 1f;


                var replayData = ReplayLoader.instance.getPreparedReplay();

                if (!__instance.carController.isTrollCar && AntiWallride.systemEnabled)
                {
                    
                    // Get the sector times
                    float[] sectortimes = TimeAttack.singleton.sectionTimings.ToArray();
                    float sector1 = sectortimes[0];
                    float sector2 = sectortimes[1] - sector1;
                    float sector3 = sectortimes[2] - (sector1 + sector2);
                    float sector4 = __instance.raceTimer - (sector1 + sector2 + sector3);
                    MelonLogger.Msg(sector1);
                    MelonLogger.Msg(sector2);
                    MelonLogger.Msg(sector3);
                    MelonLogger.Msg(sector4);

                    var recordInformation = new RecordInformation
                    {
                        DriverName = __instance.carController.driver,
                        Timing = __instance.raceTimer,
                        DeviceId = SystemInfo.deviceUniqueIdentifier,
                        Track = EventLoader.singleton.trackIndex,
                        Direction = __instance.reverse ? 1 : 0,
                        Car = __instance.carChoice,
                        TimeOfDay = Weather.singleton.night ? 1 : 0,
                        Sector1 = sector1,
                        Sector2 = sector2,
                        Sector3 = sector3,
                        Sector4 = sector4,
                        Date = DateTime.Now.ToString("M/d/yyyy"),
                        ModVersion = Constants.ModVersion
                    };

                    // Call API using custom task queue
                    CustomLeaderboardAndReplayMod.Enqueue(async () =>
                    {
                        try
                        {
                            await SubmitLeaderboardRecordAsync(recordInformation);
                            MelonCoroutines.Start(UpdateUI("New Record Sent!"));
                            //Uploader.instance.uploadingText.text = "New Record Sent!";
                            await Task.Delay(TimeSpan.FromSeconds(7));
                            Uploader.instance.uploadGroup.alpha = 0f;
                        }
                        catch (Exception ex)
                        {
                            Uploader.instance.uploadingText.text = "Error Uploading";
                            Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception occurred while submitting leaderboard record or uploading replay data: {ex.Message}");
                        }
                    });
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Msg("Done with sendNewRecord()");
                }
                return false;
            }
        }

        private static IEnumerator<string> UpdateUI(string message)
        {
            Uploader.instance.uploadingText.text = message;
            yield return null;  // Ensures execution on the next frame
        }


        private static async Task SubmitLeaderboardRecordAsync(RecordInformation recordInformation)
        {
            var s3RequestUrl = $"{Constants.NewGetS3UrlAddress}"; // Ensure this is the full API Gateway URL

            string replayId = Guid.NewGuid().ToString();
            MelonLogger.Msg($"Replay Id: {replayId}");

            var s3RequestBody = new
            {
                filename = replayId,  // Ensure filename is passed correctly
                drivername = recordInformation.DriverName,
                timing = recordInformation.Timing.ToString(),
                deviceid = recordInformation.DeviceId,
                track = recordInformation.Track.ToString(),
                direction = recordInformation.Direction.ToString(),
                car = recordInformation.Car.ToString(),
                timeofday = recordInformation.TimeOfDay.ToString(),
                sector1 = recordInformation.Sector1.ToString(),
                sector2 = recordInformation.Sector2.ToString(),
                sector3 = recordInformation.Sector3.ToString(),
                sector4 = recordInformation.Sector4.ToString(),
                date = recordInformation.Date,
                modversion = recordInformation.ModVersion
            };

            // Serialize request body
            string s3JsonBody = JsonSerializer.Serialize(s3RequestBody);
            var s3Content = new StringContent(s3JsonBody, Encoding.UTF8, "application/json");

            // Create the HttpRequestMessage object
            var s3Request = new HttpRequestMessage(HttpMethod.Post, s3RequestUrl)
            {
                Content = s3Content
            };

            // Add the API key **only for this request**
            s3Request.Headers.Add(Constants.ApiKeyHeader, BuildAPIKey());

            // Send the request
            HttpResponseMessage s3Response = await httpClient.SendAsync(s3Request);

            // Get the response body
            string s3Url = await s3Response.Content.ReadAsStringAsync();

            // Log error if the request failed
            if (!s3Response.IsSuccessStatusCode)
            {
                MelonLogger.Error($"Failed to get S3 URL: {s3Response.StatusCode}, Details: {s3Url}");
            }


            // Compress the replay JSON
            byte[] replayData = Encoding.UTF8.GetBytes(uploadReplayJson); // Convert JSON to bytes
            byte[] compressedReplayData = CompressReplay(replayData); // Compress the replay data

            var request = new HttpRequestMessage(HttpMethod.Put, s3Url)
            {
                Content = new ByteArrayContent(compressedReplayData)
            };

            request.Headers.Add(Constants.ApiKeyHeader, BuildAPIKey());
            request.Headers.Add($"x-amz-meta-drivername", recordInformation.DriverName);
            request.Headers.Add($"x-amz-meta-timing", recordInformation.Timing.ToString());
            request.Headers.Add($"x-amz-meta-deviceid", recordInformation.DeviceId);
            request.Headers.Add($"x-amz-meta-track", recordInformation.Track.ToString());
            request.Headers.Add($"x-amz-meta-direction", recordInformation.Direction.ToString());
            request.Headers.Add($"x-amz-meta-car", recordInformation.Car.ToString());
            request.Headers.Add($"x-amz-meta-timeofday", recordInformation.TimeOfDay.ToString());
            request.Headers.Add($"x-amz-meta-sector1", recordInformation.Sector1.ToString());
            request.Headers.Add($"x-amz-meta-sector2", recordInformation.Sector2.ToString());
            request.Headers.Add($"x-amz-meta-sector3", recordInformation.Sector3.ToString());
            request.Headers.Add($"x-amz-meta-sector4", recordInformation.Sector4.ToString());
            request.Headers.Add($"x-amz-meta-date", recordInformation.Date);
            request.Headers.Add($"x-amz-meta-modversion", recordInformation.ModVersion);

            // Send the PUT request to upload replay data
            var response = await httpClient.SendAsync(request);

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