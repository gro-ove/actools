using System;
using System.ComponentModel;
using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class UserChampionshipRules : NotifyPropertyChanged {
        private double _practice = 90d;

        [JsonProperty(@"practice")]
        public double Practice {
            get { return _practice; }
            set {
                if (Equals(value, _practice)) return;
                _practice = value;
                OnPropertyChanged();
            }
        }

        private double _qualifying = 60d;

        [JsonProperty(@"qualifying")]
        public double Qualifying {
            get { return _qualifying; }
            set {
                if (Equals(value, _qualifying)) return;
                _qualifying = value;
                OnPropertyChanged();
            }
        }

        private int[] _points = { 10, 8, 6, 3, 2, 1 };

        [JsonProperty(@"points")]
        public int[] Points {
            get { return _points; }
            set {
                if (Equals(value, _points)) return;
                _points = value;
                OnPropertyChanged();
            }
        }

        private bool _penalties = true;

        [JsonProperty(@"penalties")]
        public bool Penalties {
            get { return _penalties; }
            set {
                if (Equals(value, _penalties)) return;
                _penalties = value;
                OnPropertyChanged();
            }
        }

        private Game.JumpStartPenaltyType _jumpStartPenalty = Game.JumpStartPenaltyType.DriveThrough;

        [JsonProperty(@"jumpstart")]
        public Game.JumpStartPenaltyType JumpStartPenalty {
            get { return _jumpStartPenalty; }
            set {
                if (Equals(value, _jumpStartPenalty)) return;
                _jumpStartPenalty = value;
                OnPropertyChanged();
            }
        }
    }

    [Localizable(false)]
    public class UserChampionshipDriver : NotifyPropertyChanged {
        public const string PlayerName = "PLAYER";

        private string _name;

        [JsonProperty(@"name")]
        public string Name {
            get { return _name; }
            set {
                if (Equals(value, _name)) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        public bool IsPlayer => _name == PlayerName;

        private string _carId;

        [JsonProperty(@"car")]
        public string CarId {
            get { return _carId; }
            set {
                if (Equals(value, _carId)) return;
                _carId = value;
                OnPropertyChanged();
            }
        }

        private string _skinId;

        [JsonProperty(@"skin")]
        public string SkinId {
            get { return _skinId; }
            set {
                if (Equals(value, _skinId)) return;
                _skinId = value;
                OnPropertyChanged();
            }
        }
    }

    [Localizable(false)]
    public class UserChampionshipRound : NotifyPropertyChanged {
        private string _trackId;

        public string TrackId {
            get { return _trackId; }
            set {
                if (Equals(value, _trackId)) return;
                _trackId = value;
                OnPropertyChanged();
            }
        }

        private int _lapsCount;

        public int LapsCount {
            get { return _lapsCount; }
            set {
                if (Equals(value, _lapsCount)) return;
                _lapsCount = value;
                OnPropertyChanged();
            }
        }

        private int _weather;

        public int Weather {
            get { return _weather; }
            set {
                if (Equals(value, _weather)) return;
                _weather = value;
                OnPropertyChanged();
            }
        }

        private int _surface;

        public int Surface {
            get { return _surface; }
            set {
                if (Equals(value, _surface)) return;
                _surface = value;
                OnPropertyChanged();
            }
        }
    }

    public class UserChampionshipObject : AcCommonSingleFileObject {
        public const string FileExtension = ".champ";

        public override string Extension => FileExtension;

        public UserChampionshipObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) { }

        protected override void InitializeLocations() {
            base.InitializeLocations();
            AdditionalInformation = Location + @".custom";
        }

        public string AdditionalInformation { get; private set; }

        public override bool HasData => true;

        private JObject _jsonObject;

        private UserChampionshipRules _rules;

        public UserChampionshipRules Rules {
            get { return _rules; }
            set {
                if (Equals(value, _rules)) return;
                _rules = value;
                OnPropertyChanged();
            }
        }

        private UserChampionshipDriver[] _drivers;

        public UserChampionshipDriver[] Drivers {
            get { return _drivers; }
            set {
                if (Equals(value, _drivers)) return;
                _drivers = value;
                OnPropertyChanged();
            }
        }

        private UserChampionshipRound[] _rounds;

        public UserChampionshipRound[] Rounds {
            get { return _rounds; }
            set {
                if (Equals(value, _rounds)) return;
                _rounds = value;
                OnPropertyChanged();
            }
        }

        private int _maxCars;

        public int MaxCars {
            get { return _maxCars; }
            set {
                if (Equals(value, _maxCars)) return;
                _maxCars = value;
                OnPropertyChanged();
            }
        }

        private string _serializedRaceGridData;

        public string SerializedRaceGridData {
            get { return _serializedRaceGridData; }
            set {
                if (Equals(value, _serializedRaceGridData)) return;
                _serializedRaceGridData = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadOrThrow() {
            // Base version would load object’s name from it’s filename, we don’t need this

            try {
                _jsonObject = JsonExtension.Parse(Location);

                Name = _jsonObject.GetStringValueOnly("name");
                MaxCars = _jsonObject.GetIntValueOnly("maxCars") ?? -1;
                Rules = _jsonObject[@"rules"].ToObject<UserChampionshipRules>() ?? new UserChampionshipRules();
                Drivers = _jsonObject[@"opponents"].ToObject<UserChampionshipDriver[]>() ?? new [] {
                    new UserChampionshipDriver { CarId = "abarth500", SkinId = "red_white", Name = UserChampionshipDriver.PlayerName } 
                };
                Rounds = _jsonObject[@"rounds"].ToObject<UserChampionshipRound[]>() ?? new [] {
                    new UserChampionshipRound { TrackId = "magione", LapsCount = 10, Weather = 4, Surface = 3 }
                };
            } catch (Exception e) {
                Logging.Warning(e);
                AddError(AcErrorType.Data_JsonIsDamaged, Path.GetFileName(Location));
                Name = Id.ApartFromLast(Extension);
                MaxCars = -1;
                Rules = new UserChampionshipRules();
                Drivers = new UserChampionshipDriver[0];
                Rounds = new UserChampionshipRound[0];
                return;
            }

            try {
            } catch (Exception e) {
                
            }
        }

        public override void Save() {
            // Base version would rename file if name changed, we don’t need this
        }

        public override bool HandleChangedFile(string filename) {
            if (string.Equals(filename, Location, StringComparison.OrdinalIgnoreCase)) {
                // Content = File.ReadAllText(Location);
            }

            return true;
        }
    }
}