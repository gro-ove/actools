using AcManager.Tools.Helpers;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class TrackStateViewModelBase : NotifyPropertyChanged {
        private const string DefaultKey = "TrackStateVM.sd";

        public const string UserPresetableKeyValue = "Track States";

        private double _gripStart;

        public double GripStart {
            get { return _gripStart; }
            set {
                value = value.Saturate();
                if (Equals(value, _gripStart)) return;
                _gripStart = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _gripTransfer;

        public double GripTransfer {
            get { return _gripTransfer; }
            set {
                if (Equals(value, _gripTransfer)) return;
                _gripTransfer = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _gripRandomness;

        public double GripRandomness {
            get { return _gripRandomness; }
            set {
                if (Equals(value, _gripRandomness)) return;
                _gripRandomness = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private int _lapGain;

        public int LapGain {
            get { return _lapGain; }
            set {
                if (Equals(value, _lapGain)) return;
                _lapGain = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private string _description;

        [CanBeNull]
        public string Description {
            get { return _description; }
            set {
                if (Equals(value, _description)) return;
                _description = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private class SaveableData {
            public double GripStart = 95d;
            public double GripTransfer = 90d;
            public double Randomness = 2d;
            public int LapGain = 132;
            public string Description;
        }

        protected virtual void SaveLater() {
            Saveable.SaveLater();
        }

        protected readonly ISaveHelper Saveable;
    }
}