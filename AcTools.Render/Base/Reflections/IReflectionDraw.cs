using AcTools.Render.Base.Cameras;

namespace AcTools.Render.Base.Reflections {
    public interface IReflectionDraw {
        void DrawSceneForReflection(DeviceContextHolder holder, ICamera camera);
    }
}