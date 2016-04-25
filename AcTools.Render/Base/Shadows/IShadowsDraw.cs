using AcTools.Render.Base.Cameras;

namespace AcTools.Render.Base.Shadows {
    public interface IShadowsDraw {
        void DrawSceneForShadows(DeviceContextHolder holder, ICamera camera);
    }
}