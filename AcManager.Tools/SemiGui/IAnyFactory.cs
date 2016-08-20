using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public interface IAnyFactory<out T> {
        [NotNull]
        T Create();
    }
}