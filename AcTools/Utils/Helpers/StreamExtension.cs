using System;
using System.IO;
using System.Text;

namespace AcTools.Utils.Helpers {
    public static class StreamExtension {
        public static byte[] ReadAsBytes(this Stream s) {
            using (var m = new MemoryStream()) {
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        public static byte[] ReadAsBytesAndDispose(this Stream s) {
            using (s) {
                return ReadAsBytes(s);
            }
        }

        public static MemoryStream ReadAsMemoryStream(this Stream s) {
            var m = new MemoryStream();
            s.CopyTo(m);
            return m;
        }

        public static MemoryStream ReadAsMemoryStreamAndDispose(this Stream s) {
            using (s) {
                return ReadAsMemoryStream(s);
            }
        }

        public static string ReadAsString(this Stream s) {
            return ReadAsBytes(s).GetString();
        }

        /// <summary>
        /// Using UTF8 (only if it's a correct one) or Default encoding
        /// </summary>
        /// <returns></returns>
        public static string ReadAsStringAndDispose(this Stream s) {
            return ReadAsBytesAndDispose(s).GetString();
        }
    }
}
