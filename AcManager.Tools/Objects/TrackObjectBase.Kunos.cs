using System.Linq;
using AcManager.Tools.Data;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public abstract partial class TrackObjectBase {
        private KunosDlcInformation _dlc;
        private bool _dlcSet;

        [CanBeNull]
        public KunosDlcInformation Dlc {
            get {
                if (!_dlcSet) {
                    _dlcSet = true;

                    if (Author == AuthorKunos) {
                        var dlcs = DataProvider.Instance.DlcInformations;
                        for (var i = dlcs.Length - 1; i >= 0; i--) {
                            var dlc = dlcs[i];
                            if (dlc.Tracks.Contains(Id)) {
                                _dlc = dlc;
                                break;
                            }
                        }
                    }
                }

                return _dlc;
            }
        }

        public override string VersionInfoDisplay {
            get {
                var dlc = Dlc;
                return dlc != null ? $@"{AuthorKunos} ([url=""http://store.steampowered.com/app/{dlc.Id}/""]{dlc.ShortName}[/url])" :
                        base.VersionInfoDisplay;
            }
        }
    }
}
