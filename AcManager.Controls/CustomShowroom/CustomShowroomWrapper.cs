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
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Temporary;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Controls.CustomShowroom {
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
                        ExtraModelProvider.Initialize();

                        var toolsRenderer = await Task.Run(() => SettingsHolder.CustomShowroom.UseOldLiteShowroom ?
                                new ToolsKn5ObjectRenderer(description) {
                                    VisibleUi = false,
                                    UseSprite = false,
                                    UseMsaa = SettingsHolder.CustomShowroom.LiteUseMsaa,
                                    UseFxaa = SettingsHolder.CustomShowroom.LiteUseFxaa,
                                    UseBloom = SettingsHolder.CustomShowroom.LiteUseBloom
                                } :
                                new DarkKn5ObjectRenderer(description) {
                                    VisibleUi = false,
                                    UseSprite = false
                                });
                        wrapper = new LiteShowroomWrapperWithTools(toolsRenderer, carObject, skinId);
                        renderer = toolsRenderer;
                    } else {
                        Logging.Warning($"Can’t find CarObject for “{carDirectory}”");
                        Logging.Warning($"Found location: “{carObject?.Location ?? @"NULL"}”");

                        renderer = await Task.Run(() => SettingsHolder.CustomShowroom.UseOldLiteShowroom ?
                                new ForwardKn5ObjectRenderer(description) {
                                    UseMsaa = SettingsHolder.CustomShowroom.LiteUseMsaa,
                                    UseFxaa = SettingsHolder.CustomShowroom.LiteUseFxaa,
                                    UseBloom = SettingsHolder.CustomShowroom.LiteUseBloom
                                } :
                                new DarkKn5ObjectRenderer(description) {
                                    FlatMirror = true,
                                    VisibleUi = true,
                                    UseSprite = true
                                });

                        wrapper = new LiteShowroomWrapper(renderer);

                        if (skinId != null) {
                            renderer.SelectSkin(skinId);
                        }
                    }

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

        public static Task StartAsync(string kn5, string skinId = null) {
            return StartLiteAsync(kn5, skinId);
        }

        public static Task StartAsync(CarObject car, CarSkinObject skin = null) {
            return StartAsync(FileUtils.GetMainCarFilename(car.Location, car.AcdData), skin?.Id);
        }
    }
}