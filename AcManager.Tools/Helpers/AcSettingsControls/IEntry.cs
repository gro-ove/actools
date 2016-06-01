using AcTools.Utils.Helpers;

namespace AcManager.Tools.Helpers.AcSettingsControls {
    public interface IEntry : IWithId {
        string DisplayName { get; }

        bool Waiting { get; set; }

        void Clear();
    }
}