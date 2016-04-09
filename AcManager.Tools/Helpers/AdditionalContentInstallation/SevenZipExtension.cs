using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SevenZip;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public static class SevenZipExtension {
        public static bool HasAnyEncryptedFiles(this SevenZipExtractor extractor) {
            return extractor.ArchiveFileData.Any(x => x.Encrypted);
        }

        public static string GetFilename(this ArchiveFileInfo fileInfo) {
            return fileInfo.FileName.Replace('/', '\\');
        }

        public static string GetName(this ArchiveFileInfo fileInfo) {
            return Path.GetFileName(fileInfo.FileName ?? "");
        }

        public static void ExtractFile(this SevenZipExtractor extractor, ArchiveFileInfo fileInfo, Stream stream) {
            extractor.ExtractFile(fileInfo.FileName, stream);
        }

        public static byte[] ExtractFile(this SevenZipExtractor extractor, ArchiveFileInfo fileInfo) {
            using (var stream = new MemoryStream()) {
                extractor.ExtractFile(fileInfo.FileName, stream);
                return stream.ToArray();
            }
        }

        public static Task<byte[]> ExtractFileAsync(this SevenZipExtractor extractor, ArchiveFileInfo fileInfo) {
            return Task.Run(() => extractor.ExtractFile(fileInfo));
        }
    }
}