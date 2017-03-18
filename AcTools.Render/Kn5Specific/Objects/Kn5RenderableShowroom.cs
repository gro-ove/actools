using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Objects {
    /// <summary>
    /// This thing is quite different — here, textures are loaded directly from the file to videomemory,
    /// without passing thought RAM. Well, hopefully. Only for those showrooms with 200+ MB textures.
    /// </summary>
    public class Kn5RenderableShowroom : Kn5RenderableFile {
        private TexturesProviderBase _loader;

        private class TextureLoader : TexturesProviderBase, IKn5TextureLoader {
            private readonly Device _device;
            private readonly Dictionary<string, ShaderResourceView> _ready;

            public TextureLoader(Device device) {
                _device = device;
                _ready = new Dictionary<string, ShaderResourceView>();
            }

            public byte[] LoadTexture(string textureName, Stream stream, int textureSize) {
                AcToolsLogging.Write(textureName + ": " + textureSize / 1024 / 1024 + " MB");

                MemoryChunk.Bytes(textureSize).Execute(() => {
                    var bytes = new byte[textureSize];
                    AcToolsLogging.Write("Bytes are ready");

                    stream.Read(bytes, 0, textureSize);
                    AcToolsLogging.Write("Texture has been read");

                    // FromStream simply reads Stream to byte[] underneath, so we could just do it here in
                    // a more controlled manner
                    _ready[textureName] = ShaderResourceView.FromMemory(_device, bytes);
                    AcToolsLogging.Write("Texture has been loaded");
                });

                return null;
            }

            protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
                return new RenderableTexture(key) { Resource = _ready.GetValueOrDefault(key) };
            }

            public override void Dispose() {
                foreach (var i in _ready.Values.Where(x => x != null)) {
                    i.Resource.Dispose();
                    i.Dispose();
                }

                _ready.Clear();
                AcToolsLogging.Write("Textures have been disposed");
            }
        }

        public Kn5RenderableShowroom(Kn5 kn5, Matrix matrix, TexturesProviderBase loader, bool allowSkinnedObjects = false) : base(kn5, matrix, false, allowSkinnedObjects) {
            _loader = loader;
        }

        protected override ITexturesProvider InitializeTextures(IDeviceContextHolder contextHolder) {
            return _loader;
        }

        public override void Dispose() {
            base.Dispose();

            // At this point, theoretically, TextureLoader could already be disposed. But it’s safe
            // to dispose it twice — in case InitializeTextures() wasn’t called yet.
            DisposeHelper.Dispose(ref _loader);
        }

        public static Kn5RenderableShowroom Load(Device device, string filename, Matrix matrix, bool allowSkinnedObjects = false) {
            var loader = new TextureLoader(device);

            try {
                return new Kn5RenderableShowroom(Kn5.FromFile(filename, loader), matrix, loader, allowSkinnedObjects);
            } catch {
                loader.Dispose();
                throw;
            }
        }
    }
}