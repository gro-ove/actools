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

        private bool _isNew;

        public bool IsNew {
            get { return _isNew; }
            set {
                if (Equals(value, _isNew)) return;
                _isNew = value;
                OnPropertyChanged();
            }
        }

        private bool _isShown = true;

        public bool IsShown {
            get { return _isShown; }
            set {
                if (Equals(value, _isShown)) return;
                _isShown = value;
                OnPropertyChanged();
            }
        }

        public void SetNew(bool isNew) {
            IsNew = isNew;
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
