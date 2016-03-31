using System;
using System.Collections.Generic;

namespace AcManager.Tools.Helpers.AdditionalContentInstallation {
    public interface IAdditionalContentInstallator : IDisposable {
        string PasswordValue { get; set; }

        bool IsPasswordRequired { get; }

        bool IsPasswordCorrect { get; }

        IReadOnlyList<AdditionalContentEntry> Entries { get; }

        void InstallEntryTo(AdditionalContentEntry entry, Func<string, bool> filter, string targetDirectory);
    }
}
