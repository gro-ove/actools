using System.IO;
using System.Text;
using SystemHalf;
using AcTools.Numerics;

namespace AcTools {
    public class ExtendedBinaryWriter : BinaryWriter {
        public ExtendedBinaryWriter(string filename) : base(File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {}
        public ExtendedBinaryWriter(Stream stream, bool leaveOpen) : base(stream, Encoding.UTF8, leaveOpen) {}

        public void Write(float[] values) {
            for (var i = 0; i < values.Length; i++) {
                Write(values[i]);
            }
        }

        public void Write(Vec2 v) {
            Write(v.X);
            Write(v.Y);
        }

        public void Write(Vec3 v) {
            Write(v.X);
            Write(v.Y);
            Write(v.Z);
        }

        public void Write(Vec4 v) {
            Write(v.X);
            Write(v.Y);
            Write(v.Z);
            Write(v.W);
        }

        public void Write(Quat v) {
            Write(v.X);
            Write(v.Y);
            Write(v.Z);
            Write(v.W);
        }

        public void Write(Mat4x4 v) {
            Write(v.M11);
            Write(v.M12);
            Write(v.M13);
            Write(v.M14);
            Write(v.M21);
            Write(v.M22);
            Write(v.M23);
            Write(v.M24);
            Write(v.M31);
            Write(v.M32);
            Write(v.M33);
            Write(v.M34);
            Write(v.M41);
            Write(v.M42);
            Write(v.M43);
            Write(v.M44);
        }

        public void WriteHalf(float value) {
            Write(new Half(value).Value);
        }

        public override void Write(string value) {
            var bytes = Encoding.UTF8.GetBytes(value);
            Write(bytes.Length);
            Write(bytes);
        }
    }
}