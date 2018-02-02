using System;
using JetBrains.Annotations;

namespace AcTools.Processes {
    public class AcsRunEventArgs : EventArgs {
        public AcsRunEventArgs([CanBeNull] string acsFilename, bool? use32BitVersion) {
            AcsFilename = acsFilename;
            Use32BitVersion = use32BitVersion;
        }

        [CanBeNull]
        public string AcsFilename { get; }

        public bool? Use32BitVersion { get; }
    }
}