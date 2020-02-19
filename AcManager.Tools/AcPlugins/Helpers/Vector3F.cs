using System;
using System.Runtime.Serialization;

namespace AcManager.Tools.AcPlugins.Helpers {
    [DataContract]
    public struct Vector3F {
        [DataMember]
        public float X { get; set; }

        [DataMember]
        public float Y { get; set; }

        [DataMember]
        public float Z { get; set; }

        public Vector3F(float x, float y, float z) {
            X = x;
            Y = y;
            Z = z;
        }

        public float Length() {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public static Vector3F operator +(Vector3F a, Vector3F b) {
            return new Vector3F(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vector3F operator -(Vector3F a, Vector3F b) {
            return new Vector3F(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public override string ToString() {
            return $"[{X} , {Y} , {Z}]";
        }

        private static Random R = new Random();

        public static Vector3F RandomSmall() {
            return new Vector3F() {
                X = (float)(R.NextDouble() - 0.5) * 10,
                Y = (float)(R.NextDouble() - 0.5),
                Z = (float)(R.NextDouble() - 0.5) * 10,
            };
        }

        public static Vector3F RandomBig() {
            return new Vector3F() {
                X = (float)(R.NextDouble() - 0.5) * 1000,
                Y = (float)(R.NextDouble() - 0.5) * 20,
                Z = (float)(R.NextDouble() - 0.5) * 1000,
            };
        }
    }
}