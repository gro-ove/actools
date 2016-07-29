using System;
using System.IO;
using System.Linq;
using System.Text;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using SharpCompress.Archive;
using SharpCompress.Archive.Rar;
using SharpCompress.Archive.Zip;
using SharpCompress.Common;
using SharpCompress.Writer;

namespace AcManager.Tools.ContentInstallation {
    public static class SharpCompressExtension {
        // TODO: writer, string as a file
        public static void WriteString(this IWriter writer, string entryPath, string content) {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content))) {
                writer.Write(entryPath, stream);
            }
        }

        public static IArchive Open(string filename, string password) {
            var archive = ArchiveFactory.Open(filename);
            if (password == null) return archive;

            switch (archive.Type) {
                case ArchiveType.Rar:
                    return RarArchive.Open(filename, Options.None, password);
                case ArchiveType.Zip:
                    return ZipArchive.Open(filename, Options.None, password);
                case ArchiveType.Tar:
                case ArchiveType.SevenZip:
                case ArchiveType.GZip:
                    throw new NotSupportedException(string.Format(ToolsStrings.ArchiveInstallator_UnsupportedEncryption, archive.Type));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static bool HasAnyEncryptedFiles(this IArchive archive) {
            try {
                using (var entry = archive.Entries
                                          .Where(x => x.IsEncrypted && x.Size > 0 && !x.IsDirectory)
                                          .MinEntryOrDefault(x => x.CompressedSize)?
                                          .OpenEntryStream()) {
                    return entry != null && entry.ReadByte() != -1;
                }
            } catch (CryptographicException) {
                return true;
            } catch (Exception e) {
                Logging.Write("[SharpCompressExtension] HasAnyEncryptedFiles(): " + e);
                return true;
            }
        }
    }
}