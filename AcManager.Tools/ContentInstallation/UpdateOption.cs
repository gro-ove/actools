using System;
using System.Collections.Generic;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public sealed class UpdateOption : Displayable {
        private bool _enabled = true;

        public UpdateOption(string name) {
            DisplayName = name;
        }

        [CanBeNull]
        public Func<string, bool> Filter { get; set; }

        [CanBeNull]
        public Func<string, IEnumerable<string>> CleanUp { get; set; }

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