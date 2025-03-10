﻿#pragma warning disable IDE0051

using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using MelonLoader;
using Il2Cpp;
using static Il2Cpp.ReplayLoader;
using static ModNamespace.ReplayConversionUtils;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using System.Text.Json;
using System.Diagnostics.Tracing;
using static MelonLoader.MelonLogger;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Il2CppTelepathy;
namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {
        // Submits leaderboard record and returns presigned S3 URL from HTTP response
        private static async Task<string> getNextLeaderboardRecord(int track, int layout, int car, float bestTime)
        {
            try
            {
                string requestParams = "/?";
                requestParams = requestParams + "&track=" + track 
                                              + "&layout=" + layout
                                              + "&bestTime=" + bestTime.ToString();
                if (car != -1)
                {
                    requestParams = requestParams + "&car=" + car;
                }

                string requestUrl = Constants.NewGetRecordsAddress + requestParams;

                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Add(Constants.ApiKeyHeader, BuildAPIKey());

                // Send the request
                var response = await httpClient.SendAsync(request);

                // Get status code
                int statusCode = (int)response.StatusCode;

                // Handle the response
                if (statusCode == 200)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();

                    // Parse JSON to get the record Id
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(responseBody))
                        {
                            if (doc.RootElement.TryGetProperty("id", out JsonElement recordId))
                            {
                                return recordId.GetString();
                            }
                            else
                            {
                                Melon<CustomLeaderboardAndReplayMod>.Logger.Error("Response JSON does not contain 'id' field.");
                                return null;
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException je)
                    {
                        Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"JSON Parsing Exception: {je.Message}");
                        return null;
                    }
                }
                else if (statusCode == 204)
                {
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Error("No faster leaderboard record found");
                    return null;
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


        //[HarmonyPatch(typeof(GhostManager), "getOnlineGhost")]
        //public class PatchGhostManager1
        //{
        //    public static bool Prefix(GhostManager __instance, bool sameCar)
        //    {

        //        int requestTrack = EventLoader.singleton.trackIndex;
        //        int requestLayout = EventLoader.reverseLayout ? 1 : 0;
        //        int requestCar = sameCar ? EventLoader.carChoice : -1;
        //        float requestBestTime = EventManager.singleton.lastBestTime > 0 ? EventManager.singleton.lastBestTime : 600f;
        //        string recordId = null;

        //        try
        //        {
        //            // Blocking the async call here to fetch recordId
        //            recordId = Task.Run(async () =>
        //                await getNextLeaderboardRecord(requestTrack, requestLayout, requestCar, requestBestTime)
        //            ).Result;
        //        }
        //        catch (Exception ex)
        //        {
        //            Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception occurred while fetching recordId: {ex.Message}");
        //            return false; // Exit the method if an error occurs
        //        }

        //        // Check if recordId is null and exit early
        //        if (recordId == null)
        //        {
        //            MelonLogger.Msg("recordId is null. Exiting Prefix method.");
        //            return false;
        //        }


        //        ReplayLoader.replayToLoad = recordId;

        //        CustomLeaderboardAndReplayMod.Enqueue(async () =>
        //        {
        //            try
        //            {
        //                await PatchInitReplayMode.DownloadOnlineReplayJson(ReplayLoader.instance, false);
        //                CustomLeaderboardAndReplayMod.Enqueue(async () =>
        //                {
        //                    __instance.insertGhost(ReplayLoader.instance.getLoadedReplay(), true);
        //                });

        //            }
        //            catch (Exception ex)
        //            {
        //                Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception occurred while downloading replay data: {ex.Message}");
        //            }
        //        });
        //        return false;

        //    }
        //}


        // chat gpt aaaah patch code
        [HarmonyPatch(typeof(GhostManager), "getOnlineGhost")]
        public class PatchGhostManager1
        {
            public static bool Prefix(GhostManager __instance, bool sameCar)
            {
                // Start a coroutine instead of the original method logic
                MelonCoroutines.Start(GetOnlineGhostCoroutine(__instance, sameCar));

                MelonLogger.Msg("Made it here xd");

                // Return false to skip the original 'getOnlineGhost' method
                return false;
            }

            private static IEnumerator<string> GetOnlineGhostCoroutine(GhostManager ghostManager, bool sameCar)
            {
                int requestTrack = EventLoader.singleton.trackIndex;
                int requestLayout = EventLoader.reverseLayout ? 1 : 0;
                int requestCar = sameCar ? EventLoader.carChoice : -1;
                float requestBestTime = (EventManager.singleton.lastBestTime > 0)
                    ? EventManager.singleton.lastBestTime
                    : 600f;

                MelonLogger.Msg("here 1");
                // 1) Fetch `recordId` via async method
                Task<string> recordTask = null;
                try
                {
                    recordTask = getNextLeaderboardRecord(requestTrack, requestLayout, requestCar, requestBestTime);
                }
                catch (Exception ex)
                {
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception creating recordTask: {ex.Message}");
                    yield break;  // Stop the coroutine
                }

                MelonLogger.Msg("here 2");
                // 2) Wait until recordTask finishes
                while (recordTask != null && !recordTask.IsCompleted)
                    yield return null;

                if (recordTask.IsFaulted)
                {
                    // Log the exception and stop
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Error(
                        $"Exception while fetching recordId: {recordTask.Exception?.GetBaseException().Message}"
                    );
                    yield break;
                }

                string recordId = recordTask.Result;
                if (recordId == null)
                {
                    MelonLogger.Msg("recordId is null. Exiting method.");
                    yield break;
                }

                MelonLogger.Msg("here 3");
                // 3) Set up replay to load
                ReplayLoader.replayToLoad = recordId;

                // 4) Download the replay file
                Task downloadTask = null;
                try
                {
                    downloadTask = PatchInitReplayMode.DownloadOnlineReplayJson(ReplayLoader.instance, false);
                }
                catch (Exception ex)
                {
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Error($"Exception creating downloadTask: {ex.Message}");
                    yield break;
                }

                // 5) Wait until the replay download finishes
                while (downloadTask != null && !downloadTask.IsCompleted)
                    yield return null;

                if (downloadTask.IsFaulted)
                {
                    Melon<CustomLeaderboardAndReplayMod>.Logger.Error(
                        $"Exception occurred while downloading replay data: {downloadTask.Exception?.GetBaseException().Message}"
                    );
                    yield break;
                }
                MelonLogger.Msg("here 4");

                Replay ghostReplay = ReplayLoader.instance.getLoadedReplay();
                if (ghostReplay == null)
                {
                    MelonLogger.Msg("Downloaded replay was null! Check for networking / DNS issues. Skipping insertGhost.");
                    yield break;
                }
                if (ghostReplay.header == null)
                {
                    MelonLogger.Error("Downloaded replay header is null! Skipping insertGhost.");
                    yield break;
                }

                MelonLogger.Msg("SHOULD NOT GET HERE!");

                if (ghostReplay.header.cars == null || ghostReplay.header.cars.Count == 0)
                {
                    MelonLogger.Error("No cars in the downloaded replay header! Skipping insertGhost.");
                    yield break;
                }

                // Potentially also verify each entry in ghostReplay.header.cars...
                foreach (var carEntry in ghostReplay.header.cars)
                {
                    if (carEntry == null || string.IsNullOrEmpty(carEntry.model))
                    {
                        MelonLogger.Error("A car entry is null/invalid! Skipping insertGhost.");
                        yield break;
                    }
                }

                // 6) Finally, insert the ghost
                ghostManager.insertGhost(ReplayLoader.instance.getLoadedReplay(), true);
                MelonLogger.Msg("here 5");
            }

            // This is whatever async method you had before
            // that returns Task<string> for the record ID
            //private static async Task<string> getNextLeaderboardRecord(
            //    int track, int layout, int car, float bestTime)
            //{
            //    // Implementation of your method that fetches `recordId` from server
            //    // return ...
            //    await Task.Yield();
            //    return "dummy_record_id_for_example";
            //}
        }




        [HarmonyPatch(typeof(GhostManager), "loadLocalGhost")]
        public class PatchGhostManager2
        {
            public static bool Prefix(GhostManager __instance, string path)
            {
                try
                {
                    ReplayLoader.replayToLoad = path;
                    MelonLogger.Msg($"Ghost to load: {ReplayLoader.replayToLoad}");
                    PatchInitReplayMode.DownloadLocalReplayJson(ReplayLoader.instance, false);
                    __instance.insertGhost(ReplayLoader.instance.getLoadedReplay(), false);
                    return false;
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Something went wrong while trying to load the local ghost");
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(ReplayMenu), "openFolder")]
        public class PatchOpenFolder
        {
            public static bool Prefix(ReplayMenu __instance)
            {
                if (!__instance.animating)
                {
                    MelonLogger.Msg("Running custom code");
                    string arguments = (Il2Cpp.Utils.getRootFolder() + "ModReplays\\Local").Replace("/", "\\");
                    Process.Start("explorer.exe", arguments);
                }
                return false;
            }

        }


        [HarmonyPatch(typeof(ReplayLoader), "saveReplay")]
        public class PatchSaveReplay
        {
            public static bool Prefix(ReplayLoader __instance, bool saveAsGhost, string folder, int replaySlicesCount)
            {
                // Redirect to different folder
                if (folder == Constants.OldGhostFolder1 || folder == Constants.OldGhostFolder2)
                {
                    folder = Constants.NewGhostFolder;
                }
                else if (folder == Constants.OldReplayFolder1 || folder == Constants.OldReplayFolder2)
                {
                    folder = Constants.NewReplayFolder;
                }

                ReplayLoader.checkForFolder(folder);

                // Check if there are tracked cars in the replay system
                if (ReplaySystem.singleton.trackedCars.Count == 0)
                {
                    return false; // Skip original method
                }
 
                // Extract replay details
                float timeStamp = ReplaySystem.singleton.trackedCars[0].timeSlices[replaySlicesCount - 1].timeStamp;
                ReplayHeader replayHeader = new ReplayHeader(
                    2,
                    Il2CppSystem.DateTime.Now,
                    EventManager.singleton.isMultiplayer,
                    timeStamp,
                    EventLoader.singleton.localeName,
                    EventManager.singleton.reverse,
                    Weather.singleton.night
                );

                // Add car information to replay header
                for (int i = 0; i < ReplaySystem.singleton.trackedCars.Count; i++)
                {
                    replayHeader.cars.Add(new Car(ReplaySystem.singleton.trackedCars[i].model, ReplaySystem.singleton.trackedCars[i].driver));
                }

                // Create ReplayData and populate it with time slices
                ReplayData replayData = new();
                for (int j = 0; j < ReplaySystem.singleton.trackedCars.Count; j++)
                {
                    replayData.timeslices.Add(ReplaySystem.singleton.trackedCars[j].timeSlices.GetRange(0, replaySlicesCount));
                }

                NetworkReplay networkReplay = new()
                {
                    header = ConvertReplayHeaderToDTO(replayHeader),
                    replayData = ConvertReplayDataToDTO(replayData)
                };

                // Serialize the NetworkReplay to JSON
                string jsonReplay = JsonConvert.SerializeObject(networkReplay, Formatting.Indented);

                // Create a NetworkReplay structure if saving as a ghost
                if (saveAsGhost)
                {
                    // Assign serialized JSON to the static field in CustomLeaderboardAndReplayMod
                    uploadReplayJson = jsonReplay;
                }

                // Generate a replay filename based on event details
                string replayFileName = GenerateReplayFileName(folder, saveAsGhost);

                // Compress the JSON replay data
                byte[] replayByteData = Encoding.UTF8.GetBytes(jsonReplay); // Convert JSON to bytes
                byte[] compressedReplayByteData = CompressReplay(replayByteData); // Compress the data

                // Write the compressed data to file
                string fullPath = Il2Cpp.Utils.getRootFolder() + folder + "/" + replayFileName + ".iuorep";
                MelonLogger.Msg($"Writing compressed replay to file at {fullPath}");
                File.WriteAllBytes(fullPath, compressedReplayByteData); // Write as binary
                MelonLogger.Msg("Done writing replay");

                // Update saving state
                __instance.saving = false;

                return false; // Skip original method
            }

            // Helper method to generate replay file name
            private static string GenerateReplayFileName(string folder, bool saveAsGhost)
            {
                string fileName = EventManager.singleton.eventStartDate.ToString("dd.M.yyyy-HH.mm") + " ";
                if (!EventManager.singleton.isMultiplayer)
                {
                    fileName += "Time Attack - ";
                    fileName += ReplaySystem.singleton.trackedCars[0].model;
                    fileName += " at ";
                    fileName += EventLoader.singleton.localeName;
                }
                else
                {
                    fileName += "Battle - ";
                    fileName = fileName + ReplaySystem.singleton.trackedCars[0].driver + " vs " + ReplaySystem.singleton.trackedCars[1].driver;
                    fileName += " at ";
                    fileName += EventLoader.singleton.localeName;
                    // Regex to match hex color codes of the format <#xxxxxx>
                    string colorPattern = @"<#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?>";

                    // Remove all occurrences of hex color codes
                    fileName = Regex.Replace(fileName, colorPattern, "");
                }

                if (saveAsGhost)
                {
                    fileName = "Ghost - ";
                    fileName += ReplaySystem.singleton.trackedCars[0].model;
                    fileName += " at ";
                    fileName += EventLoader.singleton.localeName;
                    if (EventLoader.reverseLayout)
                    {
                        fileName += " reverse";
                    }
                }

                return fileName;
            }

            // Conversion method from ReplayHeader to ReplayHeaderDTO
            private static ReplayHeaderDTO ConvertReplayHeaderToDTO(ReplayHeader header)
            {
                var carsDtoList = new List<CarDTO>();
                foreach (var car in header.cars)
                {
                    carsDtoList.Add(new CarDTO { model = car.model, driver = car.driver });
                }

                return new ReplayHeaderDTO
                {
                    version = header.version,
                    date = new System.DateTime(header.date.Ticks),
                    isBattle = header.isBattle,
                    replayDuration = header.replayDuration,
                    track = header.track,
                    altLayout = header.altLayout,
                    night = header.night,
                    cars = carsDtoList
                };
            }

        }


        [HarmonyPatch(typeof(ReplayLoader), "initReplayMode")]
        public class PatchInitReplayMode
        {
            public static bool Prefix(ReplayLoader __instance)
            {
                EventManager singleton = EventManager.singleton;
                singleton.startDelayTime = float.PositiveInfinity;
                singleton.raceEnded = true;
                singleton.raceStarted = true;
                __instance.replaySaved = true;
                singleton.gameCanvas.enabled = false;
                for (int i = 0; i < __instance.toHideInReplay.Length; i++)
                {
                    __instance.toHideInReplay[i].SetActive(false);
                }
                __instance.hudToggler.enabled = true;
                __instance.freeCam.enabled = true;
                __instance.editorActivator.enabled = true;

                if (ReplayLoader.isOnlineReplay)
                {
                    Task.Run(() => DownloadOnlineReplayJson(__instance, true));
                }
                else
                {
                    DownloadLocalReplayJson(__instance, true);
                }

                return false;
            }


            public static void DownloadLocalReplayJson(ReplayLoader instance, bool finishReplayInit)
            {
                try
                {
                    // Read the compressed replay file as a byte array
                    byte[] compressedData = File.ReadAllBytes(ReplayLoader.replayToLoad);

                    // Decompress the data
                    byte[] decompressedData = DecompressReplay(compressedData);

                    // Convert the decompressed data back to a JSON string
                    string fileContent = Encoding.UTF8.GetString(decompressedData);

                    // Deserialize JSON to NetworkReplayDTO
                    NetworkReplay networkReplay = JsonConvert.DeserializeObject<NetworkReplay>(fileContent);

                    // Convert DTO to ReplayHeader and ReplayData
                    instance.readHeader = ConvertDTOToIl2CppReplayHeader(networkReplay.header);
                    instance.readData = ConvertDTOToIl2CppReplayData(networkReplay.replayData);

                    if (finishReplayInit)
                    {
                        MelonLogger.Msg("Local replay deserialized successfully");
                        instance.finishReplayInit();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"Exception while deserializing local replay JSON: {ex.Message}");
                    EventManager.singleton.goToMainMenu();
                }
            }



            public static async Task DownloadOnlineReplayJson(ReplayLoader instance, bool finishReplayInit)
            {
                string requestUrl = Constants.NewGetGhostAddress + ReplayLoader.replayToLoad;
                MelonLogger.Msg("important msg here:");
                MelonLogger.Msg("Attempting to send out request in DownloadOnlineReplayJson with replay " + ReplayLoader.replayToLoad);
                using (HttpClient httpClient = new())
                {
                    try
                    {
                        httpClient.DefaultRequestHeaders.Add(Constants.ApiKeyHeader, BuildAPIKey());

                        HttpResponseMessage presignedUrlResponse = await httpClient.GetAsync(requestUrl);
                        if (presignedUrlResponse.IsSuccessStatusCode)
                        {
                            string presignedS3Url = await presignedUrlResponse.Content.ReadAsStringAsync();
                            HttpResponseMessage replayResponse = await httpClient.GetAsync(presignedS3Url);
                            if (replayResponse.IsSuccessStatusCode) {

                                // Decompress the response content
                                byte[] compressedData = await replayResponse.Content.ReadAsByteArrayAsync();

                                byte[] decompressedData = DecompressReplay(compressedData);

                                // Convert the decompressed byte array back to JSON
                                string responseJson = Encoding.UTF8.GetString(decompressedData);


                                // Deserialize the JSON
                                NetworkReplay networkReplay = JsonConvert.DeserializeObject<NetworkReplay>(responseJson);

                                // Process the replay data
                                instance.readHeader = ConvertReplayHeaderDTOToIl2Cpp(networkReplay.header);
                                instance.readData = ConvertReplayDataDTOToIl2Cpp(networkReplay.replayData);

                                if (finishReplayInit)
                                {
                                    // Invoke the finishReplayInit method to proceed with the original flow
                                    // Maybe need to ask enqueue here?
                                    CustomLeaderboardAndReplayMod.Enqueue(async () =>
                                    {
                                        instance.finishReplayInit();
                                    });
                                }
                            }
                            else
                            {
                                MelonLogger.Error("Bad response from server");
                                EventManager.singleton.goToMainMenu();
                            }
                        }
                        else
                        {
                            CustomLeaderboardAndReplayMod.Enqueue(() =>
                            {
                                MelonLogger.Msg("Unknown error getting replay from server");
                                EventManager.singleton.goToMainMenu();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        CustomLeaderboardAndReplayMod.Enqueue(() =>
                        {
                            MelonLogger.Msg($"Exception while deserializing replay JSON: {ex.Message}");
                            EventManager.singleton.goToMainMenu();
                        });
                    }
                }
            }

        }

        [HarmonyPatch(typeof(ReplayLoader), "getReplayList", typeof(string))]
        public static class PatchGetReplayList
        {
            public static bool Prefix(string folder, ref Il2CppSystem.Collections.Generic.List<ReplayLoader.Replay> __result)
            {

                // Redirect to different folder
                if (folder == Constants.OldReplayFolder1 || folder == Constants.OldReplayFolder2)
                {
                    folder = Constants.NewReplayFolder;
                }
                else if (folder == Constants.OldGhostFolder1 || folder == Constants.OldGhostFolder2)
                {
                    folder = Constants.NewGhostFolder;
                }

                ReplayLoader.checkForFolder(folder);
                var list = new Il2CppSystem.Collections.Generic.List<ReplayLoader.Replay>();
                string[] files = Directory.GetFiles(Il2Cpp.Utils.getRootFolder() + folder, "*.iuorep");

                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {

                        // Read the compressed replay file as a byte array
                        byte[] compressedData = File.ReadAllBytes(files[i]);

                        // Decompress the data
                        byte[] decompressedData = DecompressReplay(compressedData);

                        // Convert the decompressed data back to a JSON string
                        string fileContent = Encoding.UTF8.GetString(decompressedData);
                        // Read the content of the replay file

                        //string fileContent = File.ReadAllText(files[i]);

                        // Print the first 200 characters of the JSON content for debugging purposes
                        //MelonLogger.Msg($"Deserialized JSON from file {files[i]} (first 1000 chars): {fileContent.Substring(0, Math.Min(1000, fileContent.Length))}");

                        // Deserialize JSON to NetworkReplayDTO
                        NetworkReplay networkReplay = JsonConvert.DeserializeObject<NetworkReplay>(fileContent);

                        // Convert DTO to Replay object
                        ReplayHeader replayHeader = ConvertDTOToIl2CppReplayHeader(networkReplay.header);
                        ReplayData replayData = ConvertDTOToIl2CppReplayData(networkReplay.replayData);

                        if (replayHeader.version != 2)
                        {
                            ReplayLoader.handleIncompatibleReplay(files[i], Il2Cpp.Utils.getRootFolder() + folder);
                            continue;
                        }

                        ReplayLoader.Replay item = new ReplayLoader.Replay
                        {
                            path = files[i],
                            header = replayHeader,
                            data = replayData
                        };

                        // Print item details before adding to the list
                        //MelonLogger.Msg($"Adding Replay item to list: {JsonConvert.SerializeObject(item, Formatting.Indented, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }).Substring(0, 5000)}");

                        list.Add(item);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg("Unable to read replay: " + files[i] + " - Exception: " + ex.Message);
                    }
                }

                //MelonLogger.Msg($"Replay data: {list[0].data}");

                __result = list; // Set the correct result type
                return false; // Skip the original method
            }

        }

        public static byte[] DecompressReplay(byte[] compressedData)
        {
            using (var compressedStream = new MemoryStream(compressedData))
            using (var decompressedStream = new MemoryStream())
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(decompressedStream);
                return decompressedStream.ToArray();
            }
        }

    }


    //
    // DTO classes for JSON deserialization
    //

    [Serializable]
    public class CarDTO
    {
        public string model;
        public string driver;

        public CarDTO() { }
    }


    [Serializable]
    public class TimeSliceDTO
    {
        public float timeStamp;
        public PositionDTO position;
        public RotationDTO rotation;
        public VelocityDTO velocity;
        public float gas;
        public float brake;
        public float clutch;
        public float steering;
        public bool handbrake;
        public float rpm;
        public int gear;
        public bool headLights;

        public TimeSliceDTO() { }

        [Serializable]
        public class PositionDTO
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        public class RotationDTO
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        [Serializable]
        public class VelocityDTO
        {
            public float x;
            public float y;
            public float z;
        }
    }


    [Serializable]
    public class ReplayDataDTO
    {
        public List<List<TimeSliceDTO>> timeslices;

        public ReplayDataDTO()
        {
            timeslices = new List<List<TimeSliceDTO>>();
        }
    }


    [Serializable]
    public class ReplayHeaderDTO
    {
        public int version;
        public DateTime date;
        public bool isBattle;
        public float replayDuration;
        public string track;
        public bool altLayout;
        public bool night;
        public List<CarDTO> cars;

        public ReplayHeaderDTO()
        {
            cars = new List<CarDTO>();
        }
    }


    [Serializable]
    public class NetworkReplay
    {
        public ReplayHeaderDTO header;
        public ReplayDataDTO replayData;
    }
}
