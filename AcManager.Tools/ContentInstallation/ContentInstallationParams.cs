using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class ContentInstallationParams {
        public static readonly ContentInstallationParams Default = new ContentInstallationParams();

        [NotNull]
        public readonly List<Func<IProgress<AsyncProgressEntry>, CancellationToken, Task>>
                PreInstallation = new List<Func<IProgress<AsyncProgressEntry>, CancellationToken, Task>>(),
                PostInstallation = new List<Func<IProgress<AsyncProgressEntry>, CancellationToken, Task>>();

        public bool AllowExecutables { get; set; }

        [CanBeNull]
        public string CarId { get; set; }

        [CanBeNull]
        public string FallbackId { get; set; }

        [CanBeNull]
        public string Checksum { get; set; }

        [CanBeNull]
        public string DisplayName { get; set; }

        [CanBeNull]
        public string InformationUrl { get; set; }

        [CanBeNull]
        public string Version { get; set; }

        // CUP-related
        public CupContentType? CupType { get; set; }

        [CanBeNull]
        public string[] IdsToUpdate { get; set; }

        [CanBeNull]
        public string Author { get; set; }

        public bool PreferCleanInstallation { get; set; }
    }
}