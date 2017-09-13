using System;
using JetBrains.Annotations;

namespace AcTools.KnhFile {
    public partial class Knh {
        public string OriginalFilename { get; }

        private Knh([NotNull] KnhEntry entry) {
            OriginalFilename = string.Empty;
            RootEntry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        private Knh(string filename, [NotNull] KnhEntry entry) {
            OriginalFilename = filename;
            RootEntry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        [NotNull]
        public KnhEntry RootEntry;
    }
}
