using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class UpdateOption : Displayable {
        private bool _enabled = true;

        private string _displayName;

        public sealed override string DisplayName {
            get => _displayName;
            set => Apply(value, ref _displayName);
        }

        public UpdateOption(string name, bool removeExisting) {
            DisplayName = name;
            RemoveExisting = removeExisting;
        }

        [CanBeNull]
        public Func<string, bool> Filter { get; set; }

        [CanBeNull]
        public Func<string, IEnumerable<string>> CleanUp { get; set; }

        [CanBeNull]
        public Func<CancellationToken, Task> BeforeTask { get; set; }

        [CanBeNull]
        public Func<CancellationToken, Task> AfterTask { get; set; }

        public bool RemoveExisting { get; }

        public bool Enabled {
            get => _enabled;
            set => Apply(value, ref _enabled);
        }

        public override string ToString() {
            return DisplayName;
        }
    }
}