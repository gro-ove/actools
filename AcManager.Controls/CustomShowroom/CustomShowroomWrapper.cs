using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificDeferred;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Wrapper;
using AcTools.Utils;

namespace AcManager.Controls.CustomShowroom {
    public enum CustomShowroomMode {
        Lite, Fancy
    }

    public static class CustomShowroomWrapper {
        private static Uri _iconUri;
        private static Icon _iconValue;

        public static void SetDefaultIcon(Uri iconUri) {
            _iconUri = iconUri;
        }

        private static Icon Icon {
            get {
                if (_iconValue != null || _iconUri == null) return _iconValue;
                using (var iconStream = Application.GetResourceStream(_iconUri)?.Stream) {
                    _iconValue = iconStream == null ? null : new Icon(iconStream);
                }
                return _iconValue;
            }
        }

        public static async Task StartLiteAsync(string kn5, string skinId = null) {
            using (var renderer = await Task.Run(() => new ForwardKn5ObjectRenderer(kn5))) {
                renderer.UseMsaa = SettingsHolder.CustomShowroom.LiteUseMsaa;
                renderer.UseFxaa = SettingsHolder.CustomShowroom.LiteUseFxaa;
                renderer.UseBloom = SettingsHolder.CustomShowroom.LiteUseBloom;

                var carDirectory = Path.GetDirectoryName(kn5);
                var carObject = CarsManager.Instance.GetById(Path.GetFileName(carDirectory) ?? "");
                if (string.Equals(carObject?.Location, carDirectory, StringComparison.OrdinalIgnoreCase)) {
                    var wrapper = new LiteShowroomWrapperWithTools(renderer, carObject, skinId);
                    wrapper.Form.Icon = Icon;
                    wrapper.Run();
                } else {
                    if (skinId != null) {
                        renderer.SelectSkin(skinId);
                    }

                    var wrapper = new LiteShowroomWrapper(renderer);
                    wrapper.Form.Icon = Icon;
                    wrapper.Run();
                }
            }
        }

        public static async Task StartFancyAsync(string kn5, string skinId = null, string showroomKn5 = null) {
            using (var renderer = await Task.Run(() => new Kn5ObjectRenderer(kn5, showroomKn5))) {
                renderer.UseFxaa = SettingsHolder.CustomShowroom.LiteUseFxaa;

                if (skinId != null) {
                    renderer.SelectSkin(skinId);
                }

                var wrapper = new FancyShowroomWrapper(renderer);
                wrapper.Form.Icon = Icon;
                wrapper.Run();
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
            return StartAsync(SettingsHolder.CustomShowroom.LiteByDefault ? CustomShowroomMode.Lite : CustomShowroomMode.Fancy, kn5, skinId);
        }

        public static Task StartAsync(CarObject car, CarSkinObject skin = null) {
            return StartAsync(FileUtils.GetMainCarFilename(car.Location), skin?.Id);
        }
    }
}