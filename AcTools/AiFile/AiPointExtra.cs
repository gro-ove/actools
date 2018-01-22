using System;
using System.Runtime.InteropServices;

namespace AcTools.AiFile {
    [StructLayout(LayoutKind.Sequential, Pack = 4), Serializable]
    public struct AiPointExtra {
        public float Speed;
        public float Gas;
        public float Brake;
        public float ObsoleteLatG;
        public float Radius;
        public float SideLeft;
        public float SideRight;
        public float Camber;
        public float Direction;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] Normal;

        public float Length;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] ForwardVector;

        public float Tag;
        public float Grade;

        public float Width => SideLeft + SideRight;

        public static readonly int Size = Marshal.SizeOf(typeof(AiPointExtra));
        public static readonly byte[] Buffer = new byte[Size];

        public void LoadFrom(ReadAheadBinaryReader reader) {
            Speed = reader.ReadSingle();
            Gas = reader.ReadSingle();
            Brake = reader.ReadSingle();
            ObsoleteLatG = reader.ReadSingle();
            Radius = reader.ReadSingle();
            SideLeft = reader.ReadSingle();
            SideRight = reader.ReadSingle();
            Camber = reader.ReadSingle();
            Direction = reader.ReadSingle();
            Normal = reader.ReadSingle3D();
            Length = reader.ReadSingle();
            ForwardVector = reader.ReadSingle3D();
            Tag = reader.ReadSingle();
            Grade = reader.ReadSingle();
        }
    }
}