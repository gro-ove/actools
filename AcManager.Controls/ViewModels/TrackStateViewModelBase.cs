using System.Text;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class TrackStateViewModelBase : NotifyPropertyChanged {
        private const string DefaultKey = "TrackStateVM.sd";

        public const string UserPresetableKeyValue = "Track States";

        #region Properties
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
        #endregion

        #region Saveable
        private class SaveableData {
            public double GripStart = 95d;
            public double GripTransfer = 90d;
            public double GripRandomness = 2d;
            public int LapGain = 132;
            public string Description;
        }

        protected virtual void SaveLater() {
            Saveable.SaveLater();
        }

        protected readonly ISaveHelper Saveable;

        protected TrackStateViewModelBase(string key, bool fixedMode) {
            Saveable = new SaveHelper<SaveableData>(key ?? DefaultKey, () => fixedMode ? null : new SaveableData {
                GripStart = GripStart,
                GripTransfer = GripTransfer,
                GripRandomness = GripRandomness,
                LapGain = LapGain,
                Description = Description
            }, o => {
                GripStart = o.GripStart;
                GripTransfer = o.GripTransfer;
                GripRandomness = o.GripRandomness;
                LapGain = o.LapGain;
                Description = o.Description;
            });
        }

        public byte[] ToBytes() {
            return Encoding.UTF8.GetBytes(Saveable.ToSerializedString() ?? "");
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Full load-and-save mode. All changes will be saved automatically and loaded 
        /// later (only with this constuctor).
        /// </summary>
        public TrackStateViewModelBase() : this(null, false) {
            Saveable.Initialize();
        }

        /// <summary>
        /// Create a new AssistsViewModel which will load data from serialized string, but won’t
        /// save any changes if they will occur.
        /// </summary>
        public static TrackStateViewModelBase CreateFixed([NotNull] string serializedData) {
            var result = new TrackStateViewModelBase(DefaultKey, true);
            result.Saveable.Reset();
            result.Saveable.FromSerializedString(serializedData);
            return result;
        }

        /// <summary>
        /// Create a new AssistsViewModel which will load data from serialized string, but won’t
        /// save any changes if they will occur.
        /// </summary>
        /// <param name="section">INI-file section.</param>
        public static TrackStateViewModelBase CreateBuiltIn([NotNull] IniFileSection section) {
            return new TrackStateViewModelBase(null, true) {
                GripStart = section.GetDouble("SESSION_START", 95d) / 100d,
                GripTransfer = section.GetDouble("SESSION_TRANSFER", 90d) / 100d,
                GripRandomness = section.GetDouble("RANDOMNESS", 2d) / 100d,
                LapGain = section.GetInt("LAP_GAIN", 132),
                Description = section.GetNonEmpty("DESCRIPTION")
            };
        }
        #endregion
    }
}