using System;
using System.IO;
using System.Text;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Objects {
    internal sealed class ReplayReader : BinaryReader {
        public ReplayReader(string filename)
            : this(File.Open(filename, FileMode.Open, FileAccess.Read)) { }

        public ReplayReader(Stream input)
            : base(input) { }

        public string ReadString(int limit) {
            var length = ReadInt32();
            if (length > limit) {
                throw new Exception("Not a string");
            }

            return Encoding.ASCII.GetString(ReadBytes(length));
        }

        public override string ReadString() {
            return ReadString(256);
        }
    }

    public class ReplayObject : AcCommonSingleFileObject {
        public const string ReplayExtension = ".acreplay";

        public override string Extension => ReplayExtension;

        public ReplayObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
        }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();

            Date = File.GetLastWriteTime(Location);

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
                }
                ParsedSuccessfully = true;
            } catch (Exception) {
                ParsedSuccessfully = false;
                throw;
            }
        }

        public override string DisplayName => Filename == "cr" && Name == "cr" ? "Previous Online Session" : base.DisplayName;

        public override int CompareTo(AcPlaceholderNew o) {
            var or = o as ReplayObject;
            return or != null ? Date.CompareTo(or.Date) : base.CompareTo(o);
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

        private DateTime _date;

        public DateTime Date {
            get { return _date; }
            set {
                if (Equals(value, _date)) return;
                _date = value;
                OnPropertyChanged(nameof(Date));
                SortAffectingValueChanged();
            }
        }
        #endregion
    }
}
