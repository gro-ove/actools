using System.IO;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public enum ServerPresetAssistState {
        Denied = 0,
        Factory = 1,
        Forced = 2
    }

    public class ServerPresetObject : AcIniObject {
        public ServerPresetObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) {}

        protected override void InitializeLocations() {
            base.InitializeLocations();
            IniFilename = Path.Combine(Location, "server_cfg.ini");
            EntryListIniFilename = Path.Combine(Location, "entry_list.ini");
            ResultsDirectory = Path.Combine(Location, "results");
        }

        public string EntryListIniFilename { get; private set; }

        public string ResultsDirectory { get; private set; }

        #region Data
        private string _trackId;

        public string TrackId {
            get { return _trackId; }
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (Equals(value, _trackId)) return;
                _trackId = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    // TODO: missing track id error
                }
            }
        }
        
        private string[] _carIds;

        [NotNull]
        public string[] CarIds {
            get { return _carIds; }
            set {
                if (Equals(value, _carIds) || _carIds != null && value.Length == _carIds.Length &&
                        value.All((x, i) => Equals(_carIds[i], x))) return;
                _carIds = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    // TODO: missing car ids error
                }
            }
        }

        private string _trackLayoutId;

        public string TrackLayoutId {
            get { return _trackLayoutId; }
            set {
                if (string.IsNullOrWhiteSpace(value)) value = null;
                if (Equals(value, _trackLayoutId)) return;
                _trackLayoutId = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _sendIntervalHz;

        public int SendIntervalHz {
            get { return _sendIntervalHz; }
            set {
                value = value.Clamp(10, 35);
                if (Equals(value, _sendIntervalHz)) return;
                _sendIntervalHz = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _capacity;

        public int Capacity {
            get { return _capacity; }
            set {
                value = value.Clamp(1, 200);
                if (Equals(value, _capacity)) return;
                _capacity = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _threads;

        public int Threads {
            get { return _threads; }
            set {
                value = value.Clamp(2, 48);
                if (Equals(value, _threads)) return;
                _threads = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _udpPort;

        public int UdpPort {
            get { return _udpPort; }
            set {
                value = value.Clamp(1, 65535);
                if (Equals(value, _udpPort)) return;
                _udpPort = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _tcpPort;

        public int TcpPort {
            get { return _tcpPort; }
            set {
                value = value.Clamp(1, 65535);
                if (Equals(value, _tcpPort)) return;
                _tcpPort = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int _httpPort;

        public int HttpPort {
            get { return _httpPort; }
            set {
                value = value.Clamp(1, 65535);
                if (Equals(value, _httpPort)) return;
                _httpPort = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _showOnLobby;

        public bool ShowOnLobby {
            get { return _showOnLobby; }
            set {
                if (Equals(value, _showOnLobby)) return;
                _showOnLobby = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _loopMode;

        public bool LoopMode {
            get { return _loopMode; }
            set {
                if (Equals(value, _loopMode)) return;
                _loopMode = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _pickupMode;

        public bool PickupMode {
            get { return _pickupMode; }
            set {
                if (Equals(value, _pickupMode)) return;
                _pickupMode = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _password;

        public string Password {
            get { return _password; }
            set {
                if (Equals(value, _password)) return;
                _password = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private string _adminPassword;

        public string AdminPassword {
            get { return _adminPassword; }
            set {
                if (Equals(value, _adminPassword)) return;
                _adminPassword = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private ServerPresetAssistState _abs;

        public ServerPresetAssistState Abs {
            get { return _abs; }
            set {
                if (Equals(value, _abs)) return;
                _abs = value;
                OnPropertyChanged();
            }
        }

        private ServerPresetAssistState _tractionControl;

        public ServerPresetAssistState TractionControl {
            get { return _tractionControl; }
            set {
                if (Equals(value, _tractionControl)) return;
                _tractionControl = value;
                OnPropertyChanged();
            }
        }

        private bool _stabilityControl;

        public bool StabilityControl {
            get { return _stabilityControl; }
            set {
                if (Equals(value, _stabilityControl)) return;
                _stabilityControl = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _autoClutch;

        public bool AutoClutch {
            get { return _autoClutch; }
            set {
                if (Equals(value, _autoClutch)) return;
                _autoClutch = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _tyreBlankets;

        public bool TyreBlankets {
            get { return _tyreBlankets; }
            set {
                if (Equals(value, _tyreBlankets)) return;
                _tyreBlankets = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private bool _forceVirtualMirror;

        public bool ForceVirtualMirror {
            get { return _forceVirtualMirror; }
            set {
                if (Equals(value, _forceVirtualMirror)) return;
                _forceVirtualMirror = value;
                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }
        #endregion

        protected override void LoadData(IniFile ini) {
            var section = ini["SERVER"];
            Name = section.Get("NAME");
            Password = section.Get("PASSWORD");
            AdminPassword = section.Get("ADMIN_PASSWORD");
            ShowOnLobby = section.GetBool("REGISTER_TO_LOBBY", true);
            LoopMode = section.GetBool("LOOP_MODE", true);
            PickupMode = section.GetBool("PICKUP_MODE_ENABLED", false);
            Capacity = section.GetInt("MAX_CLIENTS", 3);

            UdpPort = section.GetInt("UDP_PORT", 9600);
            TcpPort = section.GetInt("TCP_PORT", 9600);
            HttpPort = section.GetInt("HTTP_PORT", 8081);
            SendIntervalHz = section.GetInt("CLIENT_SEND_INTERVAL_HZ", 18);
            Threads = section.GetInt("NUM_THREADS", 2);

            TrackId = section.Get("TRACK");
            TrackLayoutId = section.Get("CONFIG_TRACK");
            CarIds = section.GetStrings("CARS", ';');

            Abs = section.GetIntEnum("ABS_ALLOWED", ServerPresetAssistState.Factory);
            TractionControl = section.GetIntEnum("TC_ALLOWED", ServerPresetAssistState.Factory);
            StabilityControl = section.GetBool("STABILITY_ALLOWED", false);
            AutoClutch = section.GetBool("AUTOCLUTCH_ALLOWED", false);
            TyreBlankets = section.GetBool("TYRE_BLANKETS_ALLOWED", false);
            ForceVirtualMirror = section.GetBool("FORCE_VIRTUAL_MIRROR", true);
        }

        public override void SaveData(IniFile ini) {
            var section = ini["SERVER"];
            section.Set("NAME", Name);
            section.Set("PASSWORD", Password);
            section.Set("ADMIN_PASSWORD", AdminPassword);
            section.Set("REGISTER_TO_LOBBY", ShowOnLobby);
            section.Set("LOOP_MODE", LoopMode);
            section.Set("PICKUP_MODE_ENABLED", PickupMode);
            section.Set("MAX_CLIENTS", Capacity);

            section.Set("UDP_PORT", UdpPort);
            section.Set("TCP_PORT", TcpPort);
            section.Set("HTTP_PORT", HttpPort);
            section.Set("CLIENT_SEND_INTERVAL_HZ", SendIntervalHz);
            section.Set("NUM_THREADS", Threads);

            section.Set("TRACK", TrackId);
            section.Set("CONFIG_TRACK", TrackLayoutId);
            section.Set("CARS", CarIds, ';');

            section.SetIntEnum("ABS_ALLOWED", Abs);
            section.SetIntEnum("TC_ALLOWED", TractionControl);
            section.Set("STABILITY_ALLOWED", StabilityControl);
            section.Set("AUTOCLUTCH_ALLOWED", AutoClutch);
            section.Set("TYRE_BLANKETS_ALLOWED", TyreBlankets);
            section.Set("FORCE_VIRTUAL_MIRROR", ForceVirtualMirror);
        }
    }
}
