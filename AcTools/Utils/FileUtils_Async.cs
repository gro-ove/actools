using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public partial class FileUtils {
        [ItemNotNull]
        public static async Task<byte[]> ReadAllBytesAsync(string filename, CancellationToken cancellation = default(CancellationToken)) {
            using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                var result = new byte[stream.Length];
                await stream.ReadAsync(result, 0, (int)stream.Length, cancellation);
                return result;
            }
        }

        public static async Task WriteAllBytesAsync([NotNull] string filename, [NotNull] byte[] bytes,
                CancellationToken cancellation = default(CancellationToken)) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using (var stream = File.Open(filename, FileMode.Create)) {
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellation);
            }
        }

        public static async Task WriteAllBytesAsync([NotNull] string filename, [NotNull] Stream source,
                CancellationToken cancellation = default(CancellationToken)) {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            using (var stream = File.Open(filename, FileMode.Create)) {
                await source.CopyToAsync(stream, 81920, cancellation);
            }
        }
    }
}
