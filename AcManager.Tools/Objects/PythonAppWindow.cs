using System.IO;
using AcManager.Tools.Managers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed class PythonAppWindow : Displayable {
        [NotNull]
        public string IconOn { get; }

        [NotNull]
        public string IconOff { get; }

        public PythonAppWindow([NotNull] string name) {
            DisplayName = name;

            var iconsDir = Path.Combine(AcRootDirectory.Instance.RequireValue, "content", "gui", "icons");
            IconOn = Path.Combine(iconsDir, name + "_ON.png");
            IconOff = Path.Combine(iconsDir, name + "_OFF.png");
        }
    }
}