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
        IRenderableTexture GetTexture(DeviceContextHolder device, string textureName);

        bool OverrideTexture(DeviceContextHolder device, string textureName, [CanBeNull] byte[] textureBytes);
        bool OverrideTexture(DeviceContextHolder device, string textureName, [CanBeNull] ShaderResourceView textureView, bool disposeLater);
        void ClearProceduralOverrides();
    }

    public partial class Kn5RenderableCar : IPaintShopObject {
        public bool OverrideTexture(DeviceContextHolder device, string textureName, byte[] textureBytes) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return false;
            }

            var texture = _texturesProvider.GetTexture(device, textureName);
            texture.SetProceduralOverride(device, textureBytes);
            return texture.Exists ||
                    _crewMain?.OverrideTexture(device, textureName, textureBytes) == true ||
                    _crewTyres?.OverrideTexture(device, textureName, textureBytes) == true ||
                    _crewStuff?.OverrideTexture(device, textureName, textureBytes) == true ||
                    _driver?.OverrideTexture(device, textureName, textureBytes) == true;
        }

        public bool OverrideTexture(DeviceContextHolder device, string textureName, ShaderResourceView textureView, bool disposeLater) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return false;
            }

            var texture = _texturesProvider?.GetTexture(device, textureName);
            texture.SetProceduralOverride(device, textureView, disposeLater);
            return texture.Exists ||
                    _crewMain?.OverrideTexture(device, textureName, textureView, disposeLater) == true ||
                    _crewTyres?.OverrideTexture(device, textureName, textureView, disposeLater) == true ||
                    _crewStuff?.OverrideTexture(device, textureName, textureView, disposeLater) == true ||
                    _driver?.OverrideTexture(device, textureName, textureView, disposeLater) == true;
        }

        private IEnumerable<IRenderableTexture> GetTextures(DeviceContextHolder device, string textureName) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) yield break;
            }

            yield return _texturesProvider?.GetTexture(device, textureName);
            yield return _crewMain?.GetTexture(device, textureName);
            yield return _crewTyres?.GetTexture(device, textureName);
            yield return _crewStuff?.GetTexture(device, textureName);
            yield return _driver?.GetTexture(device, textureName);
        }

        public IRenderableTexture GetTexture(DeviceContextHolder device, string textureName) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return null;
            }

            return GetTextures(device, textureName).FirstOrDefault(x => x.Exists);
        }

        public void ClearProceduralOverrides() {
            foreach (var texture in _texturesProvider.GetExistingTextures()) {
                texture.SetProceduralOverride(null, null);
            }

            foreach (var extra in new[] {
                _crewMain, _crewTyres, _crewStuff, _driver
            }.NonNull()) {
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