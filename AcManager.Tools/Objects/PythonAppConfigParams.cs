using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class PythonAppConfigParams {
        public PythonAppConfigParams([NotNull] string pythonAppLocation) {
            PythonAppLocation = pythonAppLocation;
            FilesRelativeDirectory = pythonAppLocation;
        }

        [NotNull]
        public string PythonAppLocation { get; }

        [NotNull]
        public string FilesRelativeDirectory { get; set; }

        [CanBeNull]
        public Action DisposalAction { get; set; }

        [CanBeNull]
        public Func<string, IEnumerable<string>> ScanFunc { get; set; }

        [CanBeNull]
        public Func<PythonAppConfigParams, string, PythonAppConfig> ConfigFactory { get; set; }

        public bool SaveOnlyNonDefault { get; set; }

        [CanBeNull]
        public Dictionary<string, string> Flags { get; set; }
    }
}