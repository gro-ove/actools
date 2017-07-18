using System;
using System.Runtime.InteropServices;
using SlimDX;

namespace AcManager.Tools.SharedMemory {
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode), Serializable]
    public class AcSharedGraphics {
        public int PacketId;
        public AcGameStatus Status;
        public AcSharedSessionType Session;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string CurrentTime;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string LastTime;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string BestTime;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string Split;

        public int CompletedLaps;
        public int Position;
        public int CurrentTimeMs;
        public int LastTimeMs;
        public int BestTimeMs;
        public float SessionTimeLeft;
        public float DistanceTraveled;

        [MarshalAs(UnmanagedType.Bool)]
        public bool IsInPit;

        public int CurrentSectorIndex;
        public int LastSectorTime;
        public int NumberOfLaps;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string TyreCompound;

        public float ReplayTimeMultiplier;
        public float NormalizedCarPosition;

        // [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public Vector3 CarCoordinates;

        public float PenaltyTime;
        public AcFlagType Flag;

        [MarshalAs(UnmanagedType.Bool)]
        public bool IdealLineOn;

        // added in 1.5
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsInPitLane;
        public float SurfaceGrip;

        // added in 1.13
        [MarshalAs(UnmanagedType.Bool)]
        public bool MandatoryPitDone;

        public static readonly int Size = Marshal.SizeOf(typeof(AcSharedGraphics));
        public static readonly byte[] Buffer = new byte[Size];
    }
}
