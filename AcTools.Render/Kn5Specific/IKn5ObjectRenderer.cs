using AcTools.Render.Base.Cameras;

namespace AcTools.Render.Kn5Specific {
    public interface IKn5ObjectRenderer {
        CameraOrbit CameraOrbit { get; }

        bool AutoRotate { get; set; }

        bool CarLightsEnabled { get; set; }

        void SelectPreviousSkin();

        void SelectNextSkin();
    }
}