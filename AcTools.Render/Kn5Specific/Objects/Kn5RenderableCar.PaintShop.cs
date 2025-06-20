using System.Collections.Generic;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Objects {
    public interface IPaintShopObject {
        [CanBeNull]
        IRenderableTexture GetTexture([NotNull] DeviceContextHolder device, [CanBeNull] string textureName);

        bool OverrideTexture([NotNull] DeviceContextHolder device, [CanBeNull] string textureName, [CanBeNull] byte[] textureBytes);
        bool OverrideTexture([NotNull] DeviceContextHolder device, [CanBeNull] string textureName, [CanBeNull] ShaderResourceView textureView, bool disposeLater);
        void ClearProceduralOverrides();
    }

    public partial class Kn5RenderableCar : IPaintShopObject {
        [NotNull]
        private IEnumerable<IPaintShopObject> GetChildrenPaintShopObjects() {
            return new[] {
                _crewMain,
                _crewTyres,
                _crewStuff,
                _driver
            }.NonNull();
        }

        public bool OverrideTexture(DeviceContextHolder device, string textureName, byte[] textureBytes) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return false;
            }

            var texture = _texturesProvider.GetTexture(device, textureName);
            texture.SetProceduralOverride(device, textureBytes);
            return texture.Exists ||
                    GetChildrenPaintShopObjects().Select(x => x.OverrideTexture(device, textureName, textureBytes)).FirstOrDefault();
        }

        public bool OverrideTexture(DeviceContextHolder device, string textureName, ShaderResourceView textureView, bool disposeLater) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return false;
            }

            var texture = _texturesProvider?.GetTexture(device, textureName);
            if (texture == null) {
                return false;
            }
            texture.SetProceduralOverride(device, textureView, disposeLater);
            return texture.Exists ||
                    GetChildrenPaintShopObjects().Select(x => x.OverrideTexture(device, textureName, textureView, disposeLater)).FirstOrDefault();
        }

        public IRenderableTexture GetTexture(DeviceContextHolder device, string textureName) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return null;
            }

            return GetChildrenPaintShopObjects().Select(x => x.GetTexture(device, textureName))
                                                .Prepend(_texturesProvider?.GetTexture(device, textureName)).FirstOrDefault(x => x.Exists);
        }

        public void ClearProceduralOverrides() {
            foreach (var texture in _texturesProvider.GetExistingTextures()) {
                texture.SetProceduralOverride(null, null);
            }

            foreach (var extra in GetChildrenPaintShopObjects()) {
                extra.ClearProceduralOverrides();
            }
        }

        public void SetCurrentSkinActive(bool active) {
            foreach (var texture in _texturesProvider.GetExistingTextures()) {
                texture.IsOverrideDisabled = !active;
            }
        }
    }
}