using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers.AcSettings {
    public sealed class AcFormEntry : Displayable, IWithId {
        public string Id { get; }

        public AcFormEntry(string id) {
            Id = id;
            DisplayName = id.ApartFromFirst(@"FORM_");
        }

        private int _posX;

        public int PosX {
            get { return _posX; }
            set {
                if (Equals(value, _posX)) return;
                _posX = value;
                OnPropertyChanged();
            }
        }

        private int _posY;

        public int PosY {
            get { return _posY; }
            set {
                if (Equals(value, _posY)) return;
                _posY = value;
                OnPropertyChanged();
            }
        }

        private bool _isVisible;

        public bool IsVisible {
            get { return _isVisible; }
            set {
                if (Equals(value, _isVisible)) return;
                _isVisible = value;
                OnPropertyChanged();
            }
        }

        private bool _isBlocked;

        public bool IsBlocked {
            get { return _isBlocked; }
            set {
                if (Equals(value, _isBlocked)) return;
                _isBlocked = value;
                OnPropertyChanged();
            }
        }

        private int _scale;

        public int Scale {
            get { return _scale; }
            set {
                value = value.Clamp(0, 10000);
                if (Equals(value, _scale)) return;
                _scale = value;
                OnPropertyChanged();
            }
        }
    }
}