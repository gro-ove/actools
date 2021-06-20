using System;
using System.Runtime.InteropServices;
using AcTools.Numerics;

namespace AcTools.AiFile {
    [StructLayout(LayoutKind.Sequential, Pack = 4), Serializable]
    public struct AiPoint {
        public Vec3 Position;
        public float Length;
        public int Id;

        public static readonly int Size = Marshal.SizeOf(typeof(AiPointExtra));
        public static readonly byte[] Buffer = new byte[Size];

        public void LoadFrom(ReadAheadBinaryReader reader) {
            Position = reader.ReadVec3();
            Length = reader.ReadSingle();
            Id = reader.ReadInt32();
        }
    }
}