using System;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Base.PostEffects {
    public class SmaaHelper : IRenderHelper {
        public static bool IsSupported { get; internal set; }

        //private EffectPpSmaa _effect;

        private ShaderResourceView _areasTexMap, _searchTexMap;

        public void OnInitialize(DeviceContextHolder holder) {
            throw new NotImplementedException();
            /*_effect = holder.GetEffect<EffectPpSmaa>();

            _areasTexMap = ShaderResourceView.FromMemory(holder.Device, SmaaResources.AreaTexDX10);
            _searchTexMap = ShaderResourceView.FromMemory(holder.Device, SmaaResources.SearchTex);*/
        }

        public void OnResize(DeviceContextHolder holder) {}

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, 
                TargetResourceTexture temporaryEdges, TargetResourceTexture temporaryBlending) {
            throw new NotImplementedException();

            /*holder.PrepareQuad(_effect.LayoutPT);
            holder.DeviceContext.OutputMerger.BlendState = null;

            // edges
            holder.DeviceContext.OutputMerger.SetTargets(temporaryEdges.TargetView);
            holder.DeviceContext.ClearRenderTargetView(temporaryEdges.TargetView, new Color4(0f, 0f, 0f, 0f));

            _effect.FxScreenSizeSpec.Set(new Vector4(1f / holder.Width, 1f / holder.Height, holder.Width, holder.Height));
            _effect.FxInputMap.SetResource(view);

            _effect.TechSmaa.DrawAllPasses(holder.DeviceContext, 6);

            // blending
            holder.DeviceContext.OutputMerger.SetTargets(temporaryBlending.TargetView);
            holder.DeviceContext.ClearRenderTargetView(temporaryBlending.TargetView, new Color4(0f, 0f, 0f, 0f));

            _effect.FxEdgesMap.SetResource(temporaryEdges.View);
            _effect.FxAreaTexMap.SetResource(_areasTexMap);
            _effect.FxSearchTexMap.SetResource(_searchTexMap);

            _effect.TechSmaaB.DrawAllPasses(holder.DeviceContext, 6);

            // final part
            holder.DeviceContext.OutputMerger.SetTargets(target);
            _effect.FxBlendMap.SetResource(temporaryBlending.View);
            _effect.TechSmaaN.DrawAllPasses(holder.DeviceContext, 6);*/
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _areasTexMap);
            DisposeHelper.Dispose(ref _searchTexMap);
        }
    }
}