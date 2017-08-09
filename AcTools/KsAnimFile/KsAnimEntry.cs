using AcTools.Utils.Helpers;

namespace AcTools.KsAnimFile {
    public abstract class KsAnimEntryBase : IWithId {
        public string NodeName;
        public abstract int Size { get; }
        string IWithId<string>.Id => NodeName;
    }

    public class KsAnimEntryV1 : KsAnimEntryBase {
        public float[][] Matrices;
        public override int Size => Matrices.Length;
    }

    public class KsAnimEntryV2 : KsAnimEntryBase {
        public KsAnimKeyframe[] KeyFrames;
        public override int Size => KeyFrames.Length;
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