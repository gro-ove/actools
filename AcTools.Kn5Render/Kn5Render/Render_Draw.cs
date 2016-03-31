using System.ComponentModel;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Kn5Render.Kn5Render {
    public partial class Render {
        bool _drawPrepared;

        void DxResize() {
            if (_renderTarget != null) {
                _renderTarget.Dispose();
                _renderTargetTexture.Dispose();
                _depthState.Dispose();
                _depthStencilView.Dispose();
                _depthStencilTexture.Dispose();
            }

            _renderTargetTexture = new Texture2D(CurrentDevice, new Texture2DDescription {
                Width = _width,
                Height = _height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = _sampleDesc,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
            _renderTarget = new RenderTargetView(CurrentDevice, _renderTargetTexture);
            _renderTargetResource = new ShaderResourceView(CurrentDevice, _renderTargetTexture);

            _depthStencilTexture = new Texture2D(CurrentDevice, new Texture2DDescription {
                Width = _width,
                Height = _height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R24G8_Typeless,
                SampleDescription = _sampleDesc,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            }) { DebugName = "DepthStencilBuffer" };
            _depthStencilView = new DepthStencilView(CurrentDevice, _depthStencilTexture, new DepthStencilViewDescription {
                Flags = DepthStencilViewFlags.None,
                Format = Format.D24_UNorm_S8_UInt,
                Dimension = DepthStencilViewDimension.Texture2DMultisampled,
                MipSlice = 0
            });

            _depthState = DepthStencilState.FromDescription(CurrentDevice, new DepthStencilStateDescription() {
                IsDepthEnabled = true,
                IsStencilEnabled = false,
                DepthWriteMask = DepthWriteMask.All,
                DepthComparison = Comparison.Less,
            });

            _viewport = new Viewport(0, 0, _width, _height, 0.0f, 1.0f);
            _context.Rasterizer.SetViewports(_viewport);

            _camera.SetLens(AspectRatio);
        }

        public void DrawPrepare() {
            _dirLight.Direction.Normalize();
            _context.OutputMerger.BlendFactor = new Color4(0, 0, 0);
            _drawPrepared = true;
        }

        public void DrawFrame() {
            if (!_drawPrepared) {
                DrawPrepare();

                _effectTest.FxDirLight.Set(_dirLight);
                _effectTest.FxCubeMap.SetResource(_reflectionCubemap);
                _context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            }

            if (_camera.Moved) {
                var cam = _camera as CameraOrbit;
                if (cam != null && _form == null) {
                    cam.Target = new Vector3(cam.Target.X, cam.Target.Y, MathF.Sin(cam.Alpha)*0.62f);
                }

                _camera.UpdateViewMatrix();
                _effectTest.FxEyePosW.Set(_camera.Position);
                _camera.Moved = false;
            }

            _context.OutputMerger.DepthStencilState = _depthState;
            _context.OutputMerger.SetTargets(_depthStencilView, _renderTarget);

            _context.ClearRenderTargetView(_renderTarget, _wireframeMode ? _wireframeBackgroundColor : _backgroundColor);
            _context.ClearDepthStencilView(_depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);

            _context.Rasterizer.State = _wireframeMode ? _wireframeRs : null;

            foreach (var obj in _objs) {
                if (!obj.Visible) continue;

                _context.InputAssembler.InputLayout = obj is MeshObjectKn5 ? _effectTest.LayoutPNT :  _effectTest.LayoutPT;
                _context.InputAssembler.SetVertexBuffers(0, obj.VertexBufferBinding);

                _context.InputAssembler.SetIndexBuffer(obj.Ib, Format.R16_UInt, 0);
                _effectTest.FxDiffuseMap.SetResource(obj.Tex ?? (obj.TexName != null && _textures.ContainsKey(obj.TexName) ? _textures[obj.TexName] : null));
                _effectTest.FxDetailsMap.SetResource(obj.DetailTexName != null && _textures.ContainsKey(obj.DetailTexName)
                    ? _textures[obj.DetailTexName] : null);
                _effectTest.FxNormalMap.SetResource(obj.NormalTexName != null && _textures.ContainsKey(obj.NormalTexName)
                    ? _textures[obj.NormalTexName] : null);
                _effectTest.FxMapMap.SetResource(obj.MapTexName != null && _textures.ContainsKey(obj.MapTexName)
                    ? _textures[obj.MapTexName] : null);

                _effectTest.FxWorldViewProj.SetMatrix(obj.Transform * _camera.ViewProj);
                _effectTest.FxWorldInvTranspose.SetMatrix(obj.InvTransform);
                _effectTest.FxWorld.SetMatrix(obj.Transform);

                if (obj.Rast != null && !_wireframeMode) {
                    _context.Rasterizer.State = obj.Rast;
                } else {
                    _context.Rasterizer.State = _wireframeMode ? _wireframeRs : null;
                }

                if (obj.Blen != null) {
                    _context.OutputMerger.BlendState = obj.Blen;
                    _context.OutputMerger.BlendSampleMask = ~0;
                } else if (obj.Mat.MinAlpha < 1.0f) {
                    _context.OutputMerger.BlendState = _transparentBlendState;
                    _context.OutputMerger.BlendSampleMask = ~0;
                } else {
                    _context.OutputMerger.BlendState = null;
                }

                if (obj is MeshObjectKn5) {
                    _effectTest.FxMaterial.Set(obj.Mat);
                    _effectTest.TechCar.DrawAllPasses(_context, obj.IndCount);
                } else if (obj is MeshObjectShadow) {
                    _effectTest.TechShadow.DrawAllPasses(_context, obj.IndCount);
                } else {
                    _effectTest.TechSimple.DrawAllPasses(_context, obj.IndCount);
                }
            }
        }

        public void DrawSomethingFromTo(ShaderResourceView res, RenderTargetView target, InputLayout layout, EffectResourceVariable variable, EffectTechnique tech,
                RasterizerState rast = null) {
            _context.OutputMerger.SetTargets(target);
            _context.ClearRenderTargetView(target, _backgroundColor);

            _context.OutputMerger.DepthStencilState = null;
            _context.OutputMerger.BlendState = null;
            _context.Rasterizer.State = rast;

            _context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            _context.InputAssembler.InputLayout = layout;
            _context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_squareBuffers.VertexBuffer, VerticePT.Stride, 0));
            _context.InputAssembler.SetIndexBuffer(_squareBuffers.IndexBuffer, Format.R16_UInt, 0);
            variable.SetResource(res);

            for (var p = 0; p < tech.Description.PassCount; p++) {
                tech.GetPassByIndex(p).Apply(_context);
                _context.DrawIndexed(6, 0, 0);
            }
        }

        public void DrawSomethingFromTo(ShaderResourceView res, RenderTargetView target, EffectTechnique tech, RasterizerState rast = null) {
            DrawSomethingFromTo(res, target, _effectScreen.LayoutPT, _effectScreen.FxInputImage, tech, rast);
        }

        void DrawPreviousFrameTo(RenderTargetView target) {
            DrawSomethingFromTo(_renderTargetResource, target, _effectScreen.TechCopy);
        }

        void BlurSomething(Texture2D texture, int blurCount, InputLayout layout, EffectResourceVariable variable, EffectVectorVariable fxTexel,
                EffectTechnique horz, EffectTechnique vert, float multiplerX, float multiplerY) {
            var temporary = new Texture2D(CurrentDevice, texture.Description);
            var sra = new ShaderResourceView(CurrentDevice, texture);
            var srb = new ShaderResourceView(CurrentDevice, temporary);
            var rta = new RenderTargetView(CurrentDevice, texture);
            var rtb = new RenderTargetView(CurrentDevice, temporary);

            fxTexel.Set(new Vector2(multiplerX / _width, multiplerY / _height));

            for (var i = 0; i < blurCount; i++) {
                DrawSomethingFromTo(sra, rtb, layout, variable, horz);
                DrawSomethingFromTo(srb, rta, layout, variable, vert);
            }

            sra.Dispose();
            srb.Dispose();
            rta.Dispose();
            rtb.Dispose();
        }

        void BlurSomething(Texture2D texture, int blurCount, InputLayout layout, EffectResourceVariable variable, EffectVectorVariable fxTexel,
                    EffectTechnique horz, EffectTechnique vert, float multipler = 2.0f) {
            BlurSomething(texture, blurCount, layout, variable, fxTexel, horz, vert, multipler, multipler);
        }

        void BlurSomething(Texture2D texture, int blurCount, float multiplerX, float multiplerY) {
            BlurSomething(texture, blurCount, _effectScreen.LayoutPT, _effectScreen.FxInputImage, _effectScreen.FxTexel,
                    _effectScreen.TechHorzBlur, _effectScreen.TechVertBlur, multiplerX, multiplerY);
        }

        void BlurSomething(Texture2D texture, int blurCount, float multipler = 2.0f) {
            BlurSomething(texture, blurCount, _effectScreen.LayoutPT, _effectScreen.FxInputImage, _effectScreen.FxTexel,
                    _effectScreen.TechHorzBlur, _effectScreen.TechVertBlur, multipler, multipler);
        }
    }
}
