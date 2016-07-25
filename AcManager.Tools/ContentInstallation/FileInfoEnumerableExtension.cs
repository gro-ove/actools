using System;
using System.Collections.Generic;
using System.Linq;

namespace AcManager.Tools.ContentInstallation {
    internal static class FileInfoEnumerableExtension {
        internal static BaseContentInstallator.IFileInfo GetByPathOrDefault(this IEnumerable<BaseContentInstallator.IFileInfo> source, string path) {
            return source.FirstOrDefault(x => string.Equals(x.Filename, path, StringComparison.OrdinalIgnoreCase));
        }
    }
}