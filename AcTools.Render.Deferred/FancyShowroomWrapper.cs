using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Deferred.Kn5Specific;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using SlimDX;

namespace AcTools.Render.Deferred {
    public class FancyShowroomWrapper : BaseKn5FormWrapper {
        private readonly Kn5ObjectRenderer _renderer;

        public FancyShowroomWrapper(Kn5ObjectRenderer renderer) : base(renderer, "Custom Showroom", 1600, 900) {
            _renderer = renderer;
        }

        protected override void OnTick(object sender, TickEventArgs args) {
            base.OnTick(sender, args);

            if (_renderer.Sun == null) return;
            if (User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu)) { 
                if (User32.IsKeyPressed(Keys.Left)) {
                    _renderer.AutoRotateSun = false;
                    _renderer.Sun.Direction = _renderer.Sun.Direction + new Vector3(-args.DeltaTime, 0f, 0f);
                }

                if (User32.IsKeyPressed(Keys.Right)) {
                    _renderer.AutoRotateSun = false;
                    _renderer.Sun.Direction = _renderer.Sun.Direction + new Vector3(args.DeltaTime, 0f, 0f);
                }

                if (User32.IsKeyPressed(Keys.Up)) {
                    _renderer.AutoRotateSun = false;
                    _renderer.Sun.Direction = _renderer.Sun.Direction + new Vector3(0f, 0f, args.DeltaTime);
                }

                if (User32.IsKeyPressed(Keys.Down)) {
                    _renderer.AutoRotateSun = false;
                    _renderer.Sun.Direction = _renderer.Sun.Direction + new Vector3(0f, 0f, -args.DeltaTime);
                }
            }
        }

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            base.OnKeyUp(sender, args);
            if (args.Handled) return;

            switch (args.KeyCode) {
                case Keys.F1:
                    _renderer.Mode = DeferredShadingRenderer.RenderingMode.Result;
                    break;

                case Keys.F2:
                    _renderer.Mode = DeferredShadingRenderer.RenderingMode.DebugGBuffer;
                    break;

                case Keys.F3:
                    _renderer.Mode = DeferredShadingRenderer.RenderingMode.DebugPostEffects;
                    break;

                case Keys.F4:
                    _renderer.Mode = DeferredShadingRenderer.RenderingMode.DebugLighting;
                    break;

                case Keys.F5:
                    _renderer.Mode = DeferredShadingRenderer.RenderingMode.DebugLocalReflections;
                    break;

                case Keys.F6:
                    _renderer.Mode = DeferredShadingRenderer.RenderingMode.WithoutTransparent;
                    break;

                case Keys.F8:
                    double multipler;
                    bool downscale; 
                    
                    {
                        // hold shift to disable downsampling
                        // hold ctrl to render scene in 8x resolution
                        // hold alt to render scene in 4x resolution
                        // hold both for 16x (but any videocard most likely won’t be able to pull this off)

                        var ctrlPressed = User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey);
                        var altPressed = User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu);
                        var shiftPressed = User32.IsKeyPressed(Keys.LShiftKey) || User32.IsKeyPressed(Keys.RShiftKey);

                        downscale = !shiftPressed;

                        if (ctrlPressed) {
                            multipler = altPressed ? 1d : 8d;
                        } else if (altPressed) {
                            multipler = 4d;
                        } else {
                            multipler = 2d;
                        }
                    }

                    _renderer.KeepFxaaWhileShooting = !downscale;
                    var image = _renderer.Shot(multipler, 1d, true);
                    var directory = FileUtils.GetDocumentsScreensDirectory();
                    FileUtils.EnsureDirectoryExists(directory);
                    var filename = Path.Combine(directory, $"__custom_showroom_{DateTime.Now.ToUnixTimestamp()}.jpg");
                    if (downscale) {
                        image = image.HighQualityResize(new Size(image.Width / 2, image.Height / 2));
                    }

                    image.Save(filename);
                    break;

                case Keys.A:
                    _renderer.AddLight();
                    break;

                case Keys.R:
                    if (args.Control) {
                        _renderer.UseCubemapReflections = !_renderer.UseCubemapReflections;
                    } else {
                        _renderer.UseLocalReflections = !_renderer.UseLocalReflections;
                    }
                    break;

                case Keys.S:
                    if (args.Control) {
                        _renderer.UseShadows = true;
                        _renderer.UseShadowsFilter = false;
                        _renderer.UseDebugShadows = !_renderer.UseDebugShadows;
                    } else if (args.Alt) {
                        _renderer.UseShadows = true;
                        _renderer.UseDebugShadows = false;
                        _renderer.UseShadowsFilter = !_renderer.UseShadowsFilter;
                    } else {
                        _renderer.UseDebugShadows = false;
                        _renderer.UseShadows = !_renderer.UseShadows;
                    }
                    break;

                case Keys.B:
                    if (args.Control) {
                        var helper = _renderer.DeviceContextHolder.GetHelper<HdrHelper>();
                        helper.BloomDebug = !helper.BloomDebug;
                    } else {
                        _renderer.BlurLocalReflections = !_renderer.BlurLocalReflections;
                    }
                    break;

                case Keys.L:
                    if (args.Control) {
                        _renderer.LimitLightsThroughGlass = !_renderer.LimitLightsThroughGlass;
                    }
                    break;

                case Keys.N:
                    _renderer.Daylight = !_renderer.Daylight;
                    break;

                case Keys.F:
                    if (args.Control) {
                        _renderer.UseFxaa = true;
                        _renderer.UseExperimentalSmaa = false;
                        _renderer.UseExperimentalFxaa = !_renderer.UseExperimentalFxaa;
                    } else if (args.Alt && SmaaHelper.IsSupported) {
                        _renderer.UseFxaa = true;
                        _renderer.UseExperimentalFxaa = false;
                        _renderer.UseExperimentalSmaa = !_renderer.UseExperimentalSmaa;
                    } else {
                        _renderer.UseExperimentalFxaa = false;
                        _renderer.UseFxaa = !_renderer.UseFxaa;
                    }
                    break;

                case Keys.Space:
                    if (args.Control) {
                        _renderer.AutoRotateSun = !_renderer.AutoRotateSun;
                    }
                    break;

                case Keys.Z:
                    _renderer.RemoveLight();
                    break;

                default:
                    return;
            }

            args.Handled = true;
        }
    }
}