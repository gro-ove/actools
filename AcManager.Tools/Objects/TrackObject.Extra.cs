using AcManager.Tools.Miscellaneous;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Objects {
    public partial class TrackObject : ICupSupportedObject {
        public CupContentType CupContentType => CupContentType.Track;
        public bool IsCupUpdateAvailable => CupClient.Instance?.ContainsAnUpdate(CupContentType, Id, Version) ?? false;
        public CupClient.CupInformation CupUpdateInformation => CupClient.Instance?.GetInformation(CupContentType, Id);

        protected override void OnVersionChanged() {
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        void ICupSupportedObject.OnCupUpdateAvailableChanged() {
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        void ICupSupportedObject.SetValues(string author, string informationUrl, string version) {
            Author = author;
            Url = informationUrl;
            Version = version;
            SaveAsync().Ignore();
        }
    }
}