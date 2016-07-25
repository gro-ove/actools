using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;

namespace AcManager.Tools.ContentInstallation.Types {
    internal class TypeFont : ContentType {
        public TypeFont() : base(ToolsStrings.ContentInstallation_FontNew, ToolsStrings.ContentInstallation_FontExisting) {}

        public override IFileAcManager GetManager() {
            return FontsManager.Instance;
        }
    }
}