using AcTools.Numerics;
using AcTools.Utils.Helpers;

namespace AcTools.KsAnimFile {
    public abstract class KsAnimEntryBase : IWithId {
        public string NodeName;
        public abstract int Size { get; }
        string IWithId<string>.Id => NodeName;
    }

    public class KsAnimEntryV1 : KsAnimEntryBase {
        public Mat4x4[] Matrices;
        public override int Size => Matrices.Length;
    }

    public class KsAnimEntryV2 : KsAnimEntryBase {
        public KsAnimKeyframe[] KeyFrames;
        public override int Size => KeyFrames.Length;
    }

    public struct KsAnimKeyframe {
        // Quaternion (four values)
        public Quat Rotation;

        // 3D-vector
        public Vec3 Transition;

        // 3D-vector
        public Vec3 Scale;

        public KsAnimKeyframe(Quat rotation, Vec3 transition, Vec3 scale) {
            Rotation = rotation;
            Transition = transition;
            Scale = scale;
        }
    }
}