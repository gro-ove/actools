using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using SlimDX;

namespace AcManager.Tools.SharedMemory {
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode), Serializable]
    public class AcSharedPhysics {
        public int PacketId;
        public float Gas;
        public float Brake;
        public float Fuel;
        public int Gear;
        public int Rpms;
        public float SteerAngle;
        public float SpeedKmh;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] Velocity;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] AccG;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] WheelSlip;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] WheelLoad;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] WheelPressure;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] WheelAngularSpeed;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TyreWear;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TyreDirtyLevel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TyreCoreTemperature;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] CamberRad;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] SuspensionTravel;

        public float Drs;
        public float TC;
        public float Heading;
        public float Pitch;
        public float Roll;
        public float CgHeight;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public float[] CarDamage;

        public int NumberOfTyresOut;

        [MarshalAs(UnmanagedType.Bool)]
        public bool PitLimiterOn;
        public float Abs;

        public float KersCharge;
        public float KersInput;

        [MarshalAs(UnmanagedType.Bool)]
        public bool AutoShifterOn;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] RideHeight;

        // added in 1.5
        public float TurboBoost;
        public float Ballast;
        public float AirDensity;

        // added in 1.6
        public float AirTemp;
        public float RoadTemp;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] LocalAngularVelocity;

        public float FinalFF;

        // added in 1.7
        public float PerformanceMeter;
        public int EngineBrake;
        public int ErsRecoveryLevel;
        public int ErsPowerLevel;
        public int ErsHeatCharging;
        public int ErsisCharging;
        public float KersCurrentKJ;
        public int DrsAvailable;
        public int DrsEnabled;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] BrakeTemperature;

        // added in 1.10
        public float Clutch;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TyreTempI;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TyreTempM;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TyreTempO;

        // added in 1.10.2
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsAiControlled;

        // added in 1.11
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public Vector3[] TyreContactPoint;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public Vector3[] TyreContactNormal;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public Vector3[] TyreContactHeading;

        public float BrakeBias;

        [Pure, NotNull]
        public static AcSharedPhysics FromFile([NotNull] MemoryMappedFile file) {
            if (file == null) throw new ArgumentNullException(nameof(file));
            using (var stream = file.CreateViewStream())
            using (var reader = new BinaryReader(stream)) {
                var size = Marshal.SizeOf(typeof(AcSharedPhysics));
                var bytes = reader.ReadBytes(size);
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                var data = (AcSharedPhysics)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(AcSharedPhysics));
                handle.Free();
                return data;
            }
        }
    }
}
