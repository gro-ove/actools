using System;
using System.IO;
using System.Linq;
using System.Text;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace AcManager.Tools.ContentInstallation {
    public static class SharpCompressExtension {
        // TODO: writer, string as a file
        public static void WriteString(this IWriter writer, string entryPath, string content) {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content))) {
                writer.Write(entryPath, stream);
            }
        }

        public static void AddZipDescription(this Stream stream, string description) {
            stream.Seek(-2, SeekOrigin.End);

            var bytes = Encoding.GetEncoding(1252).GetBytes(description);
            var length = Math.Min((ushort)bytes.Length, ushort.MaxValue);
            stream.Write(BitConverter.GetBytes(length), 0, 2);
            stream.Write(bytes, 0, length);
        }

        public static void WriteBytes(this IWriter writer, string entryPath, byte[] content) {
            using (var stream = new MemoryStream(content)) {
                writer.Write(entryPath, stream);
            }
        }

        public static void ExtractAsArchiveTo(this Stream stream, string destination, ExtractionOptions options = null) {
            options = options ?? new ExtractionOptions {
                Overwrite = true,
                ExtractFullPath = true
            };

            using (var reader = ReaderFactory.Open(stream)) {
                while (reader.MoveToNextEntry()) {
                    if (!reader.Entry.IsDirectory) {
                        reader.WriteEntryToDirectory(destination, options);
                    }
                }
            }
        }

        public static void ExtractAsArchiveTo(this byte[] bytes, string destination, ExtractionOptions options = null) {
            using (var stream = new MemoryStream(bytes)) {
                stream.ExtractAsArchiveTo(destination, options);
            }
        }

        [NotNull]
        public static IArchive Open(string filename, string password) {
            var archive = ArchiveFactory.Open(filename);
            if (password == null) return archive;

            switch (archive.Type) {
                case ArchiveType.Rar:
                    return RarArchive.Open(filename, new ReaderOptions { Password = password });
                case ArchiveType.Zip:
                    return ZipArchive.Open(filename, new ReaderOptions { Password = password });
                case ArchiveType.Tar:
                case ArchiveType.SevenZip:
                case ArchiveType.GZip:
                    throw new NotSupportedException(string.Format(ToolsStrings.ArchiveInstallator_UnsupportedEncryption, archive.Type));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool HasAnyEncryptedFiles([NotNull] this IArchive archive) {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            try {
                var entry = archive.Entries
                                   .Where(x => x.IsEncrypted && x.Size > 0 && !x.IsDirectory)
                                   .MinEntryOrDefault(x => x.CompressedSize);
                if (entry == null) return false;

                if (archive.IsSolid) {
                    using (var reader = archive.ExtractAllEntries()) {
                        while (reader.MoveToNextEntry()) {
                            if (reader.Entry.IsDirectory) continue;
                            using (var stream = reader.OpenEntryStream()) {
                                stream.ReadByte();
                                return false;
                            }
                        }

                        return false;
                    }
                }

                using (var stream = entry.OpenEntryStream()) {
                    stream.ReadByte();
                    return false;
                }
            } catch (CryptographicException) {
                return true;
            } catch (NullReferenceException) {
                // specially for not very reliable library
                return true;
            } catch (InvalidFormatException) {
                // might be file crc mismatch
                return true;
            }
        }
    }
}