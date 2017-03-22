using System;
using System.Linq;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Base.PostEffects.AO {
    public class AssaoHelper : AoHelperBase {
        private EffectPpAssao _effect;
        private ShaderResourceView _dither;

        private TargetResourceTexture[] _depths;
        private TargetResourceTexture[] _pingPong;
        private TargetResourceTextureArray _finalResults;

        public override void OnInitialize(DeviceContextHolder holder) {
            base.OnInitialize(holder);

            // textures
            _depths = new[] {
                TargetResourceTexture.Create(Format.R16_Float),
                TargetResourceTexture.Create(Format.R16_Float),
                TargetResourceTexture.Create(Format.R16_Float),
                TargetResourceTexture.Create(Format.R16_Float),
            };

            _pingPong = new[] {
                TargetResourceTexture.Create(Format.R8G8_UNorm),
                TargetResourceTexture.Create(Format.R8G8_UNorm),
            };

            _finalResults = TargetResourceTextureArray.Create(Format.R8G8_UNorm, 4);

            // effect
            _effect = holder.GetEffect<EffectPpAssao>();
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
            
            // _effect.FxSampleDirections.Set(samplesKernel);

            _dither = holder.CreateTexture(4, 4, (x, y) => {
                var angle = (float)(2f * Math.PI * MathUtils.Random());
                var r = angle.Cos();
                var g = -angle.Sin();
                var b = (float)MathUtils.Random() * 0.01f;
                return new Color4(r, g, b);
            });

            _effect.FxDitherMap.SetResource(_dither);
        }

        public override void OnResize(DeviceContextHolder holder) {
            base.OnResize(holder);

            foreach (var depth in _depths) {
                depth.Resize(holder, holder.Width / 2, holder.Height / 2, null);
            }

            foreach (var depth in _pingPong) {
                depth.Resize(holder, holder.Width / 2, holder.Height / 2, null);
            }

            _finalResults.Resize(holder, holder.Width / 2, holder.Height / 2, null);
        }

        public override void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals, ICamera camera, RenderTargetView target) {
            base.Draw(holder, depth, normals, camera, target);
            holder.PrepareQuad(_effect.LayoutPT);

            holder.DeviceContext.ClearRenderTargetView(_depths[0].TargetView, default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_depths[1].TargetView, default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_depths[2].TargetView, default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_depths[3].TargetView, default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_pingPong[0].TargetView, default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_pingPong[1].TargetView, default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_finalResults.TargetViews[0], default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_finalResults.TargetViews[1], default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_finalResults.TargetViews[2], default(Color4));
            holder.DeviceContext.ClearRenderTargetView(_finalResults.TargetViews[3], default(Color4));

            // prepare depths
            _effect.Fx_DepthSource.SetResource(depth);
            holder.DeviceContext.Rasterizer.SetViewports(new Viewport(0, 0, holder.Width / 2f, holder.Height / 2f));
            holder.DeviceContext.OutputMerger.SetTargets(_depths.Select(x => x.TargetView).ToArray());
            _effect.TechPrepareDepth.DrawAllPasses(holder.DeviceContext, 6);

            // draw stuff
            holder.DeviceContext.OutputMerger.SetTargets(target);

            /*_effect.FxDepthMap.SetResource(depth);
            _effect.FxNormalMap.SetResource(normals);
        
            _effect.FxWorldViewProj.SetMatrix(camera.ViewProj);
            _effect.FxWorldViewProjInv.SetMatrix(camera.ViewProjInvert);*/
            
            _effect.Fx_ViewspaceDepthSource.SetResource(_depths[0].View);
            _effect.Fx_ViewspaceDepthSource1.SetResource(_depths[1].View);
            _effect.Fx_ViewspaceDepthSource2.SetResource(_depths[2].View);
            _effect.Fx_ViewspaceDepthSource3.SetResource(_depths[3].View);
            _effect.Fx_NormalmapSource.SetResource(normals);

            // assao
            var c = camera as BaseCamera;
            if (c == null) return;

            var proj = Matrix.PerspectiveFovLH(c.FovY, c.Aspect, c.NearZValue, c.FarZValue);
            var view = Matrix.LookAtLH(c.Position.FlipX(), (c.Position + c.Look).FlipX(), c.Up.FlipX());
            //var view = Matrix.LookAtRH(c.Position, c.Position + c.Look, c.Up);
            //var view = Matrix.LookAtLH(c.Position, c.Position + c.Look, c.Up);

            //_effect.FxProjectionMatrix.Set(proj);

            _effect.FxProj.SetMatrix(proj);


            float depthLinearizeMul = -proj.M43;           // float depthLinearizeMul = ( clipFar * clipNear ) / ( clipFar - clipNear );
            float depthLinearizeAdd = proj.M22;           // float depthLinearizeAdd = clipFar / ( clipFar - clipNear );
            // correct the handedness issue. need to make sure this below is correct, but I think it is.
            if (depthLinearizeMul * depthLinearizeAdd < 0) {
                depthLinearizeAdd = -depthLinearizeAdd;
            }

            float tanHalfFOVY = 1.0f / proj.M22;    // = tanf( drawContext.Camera.GetYFOV( ) * 0.5f );
            float tanHalfFOVX = 1.0F / proj.M11;    // = tanHalfFOVY * drawContext.Camera.GetAspect( );

            var settings = new {
                FadeOutFrom = 50f,
                FadeOutTo = 300f,
                HorizonAngleThreshold = 0.06f,
                Radius = 1.2f,
                QualityLevel = 2,
                AdaptiveQualityLimit = 0.45f,
                Sharpness = 0.98f,

                TemporalSupersamplingAngleOffset = 0f,
                TemporalSupersamplingRadiusOffset = 1f,

                DetailShadowStrength = 0.5f,
            };

            var halfSize = 0.5f;

            var consts = new EffectPpAssao.ASSAOConstants {
                ViewportPixelSize = new Vector2(1f / holder.Width, 1f / holder.Height),
                HalfViewportPixelSize = new Vector2(halfSize / (holder.Width + 1), halfSize / (holder.Height + 1)),

                Viewport2xPixelSize = new Vector2(2f / holder.Width, 2f / holder.Height),
                Viewport2xPixelSize_x_025 = new Vector2(0.5f / holder.Width, 0.5f / holder.Height),

                DepthUnpackConsts = new Vector2(depthLinearizeMul, depthLinearizeAdd),
                CameraTanHalfFOV = new Vector2(tanHalfFOVX, tanHalfFOVY),

                NDCToViewMul = new Vector2(tanHalfFOVX * 2.0f, tanHalfFOVY * -2.0f),
                NDCToViewAdd = new Vector2(tanHalfFOVX * -1.0f, tanHalfFOVY * 1.0f),

                EffectRadius = 1.2f,
                EffectShadowStrength = 1f * 4.3f,
                EffectShadowPow = 1.5f,
                EffectShadowClamp = 0.98f,
                EffectFadeOutMul = -1.0f / (settings.FadeOutTo - settings.FadeOutFrom),
                EffectFadeOutAdd = settings.FadeOutFrom / (settings.FadeOutTo - settings.FadeOutFrom) + 1.0f,
                EffectHorizonAngleThreshold = settings.HorizonAngleThreshold.Saturate(),

                DepthPrecisionOffsetMod = 0.9992f
            };
            
            float effectSamplingRadiusNearLimit = (settings.Radius * 1.2f);
            if (settings.QualityLevel == 0) {
                //consts.EffectShadowStrength     *= 0.9f;
                effectSamplingRadiusNearLimit *= 1.50f;
            }

            effectSamplingRadiusNearLimit /= tanHalfFOVY; // to keep the effect same regardless of FOV
            consts.EffectSamplingRadiusNearLimitRec = 1.0f / effectSamplingRadiusNearLimit;
            consts.AdaptiveSampleCountLimit = settings.AdaptiveQualityLimit;
            consts.NegRecEffectRadius = -1.0f / consts.EffectRadius;

            var pass = 0;
            consts.PerPassFullResCoordOffset = new Vector2(pass % 2, pass / 2);
            consts.PerPassFullResUVOffset = new Vector2(((pass % 2) - 0.0f) / holder.Width, ((pass / 2) - 0.0f) / holder.Height);

            consts.InvSharpness = (1.0f - settings.Sharpness).Clamp(0.0f, 1.0f);
            consts.PassIndex = pass;
            consts.QuarterResPixelSize = new Vector2(0.25f / (float)holder.Width, 0.25f / (float)holder.Height);

            float additionalAngleOffset = settings.TemporalSupersamplingAngleOffset;  // if using temporal supersampling approach (like "Progressive Rendering Using Multi-frame Sampling" from GPU Pro 7, etc.)
            float additionalRadiusScale = settings.TemporalSupersamplingRadiusOffset; // if using temporal supersampling approach (like "Progressive Rendering Using Multi-frame Sampling" from GPU Pro 7, etc.)
            const int subPassCount = 5;
            consts.PatternRotScaleMatrices = new Vector4[5];
            for (int subPass = 0; subPass < subPassCount; subPass++) {
                int a = pass;
                int b = subPass;

                var spmap = new int[] { 0, 1, 4, 3, 2 };
                b = spmap[subPass];

                float ca, sa;
                float angle0 = ((float)a + (float)b / (float)subPassCount) * (3.1415926535897932384626433832795f) * 0.5f;
                angle0 += additionalAngleOffset;

                ca = angle0.Cos();
                sa = angle0.Sin();

                float scale = 1.0f + (a - 1.5f + (b - (subPassCount - 1.0f) * 0.5f) / (float)subPassCount) * 0.07f;
                scale *= additionalRadiusScale;

                consts.PatternRotScaleMatrices[subPass] = new Vector4(scale * ca, scale * -sa, -scale * sa, -scale * ca);
            }

            consts.NormalsUnpackMul = 1.0f;
            consts.NormalsUnpackAdd = 0.0f;

            consts.DetailAOStrength = settings.DetailShadowStrength;
            consts.Dummy0 = 0.0f;

            //consts.NormalsWorldToViewspaceMatrix = Matrix.Transpose(view);
            consts.NormalsWorldToViewspaceMatrix = view;

            _effect.Fx_ASSAOConsts.Set(consts);


            _effect.FxProjInv.SetMatrix(Matrix.Invert(proj));
            //_effect.FxDitherScale.Set(holder.Width / 4);
            //_effect.FxRenderTargetResolution.Set(new Vector2(holder.Width, holder.Height));

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
            _effect.FxNormalsToViewSpace.SetMatrix(camera.View);
            // _effect.Fx

            _effect.TechAssao.DrawAllPasses(holder.DeviceContext, 6);
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _dither);
        }
    }
}