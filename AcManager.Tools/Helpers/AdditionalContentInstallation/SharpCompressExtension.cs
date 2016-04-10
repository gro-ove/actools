using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using SharpCompress.Archive;
using SharpCompress.Archive.GZip;
using SharpCompress.Archive.Rar;
using SharpCompress.Archive.SevenZip;
using SharpCompress.Archive.Tar;
using SharpCompress.Archive.Zip;
using SharpCompress.Common;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class SharpCompressExtension {
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
                    throw new NotSupportedException("Sorry, but " + archive.Type + " encryption isn't supported");
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
                Logging.Write("SharpCompressExtension.HasAnyEncryptedFiles exception: " + e);
                return true;
            }
        }

        public static string GetFilename(this IArchiveEntry fileInfo) {
            return fileInfo.Key.Replace('/', '\\');
        }

        public static string GetName(this IArchiveEntry fileInfo) {
            return Path.GetFileName(fileInfo.Key ?? "");
        }


        public static byte[] ExtractFile(this IArchiveEntry fileInfo) {
            using (var stream = fileInfo.OpenEntryStream())
            using (var memory = new MemoryStream()) {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        public static async Task<byte[]> ExtractFileAsync(this IArchiveEntry fileInfo) {
            using (var stream = fileInfo.OpenEntryStream())
            using (var memory = new MemoryStream()) {
                await stream.CopyToAsync(memory);
                return memory.ToArray();
            }
        }
    }
}