namespace AcTools.KsAnimFile {
    public class KsAnimEntry {
        public string NodeName;
        public KsAnimKeyframe[] KeyFrames;
    }

    public struct KsAnimKeyframe {
        // Quaternion (four values)
        public float[] Rotation;

        // 3D-vector
        public float[] Transformation;

        // 3D-vector
        public float[] Scale;

        public KsAnimKeyframe(float[] rotation, float[] transformation, float[] scale) {
            Rotation = rotation;
            Transformation = transformation;
            Scale = scale;
        }
    }
}