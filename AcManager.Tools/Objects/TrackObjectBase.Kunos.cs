using System.Linq;
using AcManager.Tools.Data;

namespace AcManager.Tools.Objects {
    public abstract partial class TrackObjectBase {
        protected override KunosDlcInformation GetDlc() {
            var dlcs = DataProvider.Instance.DlcInformations;
            for (var i = dlcs.Length - 1; i >= 0; i--) {
                var dlc = dlcs[i];
                if (dlc.Tracks.Contains(Id)) {
                    return dlc;
                }
            }

            return null;
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
