using System;
using System.Runtime.InteropServices;
using AcTools.Numerics;

namespace AcTools.AiFile {
    [StructLayout(LayoutKind.Sequential, Pack = 4), Serializable]
    public struct AiSplineGrid {
        public Vec3 MaxExtreme;
        public Vec3 MinExtreme;
        public int NeighborsConsideredNumber;
        public float SamplingDensity;

        public Item[] Items;

        public AiSplineGrid(ReadAheadBinaryReader reader) {
            MaxExtreme = reader.ReadVec3();
            MinExtreme = reader.ReadVec3();
            NeighborsConsideredNumber = reader.ReadInt32();
            SamplingDensity = reader.ReadSingle();
            Items = new Item[reader.ReadInt32()];
            for (int i = 0, c = Items.Length; i < c; i++) {
                Items[i].LoadFrom(reader);
            }
        }

        public struct ItemSub {
            public int[] Values;

            public void LoadFrom(ReadAheadBinaryReader reader) {
                Values = new int[reader.ReadInt32()];
                for (int i = 0, c = Values.Length; i < c; i++) {
                    Values[i] = reader.ReadInt32();
                }
            }
        }

        public struct Item {
            public ItemSub[] Items;

            public void LoadFrom(ReadAheadBinaryReader reader) {
                Items = new ItemSub[reader.ReadInt32()];
                for (int i = 0, c = Items.Length; i < c; i++) {
                    Items[i].LoadFrom(reader);
                }
            }
        }
    }
}