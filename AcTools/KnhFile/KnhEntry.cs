namespace AcTools.KnhFile {
    public class KnhEntry {
        // Quaternion (four values)
        public string Name;

        // Matrix
        public float[] Transformation;

        // Children
        public KnhEntry[] Children;

        public KnhEntry(string name, float[] transformation, KnhEntry[] children) {
            Name = name;
            Transformation = transformation;
            Children = children;
        }
    }
}