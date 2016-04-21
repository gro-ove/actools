using System.Linq;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.PostEffects {
    public class HdrHelper : IRenderHelper {
        private const int DownsamplerAdaptationCycles = 4;

        private EffectPpHdr _effect;
        private TargetResourceTexture[] _textures;
        private TargetResourceTexture[] _averateColor;
        private TargetResourceTexture _newAverageColor;

        private BlurHelper _blurHelper;

        private TargetResourceTexture _bloomTexture, _tempTexture;

        public void Initialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpHdr>();
            _blurHelper = holder.GetHelper<BlurHelper>();

            _textures = Enumerable.Range(0, DownsamplerAdaptationCycles)
                          .Select(x => TargetResourceTexture.Create(Format.R16G16B16A16_Float))
                          .ToArray();

            _averateColor = Enumerable.Range(0, 2).Select(x => {
                var t = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
                t.Resize(holder, 1, 1);
                return t;
            }).ToArray();

            _newAverageColor = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _newAverageColor.Resize(holder, 1, 1);

            _bloomTexture = TargetResourceTexture.Create(Format.R11G11B10_Float);
            _tempTexture = TargetResourceTexture.Create(Format.R11G11B10_Float);
        }

        public void Resize(DeviceContextHolder holder, int width, int height) {
            var i = 4 * 3;
            foreach (var texture in _textures) {
                texture.Resize(holder, width / i, height / i);
                i *= 4;
            }

            _bloomTexture.Resize(holder, width, height);
            _tempTexture.Resize(holder, width, height);

            _blurHelper.Resize(holder, width, height);
        }

        private int _currentTexture;
        private TargetResourceTexture GetAdaptationTexture(bool inverse = false) {
            var r = _currentTexture;
            if (inverse) {
                _currentTexture = 1 - _currentTexture;
            }
            return _averateColor[r];
        }

        private void UpdateNewAverateColor(DeviceContextHolder holder, ShaderResourceView view) {
            for (var i = 0; i <= DownsamplerAdaptationCycles; i++) {
                var currentRt = i == DownsamplerAdaptationCycles ? _newAverageColor : _textures[i];

                holder.DeviceContext.Rasterizer.SetViewports(new Viewport(0, 0, currentRt.Width, currentRt.Height, 0.0f, 1.0f));
                holder.DeviceContext.OutputMerger.SetTargets(currentRt.TargetView);

                _effect.FxPixel.Set(new Vector2(1f / currentRt.Width, 1f / currentRt.Height));
                _effect.FxInputMap.SetResource(i == 0 ? view : _textures[i - 1].View);
                _effect.FxCropImage.Set(new Vector2(i == 0 ? 0.5f : 1f));
                _effect.TechDownsampling.DrawAllPasses(holder.DeviceContext, 6);
            }
        }

        private void UpdateAdaptation(DeviceContextHolder holder) {
            var adaptation = GetAdaptationTexture(true);
            holder.DeviceContext.OutputMerger.SetTargets(GetAdaptationTexture().TargetView);
            _effect.FxInputMap.SetResource(adaptation.View);
            _effect.FxBrightnessMap.SetResource(_newAverageColor.View);
            _effect.TechAdaptation.DrawAllPasses(holder.DeviceContext, 6);

        }

        private void UpdateBloom(DeviceContextHolder holder, ShaderResourceView view) {
            holder.DeviceContext.OutputMerger.SetTargets(_bloomTexture.TargetView);
            _effect.FxInputMap.SetResource(view);
            _effect.TechBloom.DrawAllPasses(holder.DeviceContext, 6);

            // blurring
            holder.DeviceContext.OutputMerger.SetTargets(_tempTexture.TargetView);
            _blurHelper.BlurHorizontally(holder, _bloomTexture.View, 5.0f);

            holder.DeviceContext.OutputMerger.SetTargets(_bloomTexture.TargetView);
            _blurHelper.BlurHorizontally(holder, _tempTexture.View, 1.0f);
        }

        public bool BloomDebug = false;

        public void Draw(DeviceContextHolder holder, ShaderResourceView view) {
            // preparation
            holder.SaveRenderTargetAndViewport();
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            // update bloom texture (bright areas)
            UpdateBloom(holder, view);

            // update those small texture
            UpdateNewAverateColor(holder, view);
            UpdateAdaptation(holder);

            // reset viewport
            holder.RestoreRenderTargetAndViewport();

            if (BloomDebug) {
                _effect.FxInputMap.SetResource(_bloomTexture.View);
                _effect.TechCopy.DrawAllPasses(holder.DeviceContext, 6);
                return;
            }

            _effect.FxInputMap.SetResource(view);
            _effect.FxBrightnessMap.SetResource(GetAdaptationTexture().View);
            _effect.FxBloomMap.SetResource(_bloomTexture.View);
            _effect.TechTonemap.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {
            foreach (var texture in _textures) {
                texture.Dispose();
            }

            foreach (var texture in _averateColor) {
                texture.Dispose();
            }

            _bloomTexture.Dispose();
            _newAverageColor.Dispose();
        }
    }
}
