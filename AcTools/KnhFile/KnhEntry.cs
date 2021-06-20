using AcTools.Numerics;

namespace AcTools.KnhFile {
    public class KnhEntry {
        // Quaternion (four values)
        public string Name;

        // Matrix
        public Mat4x4 Transformation;

        // Children
        public KnhEntry[] Children;

        public KnhEntry(string name, Mat4x4 transformation, KnhEntry[] children) {
            Name = name;
            Transformation = transformation;
            Children = children;
        }
    }
}