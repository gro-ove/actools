using System;
using System.IO;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.CustomShowroom {
    public static class Kn5Extension {
        private class ExistingKn5Textures : IKn5TextureProvider, IKn5TextureLoader {
            private readonly string _kn5;
            private string _textureName;
            private Func<int, Stream> _fn;

            public ExistingKn5Textures(string kn5) {
                _kn5 = kn5;
            }

            public void OnNewKn5(string kn5Filename) { }

            public void GetTexture(string textureName, Func<int, Stream> writer) {
                _textureName = textureName;
                _fn = writer;
                Kn5.FromFile(_kn5, this, SkippingMaterialLoader.Instance, SkippingNodeLoader.Instance);
                _textureName = null;
                _fn = null;
            }

            byte[] IKn5TextureLoader.LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
                if (textureName == _textureName) {
                    var s = _fn?.Invoke(textureSize);
                    if (s != null) {
                        reader.CopyTo(s, textureSize);
                        return null;
                    }
                }

                reader.Skip(textureSize);
                return null;
            }
        }

        public static async Task UpdateKn5(this Kn5 kn5, BaseRenderer renderer = null, CarSkinObject skin = null) {
            if (kn5.MaterialLoader != DefaultKn5MaterialLoader.Instance || kn5.NodeLoader != DefaultKn5NodeLoader.Instance) {
                throw new Exception("Can’t save KN5 loaded unusually");
            }

            var backup = kn5.OriginalFilename + ".backup";

            try {
                if (!File.Exists(backup)) {
                    FileUtils.HardLinkOrCopy(kn5.OriginalFilename, backup);
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            await Task.Run(() => {
                using (var f = FileUtils.RecycleOriginal(kn5.OriginalFilename)) {
                    try {
                        if (kn5.TextureLoader == DefaultKn5TextureLoader.Instance) {
                            kn5.Save(f.Filename);
                        } else {
                            Logging.Debug("Extra special mode for saving KN5s without textures loaded");
                            kn5.Save(f.Filename, new ExistingKn5Textures(kn5.OriginalFilename));
                        }
                    } catch {
                        FileUtils.TryToDelete(f.Filename);
                        throw;
                    }
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