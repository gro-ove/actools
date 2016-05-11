using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using SlimDX;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class Kn5MaterialUv : IRenderableMaterial {
        internal static string Filter { get; set; }

        private EffectSpecialUv _effect;
        private readonly string[] _textures;

        internal Kn5MaterialUv(Kn5Material material) {
            _textures = material?.TextureMappings.Where(x => x.Name != "txDetail"
                    && x.Name != "txNormalDetail").Select(x => x.Texture).ToArray() ?? new string[0];
        }

        public void Initialize(DeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectSpecialUv>();
        }

        public bool Prepare(DeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return false;
            if (!_textures.Contains(Filter)) return false;
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPNTG;
            contextHolder.DeviceContext.OutputMerger.BlendState = IsBlending ? contextHolder.TransparentBlendState : null;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {}

        public void Draw(DeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechMain.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => false;

        public void Dispose() {}
    }
}