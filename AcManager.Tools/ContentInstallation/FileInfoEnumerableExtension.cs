using System;
using System.Collections.Generic;
using System.Linq;

namespace AcManager.Tools.ContentInstallation {
    internal static class FileInfoEnumerableExtension {
        internal static IFileInfo GetByPathOrDefault(this IEnumerable<IFileInfo> source, string path) {
            return source.FirstOrDefault(x => string.Equals(x.Key, path, StringComparison.OrdinalIgnoreCase));
        }
    }
}