using System;
using System.IO;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.CustomShowroom {
    public static class Kn5Extension {
        public static async Task UpdateKn5(this Kn5 kn5, BaseRenderer renderer = null, CarSkinObject skin = null) {
            var backup = kn5.OriginalFilename + ".backup";
            try {
                if (!File.Exists(backup)) {
                    File.Copy(kn5.OriginalFilename, backup);
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            await Task.Run(() => {
                using (var f = FileUtils.RecycleOriginal(kn5.OriginalFilename)) {
                    kn5.Save(f.Filename);
                }
            });

            if (renderer != null) {
                var car = skin == null ? null : CarsManager.Instance.GetById(skin.CarId);
                var slot = (renderer as ToolsKn5ObjectRenderer)?.MainSlot;
                if (car != null && slot != null) {
                    slot.SetCar(CarDescription.FromKn5(kn5, car.Location, car.AcdData));
                    slot.SelectSkin(skin.Id);
                }
            }
        }
    }
}