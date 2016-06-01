using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public interface IEntry : IWithId {
        string DisplayName { get; }

        WaitingFor WaitingFor { get; }

        bool Waiting { get; }

        void Clear();
    }
}