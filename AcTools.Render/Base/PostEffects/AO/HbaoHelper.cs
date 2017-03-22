using System;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects.AO {
    public class HbaoHelper : AoHelperBase {
        private EffectPpHbao _effect;
        private ShaderResourceView _dither;

        public override void OnInitialize(DeviceContextHolder holder) {
            base.OnInitialize(holder);

            _effect = holder.GetEffect<EffectPpHbao>();
            _effect.FxNoiseMap.SetResource(holder.GetRandomTexture(4, 4));

            var samplesKernel = new Vector4[EffectPpSsao.SampleCount];
            for (var i = 0; i < samplesKernel.Length; i++) {
                samplesKernel[i].X = MathUtils.Random(-1f, 1f);
                samplesKernel[i].Y = MathUtils.Random(-1f, 1f);
                //samplesKernel[i].Y = MathUtils.Random(0f, 1f);
                samplesKernel[i].Normalize();
                //samplesKernel[i] *= 0.01f;

                //var scale = (float)i / samplesKernel.Length;
                //scale = MathUtils.Lerp(0.1f, 1f, scale * scale);
                //samplesKernel[i] *= scale;
            }
            
            _effect.FxSampleDirections.Set(samplesKernel);

            _dither = holder.CreateTexture(16, 16, (x, y) => {
                var angle = (float)(2f * Math.PI * MathUtils.Random());
                var r = angle.Cos();
                var g = -angle.Sin();
                //var b = (float)MathUtils.Random() * 1f;
                var b = 0.2f;
                return new Color4(r, g, b);
            });

            /*_dither = holder.CreateTexture(16, 16, (x, y) => {
                var angle = (float)(2f * Math.PI * MathUtils.Random());
                var r = MathUtils.Random(-1f, 1f);
                var g = MathUtils.Random(-1f, 1f);
                //var b = (float)MathUtils.Random() * 1f;
                var b = 0.2f;
                return new Color4(r, g, MathUtils.Random(-1f, 1f), b);
            });*/

            _effect.FxDitherMap.SetResource(_dither);
        }

        public void Prepare(DeviceContextHolder holder, ShaderResourceView diffuse) {
            _effect.FxFirstStepMap.SetResource(diffuse);
        }

        public override void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals, ICamera camera, RenderTargetView target) {
            base.Draw(holder, depth, normals, camera, target);

            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDepthMap.SetResource(depth);
            _effect.FxNormalMap.SetResource(normals);

            // hbao
            var c = camera as BaseCamera;
            if (c == null) return;

            //_effect.FxWorldViewProjInv.SetMatrix(c.ViewProjInvert);
            //_effect.FxView.SetMatrix(c.View);

            var proj = c.Proj;// Matrix.PerspectiveFovLH(c.FovY, c.Aspect, c.NearZValue, c.FarZValue);
            var view = c.View;// Matrix.LookAtRH(c.Position.FlipX(), (c.Position + c.Look).FlipX(), c.Up.FlipX());
            //view = Matrix.LookAtLH(c.Position, (c.Position + c.Look), c.Up);

            _effect.FxWorldViewProjInv.SetMatrix(Matrix.Invert(view * proj));
            _effect.FxView.SetMatrix(view);

            _effect.FxProjectionMatrix.SetMatrix(proj);
            _effect.FxProj.SetMatrix(proj);
            _effect.FxProjT.SetMatrix(Matrix.Transpose(proj));
            _effect.FxProjInv.SetMatrix(Matrix.Invert(proj));
            _effect.FxDitherScale.Set(holder.Width / 16);
            _effect.FxRenderTargetResolution.Set(new Vector2(holder.Width, holder.Height));

            var corners = new [] {
                new Vector4(-1f, -1f, 0f, 1f),
                new Vector4(1f, -1f, 0f, 1f),
                new Vector4(1f, 1f, 0f, 1f),
                new Vector4(-1f, 1f, 0f, 1f)
            };

            var inverseProjection = Matrix.Invert(proj);
            for (var i = 0; i < 4; ++i) {
                corners[i] = Vector4.Transform(corners[i], inverseProjection);
                corners[i] /= corners[i].W;
                corners[i] /= corners[i].Z;
            }

            _effect.FxViewFrustumVectors.Set(corners);
            //_effect.FxNormalsToViewSpace.SetMatrix(Matrix.Invert(Matrix.Transpose(camera.ViewProj)));
            //_effect.FxNormalsToViewSpace.SetMatrix(Matrix.Invert(Matrix.Transpose(camera.Proj)));
            //_effect.FxNormalsToViewSpace.SetMatrix(view);
            //_effect.FxNormalsToViewSpace.SetMatrix(Matrix.Invert(Matrix.Transpose(view)));
            // _effect.Fx

            _effect.TechHbao.DrawAllPasses(holder.DeviceContext, 6);
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _dither);
        }
    }
}