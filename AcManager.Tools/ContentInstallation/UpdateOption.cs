using System;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.ContentInstallation {
    public sealed class UpdateOption : Displayable {
        private bool _enabled = true;

        public UpdateOption(string name) {
            DisplayName = name;
        }

        public Func<string, bool> Filter { get; set; }

        public bool RemoveExisting { get; set; }

        public bool Enabled {
            get { return _enabled; }
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