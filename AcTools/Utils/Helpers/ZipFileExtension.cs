using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class ZipFileExtension {
        public static string ReadString([NotNull] this ZipArchive archive, [NotNull] string entryName) {
            return archive.ReadBytes(entryName).ToUtf8String();
        }

        [CanBeNull]
        public static string TryReadString([NotNull] this ZipArchive archive, [NotNull] string entryName) {
            return archive.TryReadBytes(entryName)?.ToUtf8String();
        }

        public static bool ZipContains([NotNull] this ZipArchive archive, [NotNull] string entryName) {
            return archive.Entries.Any(x => FileUtils.ArePathsEqual(x.FullName, entryName));
        }

        public static byte[] ReadBytes([NotNull] this ZipArchive archive, [NotNull] string entryName) {
            return archive.Entries.FirstOrDefault(x => FileUtils.ArePathsEqual(x.FullName, entryName))?.Open().ReadAsBytesAndDispose()
                    ?? throw new InvalidOperationException("Entry not found: " + entryName);
        }

        [CanBeNull]
        public static byte[] TryReadBytes([NotNull] this ZipArchive archive, [NotNull] string entryName) {
            return archive.Entries.FirstOrDefault(x => FileUtils.ArePathsEqual(x.FullName, entryName))?.Open().ReadAsBytesAndDispose();
        }

        public static ZipArchiveEntry AddString([NotNull] this ZipArchive destination, [NotNull] string entryName, string str) {
            return AddBytes(destination, entryName, Encoding.UTF8.GetBytes(str));
        }

        public static ZipArchiveEntry AddBytes([NotNull] this ZipArchive destination, [NotNull] string entryName, byte[] data) {
            return AddBytes(destination, entryName, data, 0, data.Length);
        }

        public static ZipArchiveEntry AddBytes([NotNull] this ZipArchive destination, [NotNull] string entryName, byte[] data, int index, int count) {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (entryName == null) throw new ArgumentNullException(nameof(entryName));


            using (var fs = new MemoryStream(data, index, count)) {
                var entry = destination.CreateEntry(entryName);
                var lastWrite = DateTime.Now;

                // If file to be archived has an invalid last modified time, use the first datetime representable in the Zip timestamp format
                // (midnight on January 1, 1980):
                if (lastWrite.Year < 1980 || lastWrite.Year > 2107) {
                    lastWrite = new DateTime(1980, 1, 1, 0, 0, 0);
                }

                entry.LastWriteTime = lastWrite;

                using (var es = entry.Open()) {
                    fs.CopyTo(es);
                }

                return entry;
            }
        }
    }
}