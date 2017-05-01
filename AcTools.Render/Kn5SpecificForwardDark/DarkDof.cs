using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Buffer = SlimDX.Direct3D11.Buffer;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkDof : IDisposable {
        private int _maxSize = 960;
        private float _sizeMultipler;

        [NotNull]
        public readonly TargetResourceTexture BufferScene, BufferDownsampleColor, BufferScatterBokeh;
        private ShaderResourceView _bokehBaseView;

        public DarkDof() {
            BufferScene = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            BufferDownsampleColor = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
            BufferScatterBokeh = TargetResourceTexture.Create(Format.R16G16B16A16_Float);
        }

        public void Dispose() {
            BufferDownsampleColor.Dispose();
            BufferScatterBokeh.Dispose();
            DisposeHelper.Dispose(ref _bokehBaseView);
        }

        private EffectPpDof _effect;
        private CopyHelper _copyHelper;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectPpDof>();
            _copyHelper = holder.GetHelper<CopyHelper>();
            _bokehBaseView = ShaderResourceView.FromMemory(holder.Device, Resources.Bokeh);
        }

        public int? MaxSize {
            get { return _maxSize; }
            set { _maxSize = value ?? 960; }
        }

        private Buffer _buffer;
        private int _width, _height;

        public void Prepare(DeviceContextHolder holder, int width, int height) {
            if (_effect == null) {
                OnInitialize(holder);
            }

            _width = width;
            _height = height;
            BufferScene.Resize(holder, width, height, null);

            var minMultipler = Math.Min((float)_maxSize / width, (float)_maxSize / height);
            _sizeMultipler = minMultipler;

            var smallWidth = (int)(minMultipler * width);
            var smallHeight = (int)(minMultipler * height);

            if (BufferDownsampleColor.Resize(holder, smallWidth, smallHeight, null)) {
                BufferScatterBokeh.Resize(holder, smallWidth * 2, smallHeight, null);

                DisposeHelper.Dispose(ref _buffer);
                var numQuads = smallWidth * smallHeight;
                var numIndices = numQuads * 6;

                using (var indices = new DataStream(sizeof(int) * numIndices, true, true)) {
                    for (var i = 0; i < numQuads; i++) {
                        indices.Write(i * 4 + 0);
                        indices.Write(i * 4 + 1);
                        indices.Write(i * 4 + 2);

                        indices.Write(i * 4 + 1);
                        indices.Write(i * 4 + 3);
                        indices.Write(i * 4 + 2);
                    }

                    indices.Position = 0;
                    _buffer = new Buffer(holder.Device, indices, sizeof(int) * numIndices, ResourceUsage.Default, BindFlags.IndexBuffer,
                            CpuAccessFlags.None, ResourceOptionFlags.None, 0);
                }
            }

            holder.DeviceContext.ClearRenderTargetView(BufferDownsampleColor.TargetView, (Color4)new Vector4(0));
            holder.DeviceContext.ClearRenderTargetView(BufferScatterBokeh.TargetView, (Color4)new Vector4(0));
            holder.DeviceContext.ClearRenderTargetView(BufferScene.TargetView, (Color4)new Vector4(0));
        }

        public float FocusPlane = 6f;
        public float DofCoCScale = 30f;
        public float DofCoCLimit = 24f;

        public void Process(DeviceContextHolder holder, ShaderResourceView depthView, ShaderResourceView colorView, ICamera camera, RenderTargetView target,
                bool colorIsTarget) {
            // let’s save original if color is the same as target
            if (colorIsTarget) {
                _copyHelper.Draw(holder, colorView, BufferScene.TargetView);
                _effect.FxInputTexture.SetResource(BufferScene.View);
            } else {
                _effect.FxInputTexture.SetResource(colorView);
            }

            // common input values
            _effect.FxInputTextureDepth.SetResource(depthView);
            _effect.FxZNear.Set(camera.NearZValue);
            _effect.FxZFar.Set(camera.FarZValue);

            _effect.FxFocusPlane.Set(FocusPlane);
            _effect.FxDofCoCScale.Set(DofCoCScale);
            _effect.FxCoCLimit.Set(DofCoCLimit);

            // more complicated-to-calculate params
            var focusPlane = FocusPlane + camera.NearZValue;
            var cocCalculationsBias = DofCoCScale * (1f - focusPlane / camera.NearZValue);
            var cocCalculationsScale = DofCoCScale * focusPlane * (camera.FarZValue - camera.NearZValue) / (camera.FarZValue * camera.NearZValue);
            _effect.FxCocScaleBias.Set(new Vector4(cocCalculationsScale, cocCalculationsBias, 0f, 0f));
            _effect.FxScreenSize.Set(new Vector4(_width, _height, 1f / _width, 1f / _height));
            _effect.FxScreenSizeHalfRes.Set(new Vector4(_width * _sizeMultipler, _height * _sizeMultipler,
                    1f / _sizeMultipler / _width, 1f / _sizeMultipler / _height));

            // common preparation
            holder.PrepareQuad(_effect.LayoutPT);

            // prepare downsamplecolor buffer
            holder.SaveRenderTargetAndViewport();
            holder.DeviceContext.OutputMerger.SetTargets(BufferDownsampleColor.TargetView);
            holder.DeviceContext.Rasterizer.SetViewports(BufferDownsampleColor.Viewport);
            _effect.TechDownsampleColorCoC.DrawAllPasses(holder.DeviceContext, 6);
            holder.RestoreRenderTargetAndViewport();

            // wut
            _effect.FxInputTextureDownscaledColor.SetResource(BufferDownsampleColor.View);
            _effect.FxInputTextureBokenBase.SetResource(_bokehBaseView);

            holder.SaveRenderTargetAndViewport();
            holder.DeviceContext.OutputMerger.SetTargets(BufferScatterBokeh.TargetView);
            holder.DeviceContext.Rasterizer.SetViewports(BufferScatterBokeh.Viewport);
            holder.DeviceContext.OutputMerger.BlendState = holder.States.AddState;

            _effect.TechBokehSprite.GetPassByIndex(0).Apply(holder.DeviceContext);
            RenderFullscreenGrid(holder.DeviceContext, BufferDownsampleColor.Width * BufferDownsampleColor.Height);

            holder.RestoreRenderTargetAndViewport();
            holder.DeviceContext.OutputMerger.BlendState = null;

            // set new buffer as input
            _effect.FxInputTextureBokeh.SetResource(BufferScatterBokeh.View);

            // result
            holder.PrepareQuad(_effect.LayoutPT);
            holder.DeviceContext.OutputMerger.SetTargets(target);
            _effect.TechResolveBokeh.DrawAllPasses(holder.DeviceContext, 6);

            // _copyHelper.Draw(holder, BufferScatterBokeh.View, target);
        }

        private void RenderFullscreenGrid(DeviceContext context, int quadCount) {
            context.InputAssembler.InputLayout = null;
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding());
            context.InputAssembler.SetIndexBuffer(_buffer, Format.R32_UInt, 0);
            context.DrawIndexed(6 * quadCount, 0, 0);
        }
    }
}