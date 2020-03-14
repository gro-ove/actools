using System.ComponentModel;
using JetBrains.Annotations;

namespace AcManager.Tools.Miscellaneous {
    public interface ICupSupportedObject : INotifyPropertyChanged {
        [NotNull]
        string Id { get; }

        [CanBeNull]
        string InstalledVersion { get; }

        CupContentType CupContentType { get; }

        [CanBeNull]
        CupClient.CupInformation CupUpdateInformation { get; }

        bool IsCupUpdateAvailable { get; }

        void OnCupUpdateAvailableChanged();

        void SetValues(string author, string informationUrl, string version);
    }
}