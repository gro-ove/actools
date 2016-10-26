using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace AcManager.Tools.SharedMemory {
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode), Serializable]
    public class AcSharedStaticInfo {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string SharedMemoryVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string AssettoCorsaVersion;

        // session static info
        public int NumberOfSessions;
        public int NumberOfCars;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string CarModel;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string Track;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string PlayerName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string PlayerSurname;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string PlayerNickname;

        public int SectorCount;

        // car static info
        public float MaxTorque;
        public float MaxPower;
        public int MaxRpm;
        public float MaxFuel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] SuspensionMaxTravel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TyreRadius;

        // added in 1.5
        public float MaxTurboBoost;

        [Obsolete]
        public float AirTemperature; // AirTemp since 1.6 in physic

        [Obsolete]
        public float RoadTemperature; // RoadTemp since 1.6 in physic

        public int PenaltiesEnabled;
        public float AidFuelRate;
        public float AidTireRate;
        public float AidMechanicalDamage;
        public int AidAllowTyreBlankets;
        public float AidStability;
        public int AidAutoClutch;
        public int AidAutoBlip;

        // added in 1.7.1
        public int HasDRS;
        public int HasERS;
        public int HasKERS;
        public float KersMaxJoules;
        public int EngineBrakeSettingsCount;
        public int ErsPowerControllerCount;

        // added in 1.7.2
        public float TrackSplineLength;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string TrackConfiguration;

        [Pure]
        public static AcSharedStaticInfo FromFile([NotNull] MemoryMappedFile file) {
            if (file == null) throw new ArgumentNullException(nameof(file));
            using (var stream = file.CreateViewStream())
            using (var reader = new BinaryReader(stream)) {
                var size = Marshal.SizeOf(typeof(AcSharedStaticInfo));
                var bytes = reader.ReadBytes(size);
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                var data = (AcSharedStaticInfo)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(AcSharedStaticInfo));
                handle.Free();
                return data;
            }
        }
    }
}
