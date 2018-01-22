using System;
using System.Runtime.InteropServices;

namespace AcTools.AiFile {
    [StructLayout(LayoutKind.Sequential, Pack = 4), Serializable]
    public struct AiPoint {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] Position;

        public float Length;
        public int Id;

        public static readonly int Size = Marshal.SizeOf(typeof(AiPointExtra));
        public static readonly byte[] Buffer = new byte[Size];

        public void LoadFrom(ReadAheadBinaryReader reader) {
            Position = reader.ReadSingle3D();
            Length = reader.ReadSingle();
            Id = reader.ReadInt32();
        }
    }
}