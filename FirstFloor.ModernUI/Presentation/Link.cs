using System;

namespace FirstFloor.ModernUI.Presentation {
    public class Link : Displayable {
        public virtual bool NonSelectable { get; } = false;

        private bool _isEnabled = true;

        public bool IsEnabled {
            get { return _isEnabled; }
            set {
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        private Uri _source;

        public virtual Uri Source {
            get { return _source; }
            set {
                if (_source == value) return;
                _source = value;
                OnPropertyChanged();
            }
        }
    }
}
