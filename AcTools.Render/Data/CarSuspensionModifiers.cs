using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace AcTools.Render.Data {
    /// <summary>
    /// Used in UI as view model, so notifies about properties changed.
    /// </summary>
    public class CarSuspensionModifiers : INotifyPropertyChanged {
        private float _trackWidthFrontAdd;

        public float TrackWidthFrontAdd {
            get { return _trackWidthFrontAdd; }
            set {
                if (Equals(value, _trackWidthFrontAdd)) return;
                _trackWidthFrontAdd = value;
                OnPropertyChanged();
            }
        }

        private float _trackWidthRearAdd;

        public float TrackWidthRearAdd {
            get { return _trackWidthRearAdd; }
            set {
                if (Equals(value, _trackWidthRearAdd)) return;
                _trackWidthRearAdd = value;
                OnPropertyChanged();
            }
        }

        private float _zOffsetFrontAdd;

        public float ZOffsetFrontAdd {
            get { return _zOffsetFrontAdd; }
            set {
                if (Equals(value, _zOffsetFrontAdd)) return;
                _zOffsetFrontAdd = value;
                OnPropertyChanged();
            }
        }

        private float _zOffsetRearAdd;

        public float ZOffsetRearAdd {
            get { return _zOffsetRearAdd; }
            set {
                if (Equals(value, _zOffsetRearAdd)) return;
                _zOffsetRearAdd = value;
                OnPropertyChanged();
            }
        }

        private float _baseYFrontAdd;

        public float BaseYFrontAdd {
            get { return _baseYFrontAdd; }
            set {
                if (Equals(value, _baseYFrontAdd)) return;
                _baseYFrontAdd = value;
                OnPropertyChanged();
            }
        }

        private float _baseYRearAdd;

        public float BaseYRearAdd {
            get { return _baseYRearAdd; }
            set {
                if (Equals(value, _baseYRearAdd)) return;
                _baseYRearAdd = value;
                OnPropertyChanged();
            }
        }

        private float _camberFrontAdd;

        public float CamberFrontAdd {
            get { return _camberFrontAdd; }
            set {
                if (Equals(value, _camberFrontAdd)) return;
                _camberFrontAdd = value;
                OnPropertyChanged();
            }
        }

        private float _camberRearAdd;

        public float CamberRearAdd {
            get { return _camberRearAdd; }
            set {
                if (Equals(value, _camberRearAdd)) return;
                _camberRearAdd = value;
                OnPropertyChanged();
            }
        }

        private float _toeFrontAdd;

        public float ToeFrontAdd {
            get { return _toeFrontAdd; }
            set {
                if (Equals(value, _toeFrontAdd)) return;
                _toeFrontAdd = value;
                OnPropertyChanged();
            }
        }

        private float _toeRearAdd;

        public float ToeRearAdd {
            get { return _toeRearAdd; }
            set {
                if (Equals(value, _toeRearAdd)) return;
                _toeRearAdd = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}