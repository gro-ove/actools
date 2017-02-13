using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificDeferred;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Temporary;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Controls.CustomShowroom {
    public enum CustomShowroomMode {
        Lite, Fancy
    }

    public static class CustomShowroomWrapper {
        private static bool IsSameDirectories(string a, string b) {
            try {
                var f = Directory.GetFiles(a);
                var r = Directory.GetFiles(b).Select(Path.GetFileName).ToList();
                return f.Length == r.Count && f.Select(Path.GetFileName).All(x => r.Contains(x));
            } catch (Exception) {
                return false;
            }
        }

        private static BaseFormWrapper _last;
        private static bool _starting;

        private static void SetProperties(BaseKn5FormWrapper wrapper, IKn5ObjectRenderer renderer) {
            if (!SettingsHolder.CustomShowroom.SmartCameraPivot) {
                wrapper.AutoAdjustTargetOnReset = false;
                renderer.AutoAdjustTarget = false;
            }

            wrapper.InvertMouseButtons = SettingsHolder.CustomShowroom.AlternativeControlScheme;
        }

        public static async Task StartLiteAsync(string kn5, string skinId = null) {
            if (_starting) return;
            _starting = true;

            _last?.Stop();
            _last = null;

            ForwardKn5ObjectRenderer renderer = null;
            Logging.Write("Custom Showroom: Magick.NET IsSupported=" + ImageUtils.IsMagickSupported);
            RenderLogging.Initialize(Logging.Filename, true);

            try {
                var carDirectory = Path.GetDirectoryName(kn5);
                if (Path.GetFileName(Path.GetDirectoryName(carDirectory)) == @"..") {
                    carDirectory = Path.GetDirectoryName(Path.GetDirectoryName(carDirectory));
                }

                var carObject = CarsManager.Instance.GetById(Path.GetFileName(carDirectory) ?? "");
                var toolboxMode = IsSameDirectories(carObject?.Location, carDirectory);

                LiteShowroomWrapper wrapper;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report(ControlsStrings.CustomShowroom_Loading);

                    var description = new CarDescription(kn5, carDirectory, carObject?.AcdData);
                    if (toolboxMode) {
                        renderer = await Task.Run(() => new DarkKn5ObjectRenderer(description) {
                            UseMsaa = false,
                            UseSsaa = false,
                            FlatMirror = true,
                            VisibleUi = false,
                            UseSprite = false,
                            /*BackgroundColor = ColorExtension.FromHsb(MathUtils.Random(0d, 360d),  1d, 0.8).ToColor(),
                            AmbientDown = ColorExtension.FromHsb(MathUtils.Random(0d, 360d),  1d, 0.8).ToColor(),
                            AmbientUp = ColorExtension.FromHsb(MathUtils.Random(0d, 360d),  1d, 0.8).ToColor(),*/
                        });
                        wrapper = new LiteShowroomWrapperWithTools((ToolsKn5ObjectRenderer)renderer, carObject, skinId);
                    } else {
                        Logging.Warning($"Can’t find CarObject for “{carDirectory}”");
                        Logging.Warning($"Found location: “{carObject?.Location ?? @"NULL"}”");

                        renderer = await Task.Run(() => new ForwardKn5ObjectRenderer(description));
                        wrapper = new LiteShowroomWrapper(renderer);

                        if (skinId != null) {
                            renderer.SelectSkin(skinId);
                        }
                    }

                    renderer.UseMsaa = SettingsHolder.CustomShowroom.LiteUseMsaa;
                    renderer.UseFxaa = SettingsHolder.CustomShowroom.LiteUseFxaa;
                    renderer.UseBloom = SettingsHolder.CustomShowroom.LiteUseBloom;

                    _last = wrapper;
                    SetProperties(wrapper, renderer);

                    wrapper.Form.Icon = AppIconService.GetAppIcon();
                }

                wrapper.Run(() => _starting = false);
                GC.Collect();
            } catch (Exception e) {
                NonfatalError.Notify(ControlsStrings.CustomShowroom_CannotStart, e);
            } finally {
                renderer?.Dispose();
                _last = null;
                _starting = false;
            }
        }

        public static async Task StartFancyAsync(string kn5, string skinId = null, string showroomKn5 = null) {
            if (_starting) return;
            _starting = true;

            _last?.Stop();
            _last = null;

            Kn5ObjectRenderer renderer = null;

            try {
                FancyShowroomWrapper wrapper;
                using (var waiting = new WaitingDialog()) {
                    waiting.Report(ControlsStrings.CustomShowroom_Loading);

                    renderer = await Task.Run(() => new Kn5ObjectRenderer(kn5, showroomKn5));
                    renderer.UseFxaa = SettingsHolder.CustomShowroom.LiteUseFxaa;

                    wrapper = new FancyShowroomWrapper(renderer);
                    if (skinId != null) {
                        renderer.SelectSkin(skinId);
                    }

                    _last = wrapper;
                    SetProperties(wrapper, renderer);

                    wrapper.Form.Icon = AppIconService.GetAppIcon();
                }

                wrapper.Run(() => _starting = false);
            } catch (Exception e) {
                NonfatalError.Notify(ControlsStrings.CustomShowroom_CannotStart, e);
            } finally {
                renderer?.Dispose();
                _last = null;
            }
        } 

        public static Task StartAsync(CustomShowroomMode mode, string kn5, string skinId = null) {
            switch (mode) {
                case CustomShowroomMode.Lite:
                    return StartLiteAsync(kn5, skinId);

                case CustomShowroomMode.Fancy:
                    var showroomId = SettingsHolder.CustomShowroom.ShowroomId;
                    return StartFancyAsync(kn5, skinId, showroomId == null ? null : ShowroomsManager.Instance.GetById(showroomId)?.Kn5Filename);

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public static Task StartAsync(string kn5, string skinId = null) {
            var key = SettingsHolder.CustomShowroom.LiteByDefault;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                key = !key;
            }
            return StartAsync(key ? CustomShowroomMode.Lite : CustomShowroomMode.Fancy, kn5, skinId);
        }

        public static Task StartAsync(CarObject car, CarSkinObject skin = null) {
            return StartAsync(FileUtils.GetMainCarFilename(car.Location), skin?.Id);
        }

        public static Task StartAsync(CustomShowroomMode mode, CarObject car, CarSkinObject skin = null) {
            return StartAsync(mode, FileUtils.GetMainCarFilename(car.Location), skin?.Id);
        }
    }
}