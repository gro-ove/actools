using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Kn5File;
using AcTools.Render.Base;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Textures {
    public static class TexturesProvider {
        private static List<Kn5> _kn5 = new List<Kn5>();

        public static void Initialize(Kn5 kn5) {
            _kn5.Add(kn5);
        }

        public static void DisposeAll() {
            foreach (var material in Textures.Values) {
                material.Dispose();
            }
            Textures.Clear();
            _kn5.Clear();
        }

        public static void DisposeFor(Kn5 kn5) {
            var keyPrefix = kn5.OriginalFilename + "//";
            var keys = Textures.Keys.Where(x => x.StartsWith(keyPrefix)).ToList();
            foreach (var key in keys) {
                Textures[key].Dispose();
                Textures.Remove(key);
            }
            _kn5.Remove(kn5);
        }

        private static readonly Dictionary<string, IRenderableTexture> Textures = new Dictionary<string, IRenderableTexture>();

        public static IRenderableTexture GetTexture(string kn5Filename, string textureName, DeviceContextHolder contextHolder) {
            var kn5 = _kn5.FirstOrDefault(x => x.OriginalFilename == kn5Filename);
            if (kn5 == null) return new RenderableTexture { Resource = null };

            var key = kn5Filename + "//" + textureName;
            if (Textures.ContainsKey(key)) return Textures[key];

            var result = new RenderableTexture { Resource = null };
            LoadTexture(result, contextHolder.Device, kn5.TexturesData[textureName]);
            return Textures[key] = result;
        }

        // TODO: Disposing?
        public static IRenderableTexture GetTexture(string filename, DeviceContextHolder contextHolder) {
            if (Textures.ContainsKey(filename)) return Textures[filename];

            var result = new RenderableTexture { Resource = null };
            LoadTexture(result, contextHolder.Device, filename);
            return Textures[filename] = result;
        }

        private static async void LoadTexture(RenderableTexture texture, Device device, string filename) {
            var resource = await Task.Run(() => ShaderResourceView.FromFile(device, filename));
            texture.Resource = resource;
        }

        private static async void LoadTexture(RenderableTexture texture, Device device, byte[] data) {
            var resource = await Task.Run(() => ShaderResourceView.FromMemory(device, data));
            texture.Resource = resource;
        }
    }
}
