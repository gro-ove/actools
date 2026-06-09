using System;
using System.IO;
using System.Threading.Tasks;
using AcTools.Render.Base;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX.Direct3D11;
using Debug = System.Diagnostics.Debug;

namespace AcTools.Render.Kn5Specific.Textures {
    public class RenderableTexture : IRenderableTexture {
        public string Name { get; }
        public bool Exists { get; set; }

        public bool IsDisposed { get; private set; }
        public bool IsOverrideDisabled { get; set; }

        public RenderableTexture(string name = null) {
            Name = name;
            Debug.WriteLine("[RenderableTexture] CREATED: " + name);
        }

        private ShaderResourceView _resource;

        public ShaderResourceView Resource {
            get => _proceduralOverride ?? (IsOverrideDisabled ? _resource : _override ?? _resource);
            internal set {
                if (Equals(_resource, value)) return;
                DisposeHelper.Dispose(ref _resource);
                _resource = value;
            }
        }

        private ShaderResourceView _override;

        public ShaderResourceView Override {
            get => _override;
            internal set {
                if (Equals(_override, value)) return;
                if (_override?.Disposed == false) { // wut? how does that happen?
                    DisposeHelper.Dispose(ref _override);
                }
                _override = value;
            }
        }

        private ShaderResourceView _proceduralOverride;
        private bool _disposeProceduralOverride;

        public ShaderResourceView ProceduralOverride {
            get => _proceduralOverride;
            private set {
                if (Equals(value, _proceduralOverride)) return;
                if (_disposeProceduralOverride) {
                    DisposeHelper.Dispose(ref _proceduralOverride);
                }
                _proceduralOverride = value;
            }
        }

        [CanBeNull]
        private ShaderResourceView LoadSafe([CanBeNull] Device device, byte[] bytes) {
            if (device == null) return null;
            try {
                GC.AddMemoryPressure(bytes.Length);
                return ShaderResourceView.FromMemory(device, bytes);
            } catch (Exception e) {
                AcToolsLogging.Write($"Texture {Name} damaged: {e}");
                return null;
            }
        }

        [CanBeNull]
        private ShaderResourceView LoadSafe(Device device, string filename) {
            if (device == null) return null;
            try {
                return ShaderResourceView.FromFile(device, filename);
            } catch (Exception e) {
                AcToolsLogging.Write($"Texture {Name} damaged: {e}");
                return null;
            }
        }

        public void SetProceduralOverride(IDeviceContextHolder holder, byte[] textureBytes) {
            ProceduralOverride = textureBytes == null ? null : LoadSafe(holder?.Device, textureBytes);
            _disposeProceduralOverride = true;
            holder?.RaiseTexturesUpdated();
        }

        public void SetProceduralOverride(IDeviceContextHolder holder, ShaderResourceView textureView, bool disposeLater) {
            ProceduralOverride = textureView;
            _disposeProceduralOverride = disposeLater;
            holder?.RaiseTexturesUpdated();
        }

        private int _resourceId, _overrideId;

        public void Load([CanBeNull] IDeviceContextHolder holder, string filename) {
            var id = ++_resourceId;
            var resource = LoadSafe(holder?.Device, filename);
            if (id != _resourceId) {
                resource?.Dispose();
                return;
            }

            Resource = resource;
            holder?.RaiseTexturesUpdated();
        }

        public void Load([CanBeNull] IDeviceContextHolder holder, byte[] data) {
            var id = ++_resourceId;
            var resource = LoadSafe(holder?.Device, data);
            if (id != _resourceId) {
                resource?.Dispose();
                return;
            }

            Resource = resource;
            holder?.RaiseTexturesUpdated();
        }

        public async Task LoadAsync([CanBeNull] IDeviceContextHolder holder, string filename) {
            var id = ++_resourceId;
            var resource = await Task.Run(() => LoadSafe(holder?.Device, filename));
            if (id != _resourceId) {
                resource?.Dispose();
                return;
            }

            Resource = resource;
            holder?.RaiseTexturesUpdated();
        }

        public async Task LoadAsync([CanBeNull] IDeviceContextHolder holder, byte[] data) {
            var id = ++_resourceId;
            var resource = await Task.Run(() => LoadSafe(holder?.Device, data));
            if (id != _resourceId) {
                resource?.Dispose();
                return;
            }

            Resource = resource;
            holder?.RaiseTexturesUpdated();
        }

        public async Task LoadOverrideAsync([CanBeNull] IDeviceContextHolder holder, string filename) {
            var id = ++_overrideId;

            if (filename == null || !File.Exists(filename)) {
                Override = null;
            } else {
                try {
                    var resource = await Task.Run(() => LoadSafe(holder?.Device, filename));
                    if (id != _overrideId) {
                        resource?.Dispose();
                        return;
                    }

                    Override = resource;
                } catch (Exception e) {
                    if (id != _overrideId) return;
                    AcToolsLogging.Write(e);
                    Override = null;
                }
            }

            holder?.RaiseTexturesUpdated();
        }

        public void LoadOverride([CanBeNull] IDeviceContextHolder holder, byte[] data) {
            var id = ++_overrideId;

            if (data == null) {
                Override = null;
            } else {
                try {
                    var resource = LoadSafe(holder?.Device, data);
                    if (id != _overrideId) {
                        resource?.Dispose();
                        return;
                    }

                    Override = resource;
                } catch (Exception e) {
                    if (id != _overrideId) return;
                    AcToolsLogging.Write(e);
                    Override = null;
                }
            }

            holder?.RaiseTexturesUpdated();
        }

        public async Task LoadOverrideAsync([CanBeNull] IDeviceContextHolder holder, [CanBeNull] byte[] data) {
            var id = ++_overrideId;

            if (data == null) {
                Override = null;
            } else {
                try {
                    var resource = await Task.Run(() => LoadSafe(holder?.Device, data));
                    if (id != _overrideId) {
                        resource?.Dispose();
                        return;
                    }

                    Override = resource;
                } catch (Exception e) {
                    if (id != _overrideId) return;
                    AcToolsLogging.Write(e);
                    Override = null;
                }
            }

            holder?.RaiseTexturesUpdated();
        }

        public void Dispose() {
            ++_overrideId;
            DisposeHelper.Dispose(ref _override);
            DisposeHelper.Dispose(ref _resource);
            Debug.WriteLine("[RenderableTexture] DISPOSED: " + Name);
            IsDisposed = true;
        }
    }
}
