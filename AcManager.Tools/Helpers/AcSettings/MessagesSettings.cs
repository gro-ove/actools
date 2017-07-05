namespace AcManager.Tools.Helpers.AcSettings {
    public class MessagesSettings : IniSettings {
        internal MessagesSettings() : base(@"messages", systemConfig: true) { }

        private bool _newGhost;

        public bool NewGhost {
            get => _newGhost;
            set {
                if (Equals(value, _newGhost)) return;
                _newGhost = value;
                OnPropertyChanged();
            }
        }

        private bool _serverPlayerJoined;

        public bool ServerPlayerJoined {
            get => _serverPlayerJoined;
            set {
                if (Equals(value, _serverPlayerJoined)) return;
                _serverPlayerJoined = value;
                OnPropertyChanged();
            }
        }

        private bool _serverPlayerDisconnected;

        public bool ServerPlayerDisconnected {
            get => _serverPlayerDisconnected;
            set {
                if (Equals(value, _serverPlayerDisconnected)) return;
                _serverPlayerDisconnected = value;
                OnPropertyChanged();
            }
        }

        private bool _serverKickedMsg;

        public bool ServerKickedMsg {
            get => _serverKickedMsg;
            set {
                if (Equals(value, _serverKickedMsg)) return;
                _serverKickedMsg = value;
                OnPropertyChanged();
            }
        }

        private bool _serverSessionVoting;

        public bool ServerSessionVoting {
            get => _serverSessionVoting;
            set {
                if (Equals(value, _serverSessionVoting)) return;
                _serverSessionVoting = value;
                OnPropertyChanged();
            }
        }

        private bool _serverKickVoting;

        public bool ServerKickVoting {
            get => _serverKickVoting;
            set {
                if (Equals(value, _serverKickVoting)) return;
                _serverKickVoting = value;
                OnPropertyChanged();
            }
        }

        private bool _serverVote;

        public bool ServerVote {
            get => _serverVote;
            set {
                if (Equals(value, _serverVote)) return;
                _serverVote = value;
                OnPropertyChanged();
            }
        }

        private bool _abs;

        public bool Abs {
            get => _abs;
            set {
                if (Equals(value, _abs)) return;
                _abs = value;
                OnPropertyChanged();
            }
        }

        private bool _tractionControl;

        public bool TractionControl {
            get => _tractionControl;
            set {
                if (Equals(value, _tractionControl)) return;
                _tractionControl = value;
                OnPropertyChanged();
            }
        }

        private bool _turbo;

        public bool Turbo {
            get => _turbo;
            set {
                if (Equals(value, _turbo)) return;
                _turbo = value;
                OnPropertyChanged();
            }
        }

        private bool _brakeBias;

        public bool BrakeBias {
            get => _brakeBias;
            set {
                if (Equals(value, _brakeBias)) return;
                _brakeBias = value;
                OnPropertyChanged();
            }
        }

        private bool _brakeEngine;

        public bool BrakeEngine {
            get => _brakeEngine;
            set {
                if (Equals(value, _brakeEngine)) return;
                _brakeEngine = value;
                OnPropertyChanged();
            }
        }

        private bool _mgu;

        public bool Mgu {
            get => _mgu;
            set {
                if (Equals(value, _mgu)) return;
                _mgu = value;
                OnPropertyChanged();
            }
        }

        private bool _penaltyLockControl;

        public bool PenaltyLockControl {
            get => _penaltyLockControl;
            set {
                if (Equals(value, _penaltyLockControl)) return;
                _penaltyLockControl = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            var section = Ini["SYSTEM"];
            NewGhost = section.GetBool("NEW_GHOST", true);
            ServerPlayerJoined = section.GetBool("SERVER_PLAYER_JOINED", true);
            ServerPlayerDisconnected = section.GetBool("SERVER_PLAYER_DISCONNECTED", true);
            ServerKickedMsg = section.GetBool("SERVER_KICKED_MSG", true);
            ServerSessionVoting = section.GetBool("SERVER_SESSION_VOTING", true);
            ServerKickVoting = section.GetBool("SERVER_KICK_VOTING", true);
            ServerVote = section.GetBool("SERVER_VOTE", true);
            Abs = section.GetBool("ABS", true);
            TractionControl = section.GetBool("TC", true);
            Turbo = section.GetBool("TURBO", true);
            BrakeBias = section.GetBool("BRAKE_BIAS", true);
            BrakeEngine = section.GetBool("BRAKE_ENGINE", true);
            Mgu = section.GetBool("MGU", true);
            PenaltyLockControl = Ini["PENALTY"].GetBool("LOCK_CONTROL", true);
        }

        protected override void SetToIni() {
            var section = Ini["SYSTEM"];
            section.Set("NEW_GHOST", NewGhost);
            section.Set("SERVER_PLAYER_JOINED", ServerPlayerJoined);
            section.Set("SERVER_PLAYER_DISCONNECTED", ServerPlayerDisconnected);
            section.Set("SERVER_KICKED_MSG", ServerKickedMsg);
            section.Set("SERVER_SESSION_VOTING", ServerSessionVoting);
            section.Set("SERVER_KICK_VOTING", ServerKickVoting);
            section.Set("SERVER_VOTE", ServerVote);
            section.Set("ABS", Abs);
            section.Set("TC", TractionControl);
            section.Set("TURBO", Turbo);
            section.Set("BRAKE_BIAS", BrakeBias);
            section.Set("BRAKE_ENGINE", BrakeEngine);
            section.Set("MGU", Mgu);
            Ini["PENALTY"].Set("LOCK_CONTROL", PenaltyLockControl);
        }
    }
}