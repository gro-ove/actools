using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public static class TexturesProvider {
        private class Kn5Entry {
            public string Filename;
            public Kn5 Kn5;
        }

        private static readonly List<Kn5Entry> Kn5Entries = new List<Kn5Entry>();

        public static void SetKn5(string filename, Kn5 kn5) {
            Kn5Entries.Add(new Kn5Entry {
                Filename = filename,
                Kn5 = kn5
            });
        }

        private static readonly Dictionary<string, IOverridedTextureProvider> OverridedProviders =
                new Dictionary<string, IOverridedTextureProvider>();

        public static void SetOverridedProvider(string filename, IOverridedTextureProvider provider) {
            if (provider == null) {
                OverridedProviders.Remove(filename);
            } else {
                OverridedProviders[filename] = provider;
            }
        }

        public static IEnumerable<IRenderableTexture> GetFor(Kn5 kn5) {
            var keyPrefix = kn5.OriginalFilename + "//";
            return Textures.Where(x => x.Key.StartsWith(keyPrefix)).Select(key => key.Value);
        }

        public static Task UpdateOverridesForAsync(Kn5 kn5, string textureName, DeviceContextHolder contextHolder) {
            IOverridedTextureProvider provider;
            if (!OverridedProviders.TryGetValue(kn5.OriginalFilename, out provider)) return Task.Delay(0);

            var keyPrefix = kn5.OriginalFilename + "//";
            return Task.WhenAll(Textures.Where(x => x.Key.StartsWith(keyPrefix))
                                        .Select(key => key.Value)
                                        .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                        .OfType<RenderableTexture>().Select(async texture => {
                                            var overrided = await provider.GetOverridedData(texture.Name);
                                            if (overrided != null) {
                                                await texture.LoadOverrideAsync(overrided, contextHolder.Device);
                                            } else {
                                                texture.Override = null;
                                            }
                                        }));
        }

        public static Task UpdateOverridesForAsync(Kn5 kn5, DeviceContextHolder contextHolder) {
            return UpdateOverridesForAsync(kn5, null, contextHolder);
        }

        public static void DisposeAll() {
            foreach (var material in Textures.Values) {
                material.Dispose();
            }
            Textures.Clear();
            Kn5Entries.Clear();
        }

        public static void DisposeFor(Kn5 kn5) {
            var keyPrefix = kn5.OriginalFilename + "//";
            var keys = Textures.Keys.Where(x => x.StartsWith(keyPrefix)).ToList();
            foreach (var key in keys) {
                Textures[key].Dispose();
                Textures.Remove(key);
            }

            var entry = Kn5Entries.FirstOrDefault(x => x.Kn5 == kn5);
            if (entry == null) return;
            Kn5Entries.Remove(entry);
        }

        private static readonly Dictionary<string, IRenderableTexture> Textures = new Dictionary<string, IRenderableTexture>();

        [NotNull]
        public static IRenderableTexture GetTexture([NotNull] string kn5Filename, string textureName, DeviceContextHolder contextHolder) {
            if (kn5Filename == null) throw new ArgumentNullException(nameof(kn5Filename));

            var key = kn5Filename + "//" + textureName;
            IRenderableTexture texture;
            if (Textures.TryGetValue(key, out texture)) return texture;

            var result = new RenderableTexture(textureName) { Resource = null };
            IOverridedTextureProvider provider;
            if (OverridedProviders.TryGetValue(kn5Filename, out provider)) {
                LoadOverrideAsync(result, textureName, provider, contextHolder);
            }

            var kn5 = Kn5Entries.Where(x => string.Equals(x.Filename, kn5Filename, StringComparison.OrdinalIgnoreCase))
                          .Select(x => x.Kn5).FirstOrDefault(x => x.TexturesData.ContainsKey(textureName));
            if (kn5 == null) return result;
            
            result.LoadAsync(kn5.TexturesData[textureName], contextHolder.Device).Forget();
            return Textures[key] = result;
        }

        private static async void LoadOverrideAsync(RenderableTexture texture, string textureName, IOverridedTextureProvider provider,
                DeviceContextHolder contextHolder) {
            var overrided = await provider.GetOverridedData(textureName);
            if (overrided != null) {
                texture.LoadOverrideAsync(overrided, contextHolder.Device).Forget();
            }
        }

        [CanBeNull]
        public static IRenderableTexture GetTexture(string filename, DeviceContextHolder contextHolder) {
            if (Textures.ContainsKey(filename)) return Textures[filename];

            // TODO: watching?
            if (!File.Exists(filename)) {
                return null;
            }

            var result = new RenderableTexture { Resource = null };
            result.LoadAsync(filename, contextHolder.Device).Forget();
            return Textures[filename] = result;
        }
    }
}
