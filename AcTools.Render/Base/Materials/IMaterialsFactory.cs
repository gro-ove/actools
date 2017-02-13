using JetBrains.Annotations;

namespace AcTools.Render.Base.Materials {
    public interface IMaterialsFactory {
        [NotNull]
        IRenderableMaterial CreateMaterial(object key);
    }
}