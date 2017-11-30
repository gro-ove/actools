using AcManager.Tools.Miscellaneous;

namespace AcManager.Tools.Objects {
    public partial class TrackObject : ICupSupportedObject {
        string ICupSupportedObject.InstalledVersion => Version;
        public bool IsCupUpdateAvailable => CupClient.Instance.ContainsAnUpdate(CupContentType.Car, Id, Version);
        public CupContentType CupContentType => CupContentType.Track;
        public CupClient.CupInformation CupUpdateInformation => CupClient.Instance.GetInformation(CupContentType.Car, Id);

        void ICupSupportedObject.OnCupUpdateAvailableChanged() {
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        protected override void OnVersionChanged() {
            OnPropertyChanged(nameof(ICupSupportedObject.InstalledVersion));
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        public void SetValues(string author, string informationUrl, string version) {
            Author = author;
            Url = informationUrl;
            Version = version;
            Save();
        }
    }
}