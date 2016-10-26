using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
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
        public int CurrentTimeInt;
        public int LastTimeInt;
        public int BestTimeInt;
        public float SessionTimeLeft;
        public float DistanceTraveled;
        public int IsInPit;
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
        public int IdealLineOn;

        // added in 1.5
        public int IsInPitLane;
        public float SurfaceGrip;

        [Pure]
        public static AcSharedGraphics FromFile([NotNull] MemoryMappedFile file) {
            if (file == null) throw new ArgumentNullException(nameof(file));
            using (var stream = file.CreateViewStream())
            using (var reader = new BinaryReader(stream)) {
                var size = Marshal.SizeOf(typeof(AcSharedGraphics));
                var bytes = reader.ReadBytes(size);
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                var data = (AcSharedGraphics)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(AcSharedGraphics));
                handle.Free();
                return data;
            }
        }
    }
}
