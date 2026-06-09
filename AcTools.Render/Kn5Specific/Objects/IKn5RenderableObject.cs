using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public interface IKn5RenderableObject : IRenderableObject {
        [NotNull]
        Kn5Node OriginalNode { get; }

        Matrix ModelMatrixInverted { set; }

        bool IsInitialized { get; }

        void SetMirrorMode([NotNull] IDeviceContextHolder holder, bool enabled);

        void SetDebugMode([NotNull] IDeviceContextHolder holder, bool enabled);

        void SetTransparent(bool? isTransparent);
        
        void SetCastShadows(bool? castShadows);

        AcDynamicMaterialParams DynamicMaterialParams { get; }

        int TrianglesCount { get; }
    }
}