namespace AcTools.KsAnimFile {
    public abstract class KsAnimEntryBase {
        public string NodeName;
    }

    public class KsAnimEntryV1 : KsAnimEntryBase {
        public float[][] Matrices;
    }

    public class KsAnimEntryV2 : KsAnimEntryBase {
        public KsAnimKeyframe[] KeyFrames;
    }

    public struct KsAnimKeyframe {
        // Quaternion (four values)
        public float[] Rotation;

        // 3D-vector
        public float[] Transition;

        // 3D-vector
        public float[] Scale;

        public KsAnimKeyframe(float[] rotation, float[] transition, float[] scale) {
            Rotation = rotation;
            Transition = transition;
            Scale = scale;
        }
    }
}