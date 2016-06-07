using System;
using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class CarSetupObject : AcCommonSingleFileObject {
        private const string GenericDirectory = "generic";

        public string CarId { get; }

        [CanBeNull]
        public string TrackId { get; private set; }

        [CanBeNull]
        public TrackObject Track {
            get { return _track; }
            set {
                if (Equals(value, _track)) return;
                _track = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public override string Extension => ".ini";

        public CarSetupObject(string carId, IFileAcManager manager, string fileName, bool enabled)
                : base(manager, fileName, enabled) {
            CarId = carId;

            foreach (var tyrePressure in TyresPressure) {
                tyrePressure.PropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(tyrePressure.Value)) {
                        Changed = true;
                    }
                };
            }
        }

        private int _tyres;

        public int Tyres {
            get { return _tyres; }
            set {
                value = Math.Max(value, 0);
                if (Equals(value, _tyres)) return;
                _tyres = value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        private int _fuel;

        public int Fuel {
            get { return _fuel; }
            set {
                value = Math.Max(value, 0);
                if (Equals(value, _fuel)) return;
                _fuel = value;
                OnPropertyChanged();
                Changed = true;
            }
        }

        private int _fuelMaximum = int.MaxValue;
        private TrackObject _track;

        public int FuelMaximum {
            get { return _fuelMaximum; }
            set {
                value = Math.Max(value, 0);
                if (Equals(value, _fuelMaximum)) return;
                _fuelMaximum = value;
                OnPropertyChanged();
            }
        }

        public sealed class TyrePressure : Displayable, IWithId<string> {
            public string Id { get; }

            public TyrePressure(string id, string name) {
                Id = id;
                DisplayName = name;
            }

            private int? _value;

            public int? Value {
                get { return _value; }
                set {
                    value = value?.Clamp(0, 200);
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public TyrePressure[] TyresPressure { get; } = {
            new TyrePressure("LF", "Left front"),
            new TyrePressure("RF", "Right front"),
            new TyrePressure("LR", "Left rear"),
            new TyrePressure("RR", "Right rear")
        };

        protected override void LoadOrThrow() {
            base.LoadOrThrow();

            var dir = Path.GetDirectoryName(Id);
            TrackId = dir == GenericDirectory ? null : dir;
            Track = TrackId == null ? null : TracksManager.Instance.GetById(TrackId);

            try {
                var ini = new IniFile(Location);
                Tyres = ini["TYRES"].GetInt("VALUE", 0);
                Fuel = ini["FUEL"].GetInt("VALUE", 0);

                foreach (var entry in TyresPressure) {
                    entry.Value = ini["PRESSURE_" + entry.Id].GetIntNullable("VALUE");
                }
            } catch (Exception e) {
                Logging.Warning("[CarSetupObject] Can’t read file: " + e);
                AddError(AcErrorType.Data_IniIsDamaged, FileName);
            }
        }

        protected override void Rename() {
            FileUtils.Move(Location, FileAcManager.Directories.GetLocation(Path.Combine(Track?.Id ?? GenericDirectory, Name + Extension), Enabled));
        }

        public override void Save() {
            try {
                var ini = new IniFile(Location);
                ini["TYRES"].Set("VALUE", Tyres);
                ini["FUEL"].Set("VALUE", Fuel);

                foreach (var entry in TyresPressure) {
                    ini["PRESSURE_" + entry.Id].Set("VALUE", entry.Value);
                }

                ini.SaveAs(Location);
            } catch (Exception e) {
                Logging.Warning("[CarSetupObject] Can’t save file: " + e);
            }

            base.Save();
            Changed = false;
        }

        public override string DisplayName => Track == null ? Name : $"{Name} ({Track.DisplayName})";

        public override bool HasData => true;
    }
}
