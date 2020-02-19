using AcManager.Tools.AcPlugins.Info;

namespace AcManager.Tools.AcPlugins.Helpers {
    public interface ISessionReportHandler {
        void HandleReport(SessionInfo report);
    }
}