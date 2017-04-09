using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace AcManager.Tools.SharedMemory {
    public class AcSharedConsts {
        public const int VersionSize = 15;
        public const int IdSize = 33;
        public const int LayoutIdSize = 15;
        public const int NameSize = 33;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode), Serializable]
    public class AcSharedStaticInfo {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AcSharedConsts.VersionSize)]
        public string SharedMemoryVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AcSharedConsts.VersionSize)]
        public string AssettoCorsaVersion;

        // session static info
        public int NumberOfSessions;
        public int NumberOfCars;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AcSharedConsts.IdSize)]
        public string CarModel;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AcSharedConsts.IdSize)]
        public string Track;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AcSharedConsts.NameSize)]
        public string PlayerName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AcSharedConsts.NameSize)]
        public string PlayerSurname;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AcSharedConsts.NameSize)]
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
        [MarshalAs(UnmanagedType.Bool)]
        public bool HasDRS;

        [MarshalAs(UnmanagedType.Bool)]
        public bool HasERS;

        [MarshalAs(UnmanagedType.Bool)]
        public bool HasKERS;

        public float KersMaxJoules;
        public int EngineBrakeSettingsCount;
        public int ErsPowerControllerCount;

        // added in 1.7.2
        public float TrackSplineLength;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = AcSharedConsts.LayoutIdSize)]
        public string TrackConfiguration;

        // added in 1.10.2
        public float ErsMaxJ;

        // added in 1.13
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsTimedRace;

        [MarshalAs(UnmanagedType.Bool)]
        public bool HasExtraLap;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string CarSkin;

        public int ReversedGridPositions;
        public int PitWindowStart;
        public int PitWindowEnd;

        public static readonly int Size = Marshal.SizeOf(typeof(AcSharedStaticInfo));
        public static readonly byte[] Buffer = new byte[Size];
    }
}
