using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public interface IKn5Reader {
        void SkipMaterial();

        [NotNull]
        Kn5Material ReadMaterial();

        [NotNull]
        Kn5Node ReadNodeHierarchy();

        [NotNull]
        Kn5Node ReadNode();
    }
}