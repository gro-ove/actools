using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcTools.Utils;

namespace AcTools.Kn5File {
    public partial class Kn5 {
        public void ExportTextures(string textureDir) {
            foreach (var texture in Textures.Values) {
                File.WriteAllBytes(Path.Combine(textureDir, texture.Name), TexturesData[texture.Name]);
            }
        }

        public async Task ExportTexturesAsync(string textureDir, IProgress<string> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            foreach (var texture in Textures.Values) {
                if (cancellation.IsCancellationRequested) return;
                progress?.Report(texture.Name);
                await FileUtils.WriteAllBytesAsync(Path.Combine(textureDir, texture.Name), TexturesData[texture.Name], cancellation);
            }
        }
    }
}
