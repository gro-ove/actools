using System;
using System.Collections.Generic;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.Materials {
    public interface IRenderableMaterial : IDisposable {
        void Initialize(IDeviceContextHolder contextHolder);

        bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode);

        void SetMatrices(Matrix objectTransform, ICamera camera);

        void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode);

        bool IsBlending { get; }
    }

    public interface ISkinnedMaterial : IRenderableMaterial {
        void SetBones(Matrix[] bones);
    }

    public interface IAmbientShadowMaterial : IRenderableMaterial {
        [CanBeNull]
        ShaderResourceView GetView(IDeviceContextHolder contextHolder);
    }
}
