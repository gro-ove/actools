using System;
using System.Linq;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
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

        public void OnInitialize(DeviceContextHolder holder) {
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

            _bloomTexture = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            _tempTexture = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
        }

        public void OnResize(DeviceContextHolder holder) {
            var width = holder.Width;
            var height = holder.Height;

            var i = 4 * 3;
            foreach (var texture in _textures) {
                texture.Resize(holder, Math.Max(width / i, 1), Math.Max(height / i, 1));
                i *= 4;
            }

            _bloomTexture.Resize(holder, width, height);
            _tempTexture.Resize(holder, width, height);
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
            _blurHelper.Blur(holder, _bloomTexture, _tempTexture, 1f, 2);
        }

        public bool BloomDebug = false;

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, TargetResourceTexture temporary) {
            // preparation
            holder.SaveRenderTargetAndViewport();
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);

            // update those small texture
            UpdateNewAverateColor(holder, view);
            UpdateAdaptation(holder);

            holder.RestoreRenderTargetAndViewport();
            holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
            holder.DeviceContext.ClearRenderTargetView(temporary.TargetView, BaseRenderer.ColorTransparent);

            _effect.FxInputMap.SetResource(view);
            _effect.FxBrightnessMap.SetResource(GetAdaptationTexture().View);
            _effect.TechTonemap.DrawAllPasses(holder.DeviceContext, 6);

            // update bloom texture (bright areas)
            UpdateBloom(holder, temporary.View);

            if (BloomDebug) {
                holder.RestoreRenderTargetAndViewport();
                _effect.FxInputMap.SetResource(_bloomTexture.View);
                _effect.TechCopy.DrawAllPasses(holder.DeviceContext, 6);
                return;
            }

            // reset viewport
            holder.RestoreRenderTargetAndViewport();

            _effect.FxInputMap.SetResource(temporary.View);
            _effect.FxBloomMap.SetResource(_bloomTexture.View);
            _effect.TechCombine.DrawAllPasses(holder.DeviceContext, 6);
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
