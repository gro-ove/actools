namespace FirstFloor.ModernUI.Presentation {
    public class TitleLink : Link {
        private string _groupKey;

        public string GroupKey {
            get { return _groupKey; }
            set {
                if (Equals(value, _groupKey)) return;
                _groupKey = value;
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

        private bool _isAccented;

        public bool IsAccented {
            get { return _isAccented; }
            set {
                if (Equals(value, _isAccented)) return;
                _isAccented = value;
                OnPropertyChanged();
            }
        }
    }
}