using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public partial class FileUtils {
        [ItemNotNull]
        public static async Task<string> ReadAllTextAsync(string filename, CancellationToken cancellation = default) {
            var bytes = await ReadAllBytesAsync(filename, cancellation);
            if (cancellation.IsCancellationRequested) return string.Empty;
            return Encoding.UTF8.GetString(bytes);
        }

        [ItemNotNull]
        public static async Task<string[]> ReadAllLinesAsync(string filename, CancellationToken cancellation = default) {
            var lines = new List<string>();
            using (var sr = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true), Encoding.UTF8)) {
                string line;
                while ((line = await sr.ReadLineAsync()) != null) {
                    if (cancellation.IsCancellationRequested) return new string[0];
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        [ItemNotNull]
        public static async Task<byte[]> ReadAllBytesAsync(string filename, CancellationToken cancellation = default) {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)) {
                var result = new byte[stream.Length];
                await stream.ReadAsync(result, 0, (int)stream.Length, cancellation);
                return result;
            }
        }

        public static async Task WriteAllBytesAsync([NotNull] string filename, [NotNull] byte[] bytes,
                CancellationToken cancellation = default) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (var stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellation);
            }
        }

        public static async Task WriteAllBytesAsync([NotNull] string filename, [NotNull] Stream source,
                CancellationToken cancellation = default) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            using (var stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
                await source.CopyToAsync(stream, 81920, cancellation);
            }
        }
    }
}
