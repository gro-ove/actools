using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Shaders;
using SlimDX;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class Kn5MaterialTrackMap : IRenderableMaterial {
        private readonly Vector3 _color;
        private EffectSpecialTrackMap _effect;

        internal Kn5MaterialTrackMap(Vector3 color) {
            AcToolsLogging.Write("COLOR:" + color);
            _color = color;
        }

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (_effect != null) return;
            _effect = contextHolder.GetEffect<EffectSpecialTrackMap>();
        }

        public void Refresh(IDeviceContextHolder contextHolder) {}

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return false;
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutP;
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            _effect.FxColor.Set(new Vector4(_color, 1f));
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechMain.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => false;

        public string Name => null;

        public void Dispose() { }
    }
}