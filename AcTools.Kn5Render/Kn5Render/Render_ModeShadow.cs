using System;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Kn5Render.Kn5Render.Effects;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Debug = System.Diagnostics.Debug;
using Resource = SlimDX.Direct3D11.Resource;

namespace AcTools.Kn5Render.Kn5Render {
    public partial class Render {
        public Vector3 CarSize {
            get {
                return _sceneBoundingBox.Maximum - _sceneBoundingBox.Minimum;
            }
        }

        private EffectShotBodyShadow _effectShotBodyShadow;
        private EffectShotWheelShadow _effectShotWheelShadow;

        public void ShotBodyShadow(string outputFile) {
            _width = 512;
            _height = 512;

            foreach (var obj in _objs.Where(obj => obj.Blen != null)) {
                obj.Blen.Dispose();
                obj.Blen = null;
            }

            ShotBodyShadow_SetCamera();

            DxResize();
            DrawFrame();

            var doubleSided = RasterizerState.FromDescription(CurrentDevice, new RasterizerStateDescription {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsFrontCounterclockwise = true,
                IsDepthClipEnabled = false
            });

            var tmpTexture = new Texture2D(CurrentDevice, new Texture2DDescription {
                Width = _width,
                Height = _height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            _effectShotBodyShadow = new EffectShotBodyShadow(CurrentDevice);

            var tmpRenderTarget = new RenderTargetView(CurrentDevice, tmpTexture);
            DrawPreviousBodyDepthTo(tmpRenderTarget, doubleSided);
            BlurBodyShadow(tmpTexture, 1);
            BlurSomething(tmpTexture, 5, 2f / AmbientBodyShadowSize.X, 2f / AmbientBodyShadowSize.Z);

            Resource.SaveTextureToFile(_context, tmpTexture, ImageFileFormat.Png, outputFile);

            _effectShotBodyShadow.Dispose();
            tmpRenderTarget.Dispose();
            tmpTexture.Dispose();
            doubleSided.Dispose();
        }

        void ShotBodyShadow_SetCamera() {
            var pos = -_objectPos;
            pos.Y = 0;
            _camera = new CameraOrtho { 
                Position = pos + new Vector3(0, -0.01f, 0),
                FarZ = CarSize.Y + 0.02f,
                Target = pos,
                Width = AmbientBodyShadowSize.X * 2,
                Height = AmbientBodyShadowSize.Z * 2 
            };
        }

        public void ShotWheelsShadow(params string[] outputFiles) {
            Debug.Assert(outputFiles.Length == 4);

            _width = 64;
            _height = 64;

            foreach (var obj in _objs.Where(obj => obj.Blen != null)) {
                obj.Blen.Dispose();
                obj.Blen = null;
            }

            var doubleSided = RasterizerState.FromDescription(CurrentDevice, new RasterizerStateDescription {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                IsFrontCounterclockwise = true,
                IsDepthClipEnabled = false
            });

            var tmpTexture = new Texture2D(CurrentDevice, new Texture2DDescription {
                Width = _width,
                Height = _height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            _effectShotWheelShadow = new EffectShotWheelShadow(CurrentDevice);
            var tmpRenderTarget = new RenderTargetView(CurrentDevice, tmpTexture);

            {
                Action<Kn5Node> temp = null;

                temp = x => {
                    if (x.Name.StartsWith("WHEEL_")) return;
                    if (x.NodeClass == Kn5NodeClass.Base) {
                        foreach (var child in x.Children) {
                            temp(child);
                        }
                    } else {
                        foreach (var obj in _objs.OfType<MeshObjectKn5>().Where(obj => obj.OriginalNode == x)) {
                            obj.Visible = false;
                        }
                    }
                };

                temp(_kn5.RootNode);
            }

            var wheels = new[]{ _wheelLfPos, _wheelRfPos, _wheelLrPos, _wheelRrPos };
            for (var i = 0; i < wheels.Length; i++) {
                ShotWheelShadow_SetCamera(wheels[i]);

                DxResize();
                DrawFrame();

                DrawPreviousWheelDepthTo(tmpRenderTarget, doubleSided);
                BlurWheelShadow(tmpTexture, 1);
                BlurSomething(tmpTexture, 3, 0.4f);

                Resource.SaveTextureToFile(_context, tmpTexture, ImageFileFormat.Png, outputFiles[i]);
            }

            _effectShotWheelShadow.Dispose();
            tmpRenderTarget.Dispose();
            tmpTexture.Dispose();
            doubleSided.Dispose();
        }

        void ShotWheelShadow_SetCamera(Vector3 wheelPos) {
            var pos = wheelPos - _objectPos;
            pos.Y = 0;
            _camera = new CameraOrtho { 
                Position = pos + new Vector3(0, -0.01f, 0),
                FarZ = AmbientWheelShadowSize.X / 2 + 0.02f,
                Target = pos,
                Width = AmbientWheelShadowSize.X * 2,
                Height = AmbientWheelShadowSize.Z * 2 
            };
        }

        void DrawPreviousBodyDepthTo(RenderTargetView target, RasterizerState rast = null) {
            _effectScreen.FxTexel.Set(new Vector2(2.5f / _width, 2.5f / _height));
            using (var res = new ShaderResourceView(CurrentDevice, _depthStencilTexture,
                                                 new ShaderResourceViewDescription {
                                                     Format = Format.R24_UNorm_X8_Typeless,
                                                     Dimension = ShaderResourceViewDimension.Texture2DMultisampled,
                                                     MipLevels = 1,
                                                     MostDetailedMip = 0
                                                 })) {
                DrawSomethingFromTo(res, target, _effectShotBodyShadow.LayoutPT, _effectShotBodyShadow.FxInputImage,
                        _effectShotBodyShadow.TechCreateBodyShadow, rast);
            }
        }

        void DrawPreviousWheelDepthTo(RenderTargetView target, RasterizerState rast = null) {
            _effectScreen.FxTexel.Set(new Vector2(2.5f / _width, 2.5f / _height));
            using (var res = new ShaderResourceView(CurrentDevice, _depthStencilTexture,
                                                 new ShaderResourceViewDescription {
                                                     Format = Format.R24_UNorm_X8_Typeless,
                                                     Dimension = ShaderResourceViewDimension.Texture2DMultisampled,
                                                     MipLevels = 1,
                                                     MostDetailedMip = 0
                                                 })) {
                DrawSomethingFromTo(res, target, _effectShotWheelShadow.LayoutPT, _effectShotWheelShadow.FxInputImage, 
                        _effectShotWheelShadow.TechCreateWheelShadow, rast);
            }
        }

        void BlurBodyShadow(Texture2D texture, int blurCount) {
            BlurSomething(texture, blurCount, _effectShotBodyShadow.LayoutPT, _effectShotBodyShadow.FxInputImage, _effectShotBodyShadow.FxTexel,
                    _effectShotBodyShadow.TechHorzBodyShadowBlur, _effectShotBodyShadow.TechVertBodyShadowBlur);
        }

        void BlurWheelShadow(Texture2D texture, int blurCount) {
            BlurSomething(texture, blurCount, _effectShotWheelShadow.LayoutPT, _effectShotWheelShadow.FxInputImage, _effectShotWheelShadow.FxTexel,
                    _effectShotWheelShadow.TechHorzWheelShadowBlur, _effectShotWheelShadow.TechVertWheelShadowBlur, 1.0f);
        }
    }
}
