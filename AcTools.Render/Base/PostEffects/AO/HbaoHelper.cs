using AcTools.Render.Base.Cameras;
using AcTools.Render.Shaders;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects.AO {
    public class HbaoHelper : AoHelperBase {
        private EffectPpHbao _effect;

        public override void OnInitialize(DeviceContextHolder holder) {
            base.OnInitialize(holder);
            _effect = holder.GetEffect<EffectPpHbao>();
        }

        public override void Draw(DeviceContextHolder holder, ShaderResourceView depth, ShaderResourceView normals, ICamera camera, RenderTargetView target,
                float aoPower, float aoRadiusMultiplier, bool accumulationMode) {
            SetBlurEffectTextures(depth, normals);
            SetRandomValues(holder, _effect.FxNoiseMap, _effect.FxNoiseSize, accumulationMode);

            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxDepthMap.SetResource(depth);
            // _effect.FxNormalMap.SetResource(normals);

            if (!(camera is CameraBase c)) return;

            var near = c.NearZValue;
            var far = c.FarZValue;

            _effect.FxAoPower.Set(aoPower);
            _effect.FxNearFar.Set(new Vector2((near - far) / (far * near), far / (far * near)));

            var valueY = (0.5f * c.FovY).Tan();
            var valueX = valueY / holder.Height * holder.Width;
            _effect.FxValues.Set(new Vector4(
                    valueX,
                    valueY,
                    3f * aoRadiusMultiplier * valueX / holder.Width,
                    3f * aoRadiusMultiplier * valueY / holder.Height * holder.Width / holder.Height));

            // var view = Matrix.LookAtLH(c.Position, (c.Position + c.Look), c.Up);
            // var proj = Matrix.PerspectiveFovLH(c.FovY, c.Aspect, c.NearZValue, c.FarZValue);
            // _effect.FxNormalsToViewSpace.SetMatrix(Matrix.Invert(Matrix.Transpose(proj)));
            _effect.TechHbao.DrawAllPasses(holder.DeviceContext, 6);
        }
    }
}