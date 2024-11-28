#pragma warning disable IDE0051

using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using MelonLoader;
using Il2Cpp;
using static Il2Cpp.StartCamera;
using static MelonLoader.MelonLogger;

namespace ModNamespace
{
    public partial class CustomLeaderboardAndReplayMod : MelonMod
    {
        public static string uploadReplayJson;

        [HarmonyPatch(typeof(ReplayLoader), "finishReplayInit")]
        public class LBMPatch
        {
            public static void Prefix(ReplayLoader __instance)
            {
                // Print Replay Header
                MelonLogger.Msg($"Replay Header:");
                MelonLogger.Msg($"Version: {__instance.readHeader.version}");
                MelonLogger.Msg($"Date: {__instance.readHeader.date}");
                MelonLogger.Msg($"Is Battle: {__instance.readHeader.isBattle}");
                MelonLogger.Msg($"Replay Duration: {__instance.readHeader.replayDuration}");
                MelonLogger.Msg($"Track: {__instance.readHeader.track}");
                MelonLogger.Msg($"Alt Layout: {__instance.readHeader.altLayout}");
                MelonLogger.Msg($"Night: {__instance.readHeader.night}");

                // Print Cars in Replay Header
                foreach (var car in __instance.readHeader.cars)
                {
                    MelonLogger.Msg($"Car Model: {car.model}, Driver: {car.driver}");
                }

                // Print Replay Data
                MelonLogger.Msg($"Replay Data:");
                for (int i = 0; i < __instance.readData.timeslices.Count; i++)
                {
                    MelonLogger.Msg($"Car {i + 1} Time Slices:");
                    foreach (var timeSlice in __instance.readData.timeslices[i])
                    {
                        MelonLogger.Msg($"Timestamp: {timeSlice.timeStamp}, Position: ({timeSlice.position.x}, {timeSlice.position.y}, {timeSlice.position.z}), Velocity: ({timeSlice.velocity.x}, {timeSlice.velocity.y}, {timeSlice.velocity.z})");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ReplayLoader), "initReplayMode")]
        public class ReplayModePatch
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
                    Task.Run(() => DownloadReplayJson(__instance));
                }
                else
                {
                    __instance.readReplayAsync(new Action(__instance.finishReplayInit), ReplayLoader.replayToLoad);
                }

                return false; // Skip the original method
            }

            // Update DownloadReplayJson to use the correct conversion method
            private static async Task DownloadReplayJson(ReplayLoader instance)
            {
                // Construct the URL to download the replay JSON
                string requestUrl = "https://o2hm4g1w50.execute-api.us-east-1.amazonaws.com/prod/getGhost";
                using (HttpClient httpClient = new HttpClient())
                {
                    try
                    {
                        HttpResponseMessage response = await httpClient.GetAsync(requestUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            string jsonResponse = await response.Content.ReadAsStringAsync();
                            NetworkReplay networkReplay = JsonConvert.DeserializeObject<NetworkReplay>(jsonResponse);

                            // Log the deserialized ReplayHeaderDTO, particularly the car list
                            MelonLogger.Msg("Deserialized ReplayHeaderDTO:");
                            MelonLogger.Msg($"Version: {networkReplay.header.version}");
                            MelonLogger.Msg($"Cars Count: {networkReplay.header.cars.Count}");
                            foreach (var car in networkReplay.header.cars)
                            {
                                MelonLogger.Msg($"Car Model: {car.model}, Driver: {car.driver}");
                            }

                            // Debugging deserialized data
                            foreach (var timeSliceList in networkReplay.replayData.timeslices)
                            {
                                foreach (var timeSlice in timeSliceList)
                                {
                                    //MelonLogger.Msg($"Deserialized TimeSliceDTO - Timestamp: {timeSlice.timeStamp}, Position: ({timeSlice.position.x}, {timeSlice.position.y}, {timeSlice.position.z})");
                                }
                            }

                            // Enqueue the action to update the replay data on the main thread
                            CustomLeaderboardAndReplayMod.Enqueue(() =>
                            {
                                instance.readHeader = ConvertDTOToReplayHeader(networkReplay.header);
                                instance.readData = ConvertReplayDataDTOToIl2Cpp(networkReplay.replayData);

                                MelonLogger.Msg("invoking finishReplayInit");
                                // Invoke the finishReplayInit method to proceed with the original flow
                                instance.finishReplayInit();
                            });
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

            // Method to convert ReplayDataDTO to Il2Cpp ReplayData
            private static ReplayLoader.ReplayData ConvertReplayDataDTOToIl2Cpp(ReplayDataDTO dto)
            {
                ReplayLoader.ReplayData replayData = new ReplayLoader.ReplayData();

                foreach (var timeSliceListDTO in dto.timeslices)
                {

                    var il2cppTimeSliceList = new Il2CppSystem.Collections.Generic.List<ReplaySystem.TimeSlice>();
                    foreach (var timeSliceDTO in timeSliceListDTO)
                    {
                        //MelonLogger.Msg($"Converting TimeSliceDTO - Timestamp: {timeSliceDTO.timeStamp}, Position: ({timeSliceDTO.position.x}, {timeSliceDTO.position.y}, {timeSliceDTO.position.z})");

                        ReplaySystem.TimeSlice il2cppTimeSlice = new ReplaySystem.TimeSlice(
                            timeSliceDTO.timeStamp,
                            new Vector3(timeSliceDTO.position.x, timeSliceDTO.position.y, timeSliceDTO.position.z),
                            new Quaternion(timeSliceDTO.rotation.x, timeSliceDTO.rotation.y, timeSliceDTO.rotation.z, timeSliceDTO.rotation.w),
                            new Vector3(timeSliceDTO.velocity.x, timeSliceDTO.velocity.y, timeSliceDTO.velocity.z),
                            timeSliceDTO.gas,
                            timeSliceDTO.brake,
                            timeSliceDTO.clutch,
                            timeSliceDTO.steering,
                            timeSliceDTO.handbrake,
                            timeSliceDTO.rpm,
                            timeSliceDTO.gear,
                            timeSliceDTO.headLights
                        );

                        //MelonLogger.Msg($"Created TimeSlice - Timestamp: {il2cppTimeSlice.timeStamp}, Position: {il2cppTimeSlice.position}, Velocity: {il2cppTimeSlice.velocity}");
                        il2cppTimeSliceList.Add(il2cppTimeSlice);
                    }
                    replayData.timeslices.Add(il2cppTimeSliceList);
                }

                return replayData;
            }

            // Method to convert ReplayHeaderDTO to Il2Cpp.ReplayLoader.ReplayHeader
            private static ReplayLoader.ReplayHeader ConvertDTOToReplayHeader(ReplayHeaderDTO dto)
            {
                var il2cppReplayHeader = new ReplayLoader.ReplayHeader(
                    dto.version,
                    new Il2CppSystem.DateTime(dto.date.Ticks), // Use a compatible DateTime constructor
                    dto.isBattle,
                    dto.replayDuration,
                    dto.track,
                    dto.altLayout,
                    dto.night
                );

                // Ensure cars are copied over
                foreach (var carDto in dto.cars)
                {
                    MelonLogger.Msg($"Converting CarDTO - Model: {carDto.model}, Driver: {carDto.driver}");
                    var il2cppCar = new ReplayLoader.Car(carDto.model, carDto.driver);
                    il2cppReplayHeader.cars.Add(il2cppCar);
                }

                return il2cppReplayHeader;
            }
        }
    }

    // DTO classes for JSON deserialization
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

        // Nested classes to properly deserialize nested JSON objects
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


    // The new NetworkReplay class used for JSON deserialization
    [Serializable]
    public class NetworkReplay
    {
        public ReplayHeaderDTO header;
        public ReplayDataDTO replayData;
    }
}
