using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public partial class ContentInstallationEntry {
        public sealed class ExtraOption : Displayable {
            [CanBeNull]
            public readonly Func<IProgress<AsyncProgressEntry>, CancellationToken, Task> PreInstallation, PostInstallation;

            public string Description { get; }

            private bool _active;

            public bool Active {
                get { return _active; }
                set {
                    if (Equals(value, _active)) return;
                    _active = value;
                    OnPropertyChanged();
                }
            }

            public ExtraOption(string name, string description,
                    Func<IProgress<AsyncProgressEntry>, CancellationToken,Task> pre = null,
                    Func<IProgress<AsyncProgressEntry>, CancellationToken,Task> post = null,
                    bool activeByDefault = false) {
                DisplayName = name;
                Description = description;
                PostInstallation = post;
                PreInstallation = pre;
                Active = activeByDefault;
            }
        }

        private static Task<IReadOnlyList<ExtraOption>> GetExtraOptionsAsync(EntryWrapper[] entries) {
            return GetGbwRelatedExtraOptions(entries);
        }
    }
}