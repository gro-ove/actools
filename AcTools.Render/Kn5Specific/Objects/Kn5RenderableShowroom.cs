using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;

namespace AcTools.Render.Kn5Specific.Objects {
    /// <summary>
    /// This thing is quite different — here, textures are loaded directly from the file to videomemory,
    /// without passing thought RAM. Well, hopefully. Only for those showrooms with 200+ MB textures.
    /// </summary>
    public class Kn5RenderableShowroom : Kn5RenderableFile {
        public static bool OptionLoadView = true;

        private TexturesProviderBase _loader;

        private class TextureLoader : TexturesProviderBase, IKn5TextureLoader {
            private readonly Device _device;
            private readonly Dictionary<string, Tuple<Texture2D, ShaderResourceView>> _ready;

            public TextureLoader(Device device) {
                _device = device;
                _ready = new Dictionary<string, Tuple<Texture2D, ShaderResourceView>>();
            }

            public void OnNewKn5(string kn5Filename) { }

            public byte[] LoadTexture(string textureName, ReadAheadBinaryReader reader, int textureSize) {
                if (textureSize > 4e6) {
                    AcToolsLogging.Write($"{textureName}: {(double)textureSize / 1024 / 1024:F1} MB");
                }

                MemoryChunk.Bytes(textureSize).Execute(() => {
                    var bytes = reader.ReadBytes(textureSize);

                    // FromStream simply reads Stream to byte[] underneath, so we could just do it here in
                    // a more controlled manner

                    try {
                        lock (_device) {
                            if (OptionLoadView) {
                                var view = ShaderResourceView.FromMemory(_device, bytes); // new ShaderResourceView(_device, texture);
                                _ready[textureName] = new Tuple<Texture2D, ShaderResourceView>(null, view);
                            } else {
                                var texture = Texture2D.FromMemory(_device, bytes);
                                var view = new ShaderResourceView(_device, texture);
                                _ready[textureName] = new Tuple<Texture2D, ShaderResourceView>(texture, view);
                            }
                        }
                    } catch (SEHException e) {
                        AcToolsLogging.NonFatalErrorNotify("Can’t load texture", "Try again?", e);
                    }
                });

                return null;
            }

            protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
                lock (contextHolder.Device) {
                    var t = _ready.GetValueOrDefault(key)?.Item2;
                    return new RenderableTexture(key) {
                        Resource = t,
                        Exists = t != null
                    };
                }
            }

            public override void Dispose() {
                base.Dispose();

                foreach (var i in _ready.Values.Where(x => x != null)) {
                    if (i.Item2?.Disposed == false) {
                        i.Item2.Dispose();
                    }

                    if (i.Item1?.Disposed == false) {
                        i.Item1.EvictionPriority = ResourcePriority.Minimum;
                        i.Item1.Dispose();
                    }
                }

                _ready.Clear();
                AcToolsLogging.Write("Textures have been disposed");

                GCHelper.CleanUp();
            }
        }

        public string RootDirectory { get; }

        public Kn5RenderableShowroom(Kn5 kn5, Matrix matrix, TexturesProviderBase loader, bool allowSkinnedObjects = false)
                : base(kn5, matrix, false, allowSkinnedObjects) {
            RootDirectory = Path.GetDirectoryName(kn5.OriginalFilename);
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