namespace AcTools.Render.Base.Materials {
    public interface IMaterialsFactory {
        IRenderableMaterial CreateMaterial(object key);
    }
}