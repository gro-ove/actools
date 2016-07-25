using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;

namespace AcManager.Tools.ContentInstallation.Types {
    internal class TypeWeather : ContentType {
        public TypeWeather() : base(ToolsStrings.ContentInstallation_WeatherNew, ToolsStrings.ContentInstallation_WeatherExisting) {}

        public override IFileAcManager GetManager() {
            return WeatherManager.Instance;
        }
    }
}