using System;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificDeferred;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Wrapper;
using AcTools.Utils;

namespace AcManager.Tools.SemiGui {
    public enum CustomShowroomMode {
        Lite, Fancy
    }

    public static class CustomShowroomWrapper {
        public static async Task StartLiteAsync(string kn5, string skinId = null) {
            using (var renderer = await Task.Run(() => new ForwardKn5ObjectRenderer(kn5))) {
                renderer.UseMsaa = SettingsHolder.CustomShowroom.LiteUseMsaa;
                renderer.UseFxaa = SettingsHolder.CustomShowroom.LiteUseFxaa;
                renderer.UseBloom = SettingsHolder.CustomShowroom.LiteUseBloom;

                if (skinId != null) {
                    renderer.SelectSkin(skinId);
                }

                new LiteShowroomWrapper(renderer).Run();
            }
        }

        public static async Task StartFancyAsync(string kn5, string skinId = null, string showroomKn5 = null) {
            using (var renderer = await Task.Run(() => new Kn5ObjectRenderer(kn5, showroomKn5))) {
                renderer.UseFxaa = SettingsHolder.CustomShowroom.LiteUseFxaa;

                if (skinId != null) {
                    renderer.SelectSkin(skinId);
                }

                new FancyShowroomWrapper(renderer).Run();
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