using System.Collections.Generic;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private readonly List<string> _origins = new List<string>(2);

        public string Origins => _origins.JoinToString(',');

        public void SetOrigin(string key) {
            if (!_origins.Contains(key)) {
                _origins.Add(key);
                OnPropertyChanged(nameof(Origins));

                switch (key) {
                    case LanOnlineSource.Key:
                        OriginsFromLan = true;
                        break;
                    case KunosOnlineSource.Key:
                        OriginsFromKunos = true;
                        break;
                    case MinoratingOnlineSource.Key:
                        OriginsFromMinorating = true;
                        break;
                }
            }
        }

        /// <summary>
        /// Remove origin from origins list.
        /// </summary>
        /// <param name="key">Origin (aka source) key.</param>
        /// <returns>True if it was the only origin and now its list is empty.</returns>
        public bool RemoveOrigin(string key) {
            if (_origins.Remove(key)) {
                OnPropertyChanged(nameof(Origins));

                switch (key) {
                    case LanOnlineSource.Key:
                        OriginsFromLan = false;
                        break;
                    case KunosOnlineSource.Key:
                        OriginsFromKunos = false;
                        break;
                    case MinoratingOnlineSource.Key:
                        OriginsFromMinorating = false;
                        break;
                }
            }

            return _origins.Count == 0;
        }

        public bool OriginsFrom(string source) {
            for (var j = 0; j < _origins.Count; j++) {
                if (source == _origins[j]) return true;
            }
            return false;
        }

        public bool OriginsFrom(string[] sources) {
            for (var j = 0; j < _origins.Count; j++) {
                for (var i = 0; i < sources.Length; i++) {
                    if (sources[i] == _origins[j]) return true;
                }
            }
            return false;
        }

        private Task<ServerInformation> GetInformation() {
            if (!SettingsHolder.Online.LoadServerInformationDirectly) {
                if (OriginsFromKunos) {
                    return Task.Run(() => KunosApiProvider.TryToGetInformation(Ip, PortC));
                }
            }

            return KunosApiProvider.TryToGetInformationDirectAsync(Ip, PortC);
        }

        #region Kunos-specific
        private bool _originsFromKunos;

        public bool OriginsFromKunos {
            get { return _originsFromKunos; }
            set {
                if (Equals(value, _originsFromKunos)) return;
                _originsFromKunos = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region LAN-related
        private bool _originsFromLan;

        public bool OriginsFromLan {
            get { return _originsFromLan; }
            set {
                if (Equals(value, _originsFromLan)) return;
                _originsFromLan = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Minorating-related
        private bool _originsFromMinorating;

        public bool OriginsFromMinorating {
            get { return _originsFromMinorating; }
            set {
                if (Equals(value, _originsFromMinorating)) return;
                _originsFromMinorating = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}
