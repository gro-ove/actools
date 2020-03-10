using System;
using FirstFloor.ModernUI.Commands;

namespace FirstFloor.ModernUI.Presentation {
    public class Link : Displayable {
        public virtual bool NonSelectable { get; } = false;

        private bool _isEnabled = true;

        public bool IsEnabled {
            get => _isEnabled;
            set => Apply(value, ref _isEnabled);
        }

        private bool _isPinned;

        public bool IsPinned {
            get => _isPinned;
            set => Apply(value, ref _isPinned);
        }

        private DelegateCommand _unpinCommand;

        public DelegateCommand UnpinCommand => _unpinCommand ?? (_unpinCommand = new DelegateCommand(() => {
            IsPinned = false;
        }));

        private object _icon;

        public object Icon {
            get => _icon;
            set => Apply(value, ref _icon);
        }

        private bool _isNew;

        public bool IsNew {
            get => _isNew;
            set => Apply(value, ref _isNew);
        }

        private bool _isShown = true;

        public bool IsShown {
            get => _isShown;
            set => Apply(value, ref _isShown);
        }

        public void SetNew(bool isNew) {
            IsNew = isNew;
        }

        private Uri _source;

        public virtual Uri Source {
            get => _source;
            set {
                if (_source == value) return;
                _source = value;
                OnPropertyChanged();
            }
        }

        public string Key {
            get => Source.OriginalString;
            set => Source = new Uri(value, UriKind.Relative);
        }
    }
}
