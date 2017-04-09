using System;
using System.IO;
using JetBrains.Annotations;

namespace AcTools.AiFile {
    public partial class AiLane {
        public readonly int Version;
        public readonly long Size;
        public readonly int SizeA, SizeB;

        public readonly AiPoint[] Points;

        private AiLane([NotNull] string filename) {
            using (var reader = new ReadAheadBinaryReader(filename)) {
                Version = reader.ReadInt32();
                if (Version != 7) throw new Exception($"Version {Version} not supported");

                Size = reader.ReadInt64();
                Points = new AiPoint[Size];

                for (var i = 0; i < Size; i++) {
                    Points[i].Id = reader.ReadInt32();
                    Points[i].Position = reader.ReadSingle3D();
                    Points[i].Length = reader.ReadSingle();
                }

                SizeA = reader.ReadInt32();
                SizeB = reader.ReadInt32();

                if (SizeA != Size - 1) throw new Exception($"Unexpected SizeA value ({Size - 1} expected, {SizeA} found)");
                if (SizeB != Size) AcToolsLogging.Write($"Unexpected SizeB value ({Size} expected, {SizeB} found)");
                
                for (var i = 0; i < Size; i++) {
                    var e = new float[18];
                    for (var j = 0; j < 18; j++) {
                        e[j] = reader.ReadSingle();
                    }

                    Points[i].Extra = e;
                }

                // there are some more bytes, but I have no idea what are they for
            }
        }

        public static AiLane FromFile([NotNull] string filename) {
            if (!File.Exists(filename)) throw new FileNotFoundException(filename);
            return new AiLane(filename);
        }
    }
}
