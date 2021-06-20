using System;
using System.Runtime.InteropServices;
using AcTools.Numerics;

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
        public Vec3 Normal;
        public float Length;
        public Vec3 ForwardVector;
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
            Normal = reader.ReadVec3();
            Length = reader.ReadSingle();
            ForwardVector = reader.ReadVec3();
            Tag = reader.ReadSingle();
            Grade = reader.ReadSingle();
        }
    }
}