using System;
using System.IO;
using System.Linq;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject : AcIniObject {
        public ChangeableObservableCollection<ServerSessionEntry> Sessions { get; }
        public ServerSessionEntry[] SimpleSessions { get; }
        public ServerSessionEntry RaceSession { get; }

        public ServerPresetObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {
            SimpleSessions = new[] {
                new ServerSessionEntry("BOOK", ToolsStrings.Session_Booking, false, false),
                new ServerSessionEntry("PRACTICE", ToolsStrings.Session_Practice, true, true),
                new ServerQualificationSessionEntry("QUALIFY", ToolsStrings.Session_Qualification, true, true)
            };

            RaceSession = new ServerRaceSessionEntry("RACE", ToolsStrings.Session_Race, true, true);
            Sessions = new ChangeableObservableCollection<ServerSessionEntry>(SimpleSessions.Append(RaceSession));
            Sessions.ItemPropertyChanged += OnSessionEntryPropertyChanged;
        }

        protected override IniFileMode IniFileMode => IniFileMode.ValuesWithSemicolons;

        protected override void InitializeLocations() {
            base.InitializeLocations();
            IniFilename = Path.Combine(Location, "server_cfg.ini");
            EntryListIniFilename = Path.Combine(Location, "entry_list.ini");
            ResultsDirectory = Path.Combine(Location, "results");
            InitializeWrapperLocations();
        }

        public string EntryListIniFilename { get; private set; }

        public string ResultsDirectory { get; private set; }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            LoadEntryListOrThrow();
            LoadWrapperParams();
        }

        private IniFile _entryListIniObject;

        [CanBeNull]
        public IniFile EntryListIniObject {
            get => _entryListIniObject;
            set {
                if (Equals(value, _entryListIniObject)) return;
                _entryListIniObject = value;
                OnPropertyChanged();
            }
        }

        private void LoadEntryListOrThrow() {
            string text;

            try {
                text = FileUtils.ReadAllText(EntryListIniFilename);
            } catch (FileNotFoundException) {
                AddError(AcErrorType.Data_IniIsMissing, Path.GetFileName(EntryListIniFilename));
                ResetEntryListData();
                return;
            } catch (DirectoryNotFoundException) {
                AddError(AcErrorType.Data_IniIsMissing, Path.GetFileName(EntryListIniFilename));
                ResetEntryListData();
                return;
            }

            try {
                EntryListIniObject = IniFile.Parse(text, IniFileMode);
            } catch (Exception) {
                AddError(AcErrorType.Data_IniIsDamaged, Path.GetFileName(EntryListIniFilename));
                ResetEntryListData();
                return;
            }

            try {
                LoadEntryListData(EntryListIniObject);
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        protected override void LoadData(IniFile ini) {
            foreach (var session in Sessions) {
                session.Load(ini);
            }

            IsPickupModeAvailable = !Sessions.GetById(@"BOOK").IsEnabled;

            var section = ini["SERVER"];
            Name = section.GetPossiblyEmpty("NAME");
            Password = section.GetNonEmpty("PASSWORD");
            AdminPassword = section.GetNonEmpty("ADMIN_PASSWORD");
            ShowOnLobby = section.GetBool("REGISTER_TO_LOBBY", true);
            LoopMode = section.GetBool("LOOP_MODE", true);
            PickupMode = section.GetBool("PICKUP_MODE_ENABLED", true);
            PickupModeLockedEntryList = section.GetBool("LOCKED_ENTRY_LIST", false);
            Capacity = section.GetInt("MAX_CLIENTS", 3);

            UdpPort = section.GetInt("UDP_PORT", 9600);
            TcpPort = section.GetInt("TCP_PORT", 9600);
            HttpPort = section.GetInt("HTTP_PORT", 8081);
            SendIntervalHz = section.GetInt("CLIENT_SEND_INTERVAL_HZ", 18);
            Threads = section.GetInt("NUM_THREADS", 2);

            TrackId = section.GetNonEmpty("TRACK", "imola");
            TrackLayoutId = section.GetNonEmpty("CONFIG_TRACK");
            CarIds = section.GetStrings("CARS", ';');

            Abs = section.GetIntEnum("ABS_ALLOWED", ServerPresetAssistState.Factory);
            TractionControl = section.GetIntEnum("TC_ALLOWED", ServerPresetAssistState.Factory);
            StabilityControl = section.GetBool("STABILITY_ALLOWED", false);
            AutoClutch = section.GetBool("AUTOCLUTCH_ALLOWED", false);
            TyreBlankets = section.GetBool("TYRE_BLANKETS_ALLOWED", false);
            ForceVirtualMirror = section.GetBool("FORCE_VIRTUAL_MIRROR", true);

            FuelRate = section.GetInt("FUEL_RATE", 100);
            DamageRate = section.GetInt("DAMAGE_MULTIPLIER", 100);
            TyreWearRate = section.GetInt("TYRE_WEAR_RATE", 100);
            AllowTyresOut = section.GetInt("ALLOWED_TYRES_OUT", 2);
            MaxBallast = section.GetInt("MAX_BALLAST_KG", 0);
            JumpStart = section.GetIntEnum("START_RULE", ServerPresetJumpStart.CarLocked);
            RaceGasPenaltyDisabled = section.GetBool("RACE_GAS_PENALTY_DISABLED", false);

            SunAngle = section.GetDouble("SUN_ANGLE", 0d);
            TimeMultiplier = section.GetDouble("TIME_OF_DAY_MULT", 1d);
            DynamicTrackEnabled = ini.ContainsKey(@"DYNAMIC_TRACK");
            TrackProperties = Game.TrackProperties.Load(DynamicTrackEnabled ? ini["DYNAMIC_TRACK"] : ini["__CM_DYNAMIC_TRACK_OFF"]);

            KickVoteQuorum = section.GetInt("KICK_QUORUM", 85);
            SessionVoteQuorum = section.GetInt("VOTING_QUORUM", 80);
            VoteDuration = TimeSpan.FromSeconds(section.GetDouble("VOTE_DURATION", 20d));
            BlacklistMode = section.GetBool("BLACKLIST_MODE", true);
            MaxCollisionsPerKm = section.GetInt("MAX_CONTACTS_PER_KM", -1);

            _welcomeMessagePath = null; // thus, forcing loaded message and changed flag to update
            WelcomeMessagePath = ini["DATA"].GetNonEmpty("WELCOME_PATH");

            // At least one weather entry is needed in order to launch the server
            var weather = new ChangeableObservableCollection<ServerWeatherEntry>(ini.GetSections("WEATHER").Select(x => new ServerWeatherEntry(x)));
            if (weather.Count == 0) {
                weather.Add(new ServerWeatherEntry());
            }

            Weather = weather;
        }

        private void LoadEntryListData(IniFile ini) {
            DriverEntries = new ChangeableObservableCollection<ServerPresetDriverEntry>(ini.GetSections("CAR").Select(x => new ServerPresetDriverEntry(x)));
        }

        private void ResetEntryListData() {
            EntryListIniObject = null;
            LoadEntryListData(IniFile.Empty);
        }

        public override void SaveData(IniFile ini) {
            foreach (var session in Sessions) {
                session.Save(ini);
            }

            var section = ini["SERVER"];
            section.Set("NAME", Name);
            section.Set("PASSWORD", Password);
            section.Set("ADMIN_PASSWORD", AdminPassword);
            section.Set("REGISTER_TO_LOBBY", ShowOnLobby);
            section.Set("LOOP_MODE", LoopMode);
            section.Set("PICKUP_MODE_ENABLED", PickupMode);
            section.Set("LOCKED_ENTRY_LIST", PickupModeLockedEntryList);
            section.Set("MAX_CLIENTS", Capacity);

            section.Set("UDP_PORT", UdpPort);
            section.Set("TCP_PORT", TcpPort);
            section.Set("HTTP_PORT", HttpPort);
            section.Set("CLIENT_SEND_INTERVAL_HZ", SendIntervalHz);
            section.Set("NUM_THREADS", Threads);

            section.Set("TRACK", TrackId ?? "");
            section.Set("CONFIG_TRACK", TrackLayoutId ?? "");
            section.Set("CARS", CarIds, ';');

            section.SetIntEnum("ABS_ALLOWED", Abs);
            section.SetIntEnum("TC_ALLOWED", TractionControl);
            section.Set("STABILITY_ALLOWED", StabilityControl);
            section.Set("AUTOCLUTCH_ALLOWED", AutoClutch);
            section.Set("TYRE_BLANKETS_ALLOWED", TyreBlankets);
            section.Set("FORCE_VIRTUAL_MIRROR", ForceVirtualMirror);

            section.Set("FUEL_RATE", FuelRate);
            section.Set("DAMAGE_MULTIPLIER", DamageRate);
            section.Set("TYRE_WEAR_RATE", TyreWearRate);
            section.Set("ALLOWED_TYRES_OUT", AllowTyresOut);
            section.Set("MAX_BALLAST_KG", MaxBallast);
            section.SetIntEnum("START_RULE", JumpStart);
            section.Set("FORCE_VIRTUAL_MIRROR", ForceVirtualMirror);

            section.Set("SUN_ANGLE", SunAngle.RoundToInt());
            section.Set("TIME_OF_DAY_MULT", TimeMultiplier, "F3");
            ini.SetSections("WEATHER", Weather, (e, s) => e.SaveTo(s));
            if (DynamicTrackEnabled) {
                ini.Remove("__CM_DYNAMIC_TRACK_OFF");
                (TrackProperties ?? Game.GetDefaultTrackPropertiesPreset().Properties).Set(ini["DYNAMIC_TRACK"]);
            } else {
                ini.Remove("DYNAMIC_TRACK");
                (TrackProperties ?? Game.GetDefaultTrackPropertiesPreset().Properties).Set(ini["__CM_DYNAMIC_TRACK_OFF"]);
            }

            section.Set("KICK_QUORUM", KickVoteQuorum);
            section.Set("VOTING_QUORUM", SessionVoteQuorum);
            section.Set("VOTE_DURATION", VoteDuration.TotalSeconds.RoundToInt());
            section.Set("BLACKLIST_MODE", BlacklistMode);
            section.Set("MAX_CONTACTS_PER_KM", MaxCollisionsPerKm);

            if (WelcomeMessageChanged) {
                SaveWelcomeMessageCommand.Execute();
            }

            ini["DATA"].Set("WELCOME_PATH", WelcomeMessagePath ?? "");

            var welcomeFilename = Path.Combine(ServerPresetsManager.ServerDirectory, "cfg", $"welcome_{Id}.txt");
            if (!string.IsNullOrWhiteSpace(WelcomeMessage)) {
                FileUtils.EnsureFileDirectoryExists(welcomeFilename);
                File.WriteAllText(welcomeFilename, WelcomeMessage);
                section.Set("WELCOME_MESSAGE", string.IsNullOrWhiteSpace(WelcomeMessagePath) ? "" : $"cfg/{Path.GetFileName(welcomeFilename)}");
            } else {
                if (File.Exists(welcomeFilename)) {
                    FileUtils.Recycle(welcomeFilename);
                }
                section.Remove("WELCOME_MESSAGE");
            }
        }

        public override void Save() {
            var ini = EntryListIniObject ?? IniFile.Empty;
            ini.SetSections("CAR", DriverEntries, (entry, section) => entry.SaveTo(section));
            using ((FileAcManager as IIgnorer)?.IgnoreChanges()) {
                FileUtils.EnsureFileDirectoryExists(EntryListIniFilename);
                File.WriteAllText(EntryListIniFilename, ini.ToString());
                base.Save();
            }

            SaveWrapperParams();
            RemoveError(AcErrorType.Data_IniIsMissing);
        }

        public override bool HandleChangedFile(string filename) {
            var iniChanged = FileUtils.Affects(filename, IniFilename) || FileUtils.Affects(filename, EntryListIniFilename);
            if (iniChanged || FileUtils.Affects(filename, WrapperConfigFilename) || FileUtils.Affects(filename, WrapperContentFilename)) {
                if (!Changed || ModernDialog.ShowMessage(iniChanged ?
                        ToolsStrings.AcObject_ReloadAutomatically_Ini : ToolsStrings.AcObject_ReloadAutomatically_Json,
                        ToolsStrings.AcObject_ReloadAutomatically, MessageBoxButton.YesNo, "autoReload") == MessageBoxResult.Yes) {
                    ClearErrors(AcErrorCategory.Data);
                    LoadOrThrow();
                    Changed = false;
                }

                return true;
            }

            return base.HandleChangedFile(filename);
        }

        private DelegateCommand _randomizeSkinsCommand;

        public DelegateCommand RandomizeSkinsCommand => _randomizeSkinsCommand ?? (_randomizeSkinsCommand = new DelegateCommand(() => {
            var cars = DriverEntries.Select(x => x.CarObject).NonNull().Select(x => new {
                Car = x,
                Skins = GoodShuffle.Get(x.EnabledOnlySkins)
            }).ToList();
            foreach (var d in DriverEntries) {
                d.CarSkinId = cars.FirstOrDefault(x => x.Car == d.CarObject)?.Skins.Next.Id;
            }
        }));

        private DelegateCommand _deleteAllEntriesCommand;

        public DelegateCommand DeleteAllEntriesCommand => _deleteAllEntriesCommand ?? (_deleteAllEntriesCommand = new DelegateCommand(() => {
            DriverEntries.Clear();
        }));
    }
}
