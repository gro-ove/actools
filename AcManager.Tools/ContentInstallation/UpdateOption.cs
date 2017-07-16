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
            set {
                if (value == _displayName) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        public UpdateOption(string name) {
            DisplayName = name;
        }

        [CanBeNull]
        public Func<string, bool> Filter { get; set; }

        [CanBeNull]
        public Func<string, IEnumerable<string>> CleanUp { get; set; }

        [CanBeNull]
        public Func<CancellationToken, Task> BeforeTask { get; set; }

        [CanBeNull]
        public Func<CancellationToken, Task> AfterTask { get; set; }

        public bool RemoveExisting { get; set; }

        public bool Enabled {
            get => _enabled;
            set {
                if (Equals(value, _enabled)) return;
                _enabled = value;
                OnPropertyChanged();
            }
        }

        public override string ToString() {
            return DisplayName;
        }
    }
}