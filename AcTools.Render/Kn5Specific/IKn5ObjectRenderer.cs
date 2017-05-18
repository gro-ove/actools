using AcTools.Render.Base.Cameras;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific {
    public interface IKn5ObjectRenderer {
        [NotNull]
        CameraBase Camera { get; }

        [CanBeNull]
        CameraOrbit CameraOrbit { get; }

        [CanBeNull]
        FpsCamera FpsCamera { get; }

        bool AutoRotate { get; set; }

        bool LockCamera { get; }

        bool AutoAdjustTarget { get; set; }

        bool UseFpsCamera { get; set; }

        bool VisibleUi { get; set; }

        bool CarLightsEnabled { get; set; }

        bool CarBrakeLightsEnabled { get; set; }

        void SelectPreviousSkin();

        void SelectNextSkin();

        void SelectSkin([CanBeNull] string skinId);

        void ResetCamera();

        void ChangeCameraFov(float newFovY);
    }
}