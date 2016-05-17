using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Temporary;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public class TexturesProvider : IDisposable {
        public static bool OptionOverrideAsync = false;

        private class Kn5Entry {
            public string Filename;
            public Kn5 Kn5;
        }

        private readonly List<Kn5Entry> _kn5Entries = new List<Kn5Entry>();

        public void SetKn5(string filename, Kn5 kn5) {
            _kn5Entries.Add(new Kn5Entry {
                Filename = filename,
                Kn5 = kn5
            });
        }

        private readonly Dictionary<string, IOverridedTextureProvider> _overridedProviders =
                new Dictionary<string, IOverridedTextureProvider>();

        public void SetOverridedProvider(string filename, IOverridedTextureProvider provider) {
            if (provider == null) {
                _overridedProviders.Remove(filename);
            } else {
                _overridedProviders[filename] = provider;
            }
        }

        private readonly Dictionary<string, IRenderableTexture> _textures = new Dictionary<string, IRenderableTexture>();

        public IEnumerable<IRenderableTexture> GetFor(Kn5 kn5) {
            var keyPrefix = kn5.OriginalFilename + "//";
            return _textures.Where(x => x.Key.StartsWith(keyPrefix)).Select(key => key.Value);
        }

        public Task UpdateOverridesForAsync(Kn5 kn5, string textureName, DeviceContextHolder contextHolder) {
            IOverridedTextureProvider provider;
            if (!_overridedProviders.TryGetValue(kn5.OriginalFilename, out provider)) return Task.Delay(0);

            var keyPrefix = kn5.OriginalFilename + "//";
            if (OptionOverrideAsync) {
                return Task.WhenAll(_textures.Where(x => x.Key.StartsWith(keyPrefix))
                                            .Select(key => key.Value)
                                            .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                            .OfType<RenderableTexture>().Select(async texture => {
                                                var overrided = await provider.GetOverridedDataAsync(texture.Name);
                                                if (overrided != null) {
                                                    await texture.LoadOverrideAsync(overrided, contextHolder.Device);
                                                } else {
                                                    texture.Override = null;
                                                }
                                            }));
            }


            foreach (var texture in _textures.Where(x => x.Key.StartsWith(keyPrefix))
                                    .Select(key => key.Value)
                                    .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                    .OfType<RenderableTexture>()) {
                try {
                    var overrided = provider.GetOverridedData(texture.Name);
                    if (overrided != null) {
                        texture.LoadOverride(overrided, contextHolder.Device);
                    } else {
                        texture.Override = null;
                    }
                } catch (Exception e) {
                    Logging.Warning("Can't load override texture: " + e);
                    texture.Override = null;
                }
            }

            return Task.Delay(0);
        }

        public Task UpdateOverridesForAsync(Kn5 kn5, DeviceContextHolder contextHolder) {
            return UpdateOverridesForAsync(kn5, null, contextHolder);
        }

        public void Dispose() {
            _textures.DisposeEverything();
            _kn5Entries.Clear();
            _overridedProviders.Clear();
        }

        public void DisposeFor(Kn5 kn5) {
            var keyPrefix = kn5.OriginalFilename + "//";
            var keys = _textures.Keys.Where(x => x.StartsWith(keyPrefix)).ToList();
            foreach (var key in keys) {
                _textures[key].Dispose();
                _textures.Remove(key);
            }

            var entry = _kn5Entries.FirstOrDefault(x => x.Kn5 == kn5);
            if (entry == null) return;
            _kn5Entries.Remove(entry);
        }

        [NotNull]
        public IRenderableTexture GetTexture([NotNull] string kn5Filename, string textureName, DeviceContextHolder contextHolder) {
            if (kn5Filename == null) throw new ArgumentNullException(nameof(kn5Filename));

            var key = kn5Filename + "//" + textureName;
            IRenderableTexture texture;
            if (_textures.TryGetValue(key, out texture)) return texture;

            var result = new RenderableTexture(textureName) { Resource = null };
            IOverridedTextureProvider provider;
            if (_overridedProviders.TryGetValue(kn5Filename, out provider)) {
                LoadOverrideAsync(result, textureName, provider, contextHolder);
            }

            var kn5 = _kn5Entries.Where(x => string.Equals(x.Filename, kn5Filename, StringComparison.OrdinalIgnoreCase))
                          .Select(x => x.Kn5).FirstOrDefault(x => x.TexturesData.ContainsKey(textureName));
            if (kn5 == null) return result;
            
            result.LoadAsync(kn5.TexturesData[textureName], contextHolder.Device).Forget();
            return _textures[key] = result;
        }

        private async void LoadOverrideAsync(RenderableTexture texture, string textureName, IOverridedTextureProvider provider,
                DeviceContextHolder contextHolder) {
            var overrided = await provider.GetOverridedDataAsync(textureName);
            if (overrided != null) {
                texture.LoadOverrideAsync(overrided, contextHolder.Device).Forget();
            }
        }

        public async Task UpdateTextureAsync(string filename, DeviceContextHolder contextHolder) {
            foreach (var texture in _textures
                    .Where(pair => string.Equals(pair.Key, filename, StringComparison.OrdinalIgnoreCase) && !pair.Value.IsDisposed)
                    .Select(pair => pair.Value).OfType<RenderableTexture>()) {
                await texture.LoadAsync(filename, contextHolder.Device);
            }
        }

        [CanBeNull]
        public IRenderableTexture GetTexture(string filename, DeviceContextHolder contextHolder) {
            IRenderableTexture cached;
            if (_textures.TryGetValue(filename, out cached) && !cached.IsDisposed) return cached;
            if (!File.Exists(filename)) return null;

            var result = new RenderableTexture { Resource = null };
            result.LoadAsync(filename, contextHolder.Device).Forget();
            return _textures[filename] = result;
        }
    }
}
