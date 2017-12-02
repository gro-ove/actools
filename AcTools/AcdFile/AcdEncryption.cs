using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace AcTools.AcdFile {
    public interface IAcdEncryptionFactory {
        IAcdEncryption Create(string keySource);
    }

    public interface IAcdEncryption {
        void Decrypt([NotNull] byte[] data);
        void Encrypt([NotNull] byte[] data, [NotNull] byte[] result);
    }

    public static class AcdEncryption {
        internal static IAcdEncryptionFactory Factory;

        [NotNull]
        public static IAcdEncryption FromAcdFilename([NotNull] string acdFilename) {
            if (Factory == null) throw new NotSupportedException();
            var name = Path.GetFileName(acdFilename) ?? "";
            var id = name.StartsWith("data", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(Path.GetDirectoryName(acdFilename)) : name;
            return Factory.Create(id);
        }
    }
}
