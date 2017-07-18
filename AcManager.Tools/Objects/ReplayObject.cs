using System;
using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class ReplayObject : AcCommonSingleFileObject {
        public static string PreviousReplayName { get; } = @"cr";
        public static string AutosaveCategory { get; } = @"temp";
        public const string ReplayExtension = ".acreplay";

        public override string Extension => ReplayExtension;

        [CanBeNull]
        public string Category { get; }

        private string _editableCategory;

        [CanBeNull]
        public string EditableCategory {
            get => _editableCategory;
            set {
                value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (Equals(value, _editableCategory)) return;
                _editableCategory = value;
                OnPropertyChanged();
                if (value != Category) {
                    Changed = true;
                }
            }
        }

        public ReplayObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            Category = Path.GetDirectoryName(id);
            EditableCategory = Category;
            if (string.IsNullOrWhiteSpace(Category)) Category = null;
        }

        protected override string GetNewId(string newName) {
            var fileName = SettingsHolder.Drive.AutoAddReplaysExtension ? newName + Extension : newName;
            return EditableCategory == null ? fileName : Path.Combine(EditableCategory, fileName);
        }

        public override bool HandleChangedFile(string filename) {
            if (string.Equals(filename, Location, StringComparison.OrdinalIgnoreCase)) {
                Reload();
            }

            return true;
        }

        public bool IsAutoSave => !AcSettingsHolder.Replay.Autosave
                ? Category == null && Id == PreviousReplayName : string.Equals(Category, AutosaveCategory, StringComparison.OrdinalIgnoreCase);

        public override string Name {
            get => base.Name;
            protected set {
                ErrorIf(value == null || value.Contains("[") || value.Contains("]"), AcErrorType.Replay_InvalidName);
                base.Name = value;
            }
        }

        public override void Save() {
            if (EditableCategory != Category) {
                RenameAsync();
            }

            base.Save();
        }

        /*public async Task ReplaceWeather(string newWeatherId) {
            var temporary = FileUtils.EnsureUnique(Path.Combine(Path.GetDirectoryName(Location) ?? "", "tmp"));
            using (var reader = new ReplayReader(Location)) {
                var version = reader.ReadInt32();
                if (version < 14) {
                    throw new Exception("Unsupported version of replay");
                }

                var something = reader.ReadBytes(8);
                reader.ReadString();

                using (var stream = File.Open(temporary, FileMode.CreateNew, FileAccess.ReadWrite))
                using (var updated = new BinaryWriter(File.Open(temporary, FileMode.CreateNew, FileAccess.ReadWrite), Encoding.ASCII, true)) {
                    updated.Write(version);
                    updated.Write(something);
                    updated.Write(newWeatherId.Length);
                    updated.Write(Encoding.ASCII.GetBytes(newWeatherId));
                    await reader.CopyToAsync(stream).ConfigureAwait(false);
                }

                FileUtils.Recycle(Location);
                File.Move(temporary, Location);
            }
        }*/

        protected override void LoadOrThrow() {
            EditableCategory = Category;
            OldName = Path.GetFileName(Id).ApartFromLast(Extension, StringComparison.OrdinalIgnoreCase);
            Name = OldName;

            var fileInfo = new FileInfo(Location);
            if (!fileInfo.Exists) {
                throw new AcErrorException(this, AcErrorType.Load_Base, "File appears to be deleted");
            }

            Size = fileInfo.Length;

            if (Id == PreviousReplayName) {
                IsNew = true;
            }

            if (!SettingsHolder.Drive.TryToLoadReplays) return;

            try {
                using (var reader = new ReplayReader(Location)) {
                    var version = reader.ReadInt32();

                    if (version >= 14) {
                        reader.Skip(8);

                        WeatherId = reader.ReadString();
                        /*if (!string.IsNullOrWhiteSpace(WeatherId)) {
                            ErrorIf(WeatherManager.Instance.GetWrapperById(WeatherId) == null,
                                    AcErrorType.Replay_WeatherIsMissing, WeatherId);
                        }*/

                        TrackId = reader.ReadString();
                        TrackConfiguration = reader.ReadString();
                    } else {
                        TrackId = reader.ReadString();
                    }

                    ErrorIf(TracksManager.Instance.GetWrapperById(TrackId) == null,
                            AcErrorType.Replay_TrackIsMissing, TrackId);

                    CarId = reader.TryToReadNextString();

                    if (!string.IsNullOrWhiteSpace(CarId)) {
                        ErrorIf(CarsManager.Instance.GetWrapperById(CarId) == null,
                                AcErrorType.Replay_CarIsMissing, CarId);
                    }

                    try {
                        DriverName = reader.ReadString();
                        reader.ReadInt64();
                        CarSkinId = reader.ReadString();
                    } catch (Exception) {
                        // ignored
                    }
                }

                ParsedSuccessfully = true;
            } catch (Exception e) {
                ParsedSuccessfully = false;
                throw new AcErrorException(this, AcErrorType.Load_Base, e);
            }
        }

        public override string DisplayName => IsAutoSave && Name == PreviousReplayName
                ? ToolsStrings.ReplayObject_PreviousSession
                : base.DisplayName;

        public override int CompareTo(AcPlaceholderNew o) {
            var or = o as ReplayObject;



            var r = o as ReplayObject;
            if (r == null) return base.CompareTo(o);

            var tp = Category;
            var rp = r.Category;

            if (tp != rp) {
                if (tp == AutosaveCategory) return -1;
                if (rp == AutosaveCategory) return 1;
                if (tp == null) return 1;
                if (rp == null) return -1;
                return AlphanumComparatorFast.Compare(tp, rp);
            }

            return CreationDateTime.CompareTo(or.CreationDateTime);
        }

        #region Simple Properties
        public override bool HasData => ParsedSuccessfully;

        private bool _parsedSuccessfully;

        public bool ParsedSuccessfully {
            get => _parsedSuccessfully;
            set {
                if (Equals(value, _parsedSuccessfully)) return;
                _parsedSuccessfully = value;
                OnPropertyChanged(nameof(ParsedSuccessfully));
                OnPropertyChanged(nameof(HasData));
            }
        }

        private string _weatherId;

        public string WeatherId {
            get => _weatherId;
            set {
                if (Equals(value, _weatherId)) return;
                _weatherId = value;
                OnPropertyChanged(nameof(WeatherId));
            }
        }

        private string _carId;

        public string CarId {
            get => _carId;
            set {
                if (Equals(value, _carId)) return;
                _carId = value;
                OnPropertyChanged();
            }
        }

        private string _carSkinId;

        public string CarSkinId {
            get => _carSkinId;
            set {
                if (Equals(value, _carSkinId)) return;
                _carSkinId = value;
                OnPropertyChanged();
            }
        }

        private string _driverName;

        public string DriverName {
            get => _driverName;
            set {
                if (Equals(value, _driverName)) return;
                _driverName = value;
                OnPropertyChanged();
            }
        }

        private string _trackId;

        public string TrackId {
            get => _trackId;
            set {
                if (Equals(value, _trackId)) return;
                _trackId = value;
                OnPropertyChanged(nameof(TrackId));
            }
        }

        private string _trackConfiguration;

        public string TrackConfiguration {
            get => _trackConfiguration;
            set {
                if (Equals(value, _trackConfiguration)) return;
                _trackConfiguration = value;
                OnPropertyChanged(nameof(TrackConfiguration));
            }
        }

        private long _size;

        public long Size {
            get => _size;
            set {
                if (Equals(value, _size)) return;
                _size = value;
                OnPropertyChanged();
            }
        }
        #endregion

        // Bunch of temporary fields for filtering

        private CarObject _car;

        [CanBeNull]
        internal CarObject Car => _car ?? (_car = CarId == null ? null : CarsManager.Instance.GetById(CarId));

        private CarSkinObject _carSkin;

        [CanBeNull]
        internal CarSkinObject CarSkin => _carSkin ?? (_carSkin = CarSkinId == null ? null : Car?.GetSkinById(CarSkinId));

        private WeatherObject _weather;

        [CanBeNull]
        internal WeatherObject Weather => _weather ?? (_weather = WeatherId == null ? null : WeatherManager.Instance.GetById(WeatherId));

        private TrackObjectBase _track;

        [CanBeNull]
        internal TrackObjectBase Track => _track ?? (_track = TrackId == null ? null : TrackConfiguration == null
                ? TracksManager.Instance.GetById(TrackId) : TracksManager.Instance.GetLayoutById(TrackId, TrackConfiguration));
    }
}
