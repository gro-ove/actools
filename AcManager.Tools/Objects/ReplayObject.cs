using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    internal sealed class ReplayReader : BinaryReader {
        public ReplayReader(string filename)
            : this(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096)) { }

        public ReplayReader(Stream input)
            : base(input) { }

        public string ReadString(int limit) {
            var length = ReadInt32();
            if (length > limit) {
                throw new Exception(Resources.ReplayReader_UnsupportedFormat);
            }

            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsStringCharacter(int c) {
            return c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c >= 'A' && c <= 'Z';
        }

        [CanBeNull]
        public string TryToReadNextString() {
            var stream = BaseStream;

            const int l = 2048;
            var b = new byte[l + 4];
            int bytesRead;
            while ((bytesRead = stream.Read(b, 4, l)) > 0) {
                var i = 3;
                while (i < bytesRead) {
                    if (!IsStringCharacter(b[i])) {
                        i += 4;
                    } else if (!IsStringCharacter(b[i - 1])) {
                        i += 3;
                    } else if (!IsStringCharacter(b[i - 2])) {
                        i += 2;
                    } else if (!IsStringCharacter(b[i - 3])) {
                        ++i;
                    } else {
                        var sb = new StringBuilder();
                        for (i = i - 3; i < bytesRead; i++) {
                            var n = b[i];
                            if (IsStringCharacter(n)) {
                                sb.Append((char)n);
                            } else {
                                break;
                            }
                        }

                        if (i == bytesRead) {
                            int n;
                            while ((n = stream.ReadByte()) != -1) {
                                if (IsStringCharacter(n)) {
                                    sb.Append((char)n);
                                } else {
                                    BaseStream.Seek(-1, SeekOrigin.Current);
                                    break;
                                }
                            }
                        } else {
                            BaseStream.Seek(i - bytesRead - 4, SeekOrigin.Current);
                        }

                        return sb.ToString();
                    }
                }

                b[0] = b[l];
                b[1] = b[l + 1];
                b[2] = b[l + 2];
                b[3] = b[l + 3];
            }

            return null;
        }

        public override string ReadString() {
            return ReadString(256);
        }
    }

    public class ReplayObject : AcCommonSingleFileObject {
        public static string PreviousReplayName => @"cr";
        public const string ReplayExtension = ".acreplay";

        public override string Extension => ReplayExtension;

        public ReplayObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) { }

        protected override void Rename() {
            Rename(SettingsHolder.Drive.AutoAddReplaysExtension ? Name + Extension : Name);
        }

        public override string Name {
            get { return base.Name; }
            protected set {
                ErrorIf(value == null || value.Contains("[") || value.Contains("]"), AcErrorType.Replay_InvalidName);
                base.Name = value;
            }
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();

            Size = new FileInfo(Location).Length;

            if (Id == PreviousReplayName) {
                IsNew = true;
            }

            if (!SettingsHolder.Drive.TryToLoadReplays) return;

            try {
                using (var reader = new ReplayReader(Location)) {
                    var version = reader.ReadInt32();

                    if (version >= 0xe) {
                        reader.ReadBytes(8);

                        WeatherId = reader.ReadString();
                        TrackId = reader.ReadString();
                        TrackConfiguration = reader.ReadString();
                    } else {
                        TrackId = reader.ReadString();
                    }
                    
                    ErrorIf(TracksManager.Instance.GetWrapperById(TrackId) == null, AcErrorType.Replay_TrackIsMissing, TrackId);

                    CarId = reader.TryToReadNextString();
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

        public override string DisplayName => Id == PreviousReplayName && Name == PreviousReplayName ? Resources.ReplayObject_PreviousSession : base.DisplayName;

        public override int CompareTo(AcPlaceholderNew o) {
            var or = o as ReplayObject;
            return or != null ? CreationTime.CompareTo(or.CreationTime) : base.CompareTo(o);
        }

        #region Simple Properties
        public override bool HasData => ParsedSuccessfully;

        private bool _parsedSuccessfully;

        public bool ParsedSuccessfully {
            get { return _parsedSuccessfully; }
            set {
                if (Equals(value, _parsedSuccessfully)) return;
                _parsedSuccessfully = value;
                OnPropertyChanged(nameof(ParsedSuccessfully));
                OnPropertyChanged(nameof(HasData));
            }
        }

        private string _weatherId;

        public string WeatherId {
            get { return _weatherId; }
            set {
                if (Equals(value, _weatherId)) return;
                _weatherId = value;
                OnPropertyChanged(nameof(WeatherId));
            }
        }

        private string _carId;

        public string CarId {
            get { return _carId; }
            set {
                if (Equals(value, _carId)) return;
                _carId = value;
                OnPropertyChanged();
            }
        }

        private string _carSkinId;

        public string CarSkinId {
            get { return _carSkinId; }
            set {
                if (Equals(value, _carSkinId)) return;
                _carSkinId = value;
                OnPropertyChanged();
            }
        }

        private string _driverName;

        public string DriverName {
            get { return _driverName; }
            set {
                if (Equals(value, _driverName)) return;
                _driverName = value;
                OnPropertyChanged();
            }
        }

        private string _trackId;

        public string TrackId {
            get { return _trackId; }
            set {
                if (Equals(value, _trackId)) return;
                _trackId = value;
                OnPropertyChanged(nameof(TrackId));
            }
        }

        private string _trackConfiguration;

        public string TrackConfiguration {
            get { return _trackConfiguration; }
            set {
                if (Equals(value, _trackConfiguration)) return;
                _trackConfiguration = value;
                OnPropertyChanged(nameof(TrackConfiguration));
            }
        }

        private long _size;

        public long Size {
            get { return _size; }
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

        private TrackBaseObject _track;

        [CanBeNull]
        internal TrackBaseObject Track => _track ?? (_track = TrackId == null ? null : TrackConfiguration == null
                ? TracksManager.Instance.GetById(TrackId) : TracksManager.Instance.GetLayoutById(TrackId, TrackConfiguration));
    }
}
