using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2Cpp;
using UnityEngine;
using static Il2Cpp.ReplayLoader;

namespace ModNamespace
{
    public class ReplayConversionUtils
    {

        public static Il2Cpp.ReplayLoader.ReplayHeader ConvertDTOToIl2CppReplayHeader(ReplayHeaderDTO dto)
        {
            Il2Cpp.ReplayLoader.ReplayHeader replayHeader = new(
                dto.version,
                Il2CppSystem.DateTime.Now,
                //new Il2CppSystem.DateTime(dto.date.Ticks), // Using compatible DateTime constructor
                dto.isBattle,
                dto.replayDuration,
                dto.track,
                dto.altLayout,
                dto.night
            );

            var il2cppCars = new Il2CppSystem.Collections.Generic.List<ReplayLoader.Car>();
            foreach (var carDTO in dto.cars)
            {
                var il2cppCar = new ReplayLoader.Car(carDTO.model, carDTO.driver);
                il2cppCars.Add(il2cppCar);
            }

            replayHeader.cars = il2cppCars;

            return replayHeader;
        }


        public static Il2Cpp.ReplayLoader.ReplayData ConvertDTOToIl2CppReplayData(ReplayDataDTO dto)
        {
            Il2Cpp.ReplayLoader.ReplayData replayData = new Il2Cpp.ReplayLoader.ReplayData();

            foreach (var timeSliceListDTO in dto.timeslices)
            {
                var il2cppTimeSliceList = new Il2CppSystem.Collections.Generic.List<ReplaySystem.TimeSlice>();
                foreach (var timeSliceDTO in timeSliceListDTO)
                {
                    ReplaySystem.TimeSlice timeSlice = new ReplaySystem.TimeSlice(
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
                    il2cppTimeSliceList.Add(timeSlice);
                }
                replayData.timeslices.Add(il2cppTimeSliceList);
            }

            return replayData;
        }


        public static ReplayDataDTO ConvertReplayDataToDTO(ReplayData replayData)
        {
            ReplayDataDTO replayDataDTO = new ReplayDataDTO();

            foreach (var timeSliceList in replayData.timeslices)
            {
                List<TimeSliceDTO> timeSliceDTOList = new List<TimeSliceDTO>();
                foreach (var timeSlice in timeSliceList)
                {
                    TimeSliceDTO timeSliceDTO = new TimeSliceDTO
                    {
                        timeStamp = timeSlice.timeStamp,
                        position = new TimeSliceDTO.PositionDTO
                        {
                            x = timeSlice.position.x,
                            y = timeSlice.position.y,
                            z = timeSlice.position.z
                        },
                        rotation = new TimeSliceDTO.RotationDTO
                        {
                            x = timeSlice.rotation.x,
                            y = timeSlice.rotation.y,
                            z = timeSlice.rotation.z,
                            w = timeSlice.rotation.w
                        },
                        velocity = new TimeSliceDTO.VelocityDTO
                        {
                            x = timeSlice.velocity.x,
                            y = timeSlice.velocity.y,
                            z = timeSlice.velocity.z
                        },
                        gas = timeSlice.gas,
                        brake = timeSlice.brake,
                        clutch = timeSlice.clutch,
                        steering = timeSlice.steering,
                        handbrake = timeSlice.handbrake,
                        rpm = timeSlice.rpm,
                        gear = timeSlice.gear,
                        headLights = timeSlice.headLights
                    };

                    timeSliceDTOList.Add(timeSliceDTO);
                }
                replayDataDTO.timeslices.Add(timeSliceDTOList);
            }

            return replayDataDTO;
        }


        // Converts ReplayDataDTO to Il2Cpp.ReplayLoader.ReplayData
        public static ReplayLoader.ReplayData ConvertReplayDataDTOToIl2Cpp(ReplayDataDTO dto)
        {
            ReplayLoader.ReplayData replayData = new();

            foreach (var timeSliceListDTO in dto.timeslices)
            {

                var il2cppTimeSliceList = new Il2CppSystem.Collections.Generic.List<ReplaySystem.TimeSlice>();
                foreach (var timeSliceDTO in timeSliceListDTO)
                {
                    ReplaySystem.TimeSlice il2cppTimeSlice = new(
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

                    il2cppTimeSliceList.Add(il2cppTimeSlice);
                }
                replayData.timeslices.Add(il2cppTimeSliceList);
            }

            return replayData;
        }



        // Converts ReplayHeaderDTO to Il2Cpp.ReplayLoader.ReplayHeader
        public static ReplayLoader.ReplayHeader ConvertReplayHeaderDTOToIl2Cpp(ReplayHeaderDTO dto)
        {
            var il2cppReplayHeader = new ReplayLoader.ReplayHeader(
                dto.version,
                new Il2CppSystem.DateTime(dto.date.Ticks),
                dto.isBattle,
                dto.replayDuration,
                dto.track,
                dto.altLayout,
                dto.night
            );

            foreach (var carDto in dto.cars)
            {
                var il2cppCar = new ReplayLoader.Car(carDto.model, carDto.driver);
                il2cppReplayHeader.cars.Add(il2cppCar);
            }

            return il2cppReplayHeader;
        }


    }
}
