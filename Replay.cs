#pragma warning disable IDE0051

using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using MelonLoader;
using Il2Cpp;
using static Il2Cpp.ReplayLoader;
using static ModNamespace.ReplayConversionUtils;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {

        [HarmonyPatch(typeof(GhostManager), "getOnlineGhost")]
        public class PatchGhostManager1
        {
            public static bool Prefix()
            {
                MelonLogger.Msg("In GhostManager getOnlineGhost");
                //MelonLogger.Msg($"lastBestTime: {EventManager.singleton.lastBestTime}"); // Works
                float lastBestTime = EventManager.singleton.lastBestTime;

                // TODO: implement
                //next up: need to patch getOnlineGhost, set out a network request to get top(however many --maybe 50, maybe 200) times and determine which one is just above you, then send a
                //request to download that one and load it.Its pretty straightforward, just have to set the static replayToLoad field(With recordId) before calling DownloadReplayJson
                //also need to add a flag to the DownloadReplayJson field so you can disable finishReplayInit. Might be nice to put up a text box telling the user who they're racing (what place on the LB)
                // Above is done

                return false;
            }
        }

        [HarmonyPatch(typeof(GhostManager), "loadLocalGhost")]
        public class PatchGhostManager2
        {
            public static bool Prefix(GhostManager __instance, string path)
            {
                MelonLogger.Msg("In GhostManager loadLocalGhost, calling DownloadLocalReplay");
                MelonLogger.Msg($"Path: {path}");
                ReplayLoader.replayToLoad = path;
                MelonLogger.Msg($"Ghost to load: {ReplayLoader.replayToLoad}");
                PatchInitReplayMode.DownloadLocalReplayJson(ReplayLoader.instance, false);
                __instance.insertGhost(ReplayLoader.instance.getLoadedReplay(), false);
                return true;
            }
        }

        [HarmonyPatch(typeof(ReplayMenu), "openFolder")]
        public class PatchOpenFolder
        {
            public static bool Prefix(ReplayMenu __instance)
            {
                if (!__instance.animating)
                {
                    string arguments = (Utils.getRootFolder() + "ModReplays").Replace("/", "\\");
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
                if (folder == "Ghosts\\Local")
                {
                    folder = "ModGhosts\\Local";
                }
                else if (folder == "Replays")
                {
                    folder = "ModReplays";
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

                
                // Write JSON to file
                string fullPath = Utils.getRootFolder() + folder + "/" + replayFileName + ".iureplay";
                MelonLogger.Msg($"Writing replay to file at {fullPath}");
                File.WriteAllText(fullPath, jsonReplay);
                MelonLogger.Msg("Done writing");

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

                MelonLogger.Msg("About to be there...");
                if (ReplayLoader.isOnlineReplay)
                {
                    Task.Run(() => DownloadOnlineReplayJson(__instance));
                }
                else
                {
                    MelonLogger.Msg("in the right place!");
                    MelonLogger.Msg(ReplayLoader.replayToLoad);
                    DownloadLocalReplayJson(__instance, true);
                }

                return false;
            }


            public static void DownloadLocalReplayJson(ReplayLoader instance, bool finishReplayInit)
            {
                try
                {
                    // Read the content of the replay file
                    string fileContent = File.ReadAllText(ReplayLoader.replayToLoad);

                    // Deserialize JSON to NetworkReplayDTO
                    NetworkReplay networkReplay = JsonConvert.DeserializeObject<NetworkReplay>(fileContent);

                    // Convert DTO to ReplayHeader and ReplayData
                    instance.readHeader = ConvertDTOToIl2CppReplayHeader(networkReplay.header);
                    instance.readData = ConvertDTOToIl2CppReplayData(networkReplay.replayData);

                    if (finishReplayInit)
                    {
                        MelonLogger.Msg("Local replay deserialized successfully, invoking finishReplayInit...");
                        instance.finishReplayInit();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"Exception while deserializing local replay JSON: {ex.Message}");
                    EventManager.singleton.goToMainMenu();
                }
            }


            private static async Task DownloadOnlineReplayJson(ReplayLoader instance)
            {
                string requestUrl = Constants.NewGetGhostAddress + ReplayLoader.replayToLoad;
                using (HttpClient httpClient = new())
                {
                    try
                    {
                        HttpResponseMessage presignedUrlResponse = await httpClient.GetAsync(requestUrl);
                        if (presignedUrlResponse.IsSuccessStatusCode)
                        {
                            string presignedS3Url = await presignedUrlResponse.Content.ReadAsStringAsync();
                            HttpResponseMessage replayResponse = await httpClient.GetAsync(presignedS3Url);
                            if (replayResponse.IsSuccessStatusCode) {
                                string responseJson = await replayResponse.Content.ReadAsStringAsync();
                                
                                NetworkReplay networkReplay = JsonConvert.DeserializeObject<NetworkReplay>(responseJson);

                                // Enqueue the action to update the replay data on the main thread
                                CustomLeaderboardAndReplayMod.Enqueue(() =>
                                {
                                    instance.readHeader = ConvertReplayHeaderDTOToIl2Cpp(networkReplay.header);
                                    instance.readData = ConvertReplayDataDTOToIl2Cpp(networkReplay.replayData);

                                    // Invoke the finishReplayInit method to proceed with the original flow
                                    instance.finishReplayInit();
                                });
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
                folder = "ModGhosts\\Local";

                ReplayLoader.checkForFolder(folder);
                var list = new Il2CppSystem.Collections.Generic.List<ReplayLoader.Replay>();
                string[] files = Directory.GetFiles(Utils.getRootFolder() + folder, "*.iureplay");

                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        // Read the content of the replay file
                        string fileContent = File.ReadAllText(files[i]);

                        // Print the first 200 characters of the JSON content for debugging purposes
                        //MelonLogger.Msg($"Deserialized JSON from file {files[i]} (first 1000 chars): {fileContent.Substring(0, Math.Min(1000, fileContent.Length))}");

                        // Deserialize JSON to NetworkReplayDTO
                        NetworkReplay networkReplay = JsonConvert.DeserializeObject<NetworkReplay>(fileContent);

                        // Convert DTO to Replay object
                        ReplayHeader replayHeader = ConvertDTOToIl2CppReplayHeader(networkReplay.header);
                        ReplayData replayData = ConvertDTOToIl2CppReplayData(networkReplay.replayData);

                        if (replayHeader.version != 2)
                        {
                            ReplayLoader.handleIncompatibleReplay(files[i], Utils.getRootFolder() + folder);
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
