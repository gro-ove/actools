using System;
using System.IO;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class StreamExtension {
        [NotNull]
        public static byte[] ReadAsBytes([NotNull] this Stream s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            using (var m = new MemoryStream()) {
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        [NotNull]
        public static byte[] ReadAsBytesAndDispose([NotNull] this Stream s) {
            using (s) {
                return ReadAsBytes(s);
            }
        }

        [NotNull]
        public static MemoryStream ReadAsMemoryStream([NotNull] this Stream s) {
            var m = new MemoryStream();
            s.CopyTo(m);
            return m;
        }

        [NotNull]
        public static MemoryStream ReadAsMemoryStreamAndDispose([NotNull] this Stream s) {
            using (s) {
                return ReadAsMemoryStream(s);
            }
        }

        [NotNull]
        public static string ReadAsString([NotNull] this Stream s) {
            return ReadAsBytes(s).ToUtf8String();
        }

        /// <summary>
        /// Using UTF8 (only if it’s a correct one) or Default encoding
        /// </summary>
        /// <returns></returns>
        [NotNull]
        public static string ReadAsStringAndDispose([NotNull] this Stream s) {
            return ReadAsBytesAndDispose(s).ToUtf8String();
        }
    }
}
