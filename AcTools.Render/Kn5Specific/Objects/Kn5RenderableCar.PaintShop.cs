using AcTools.Render.Base;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        public bool OverrideTexture(DeviceContextHolder device, string textureName, [CanBeNull] byte[] textureBytes) {
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

        public bool OverrideTexture(DeviceContextHolder device, string textureName, [CanBeNull] ShaderResourceView textureView, bool disposeLater) {
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