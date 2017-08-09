using AcManager.Tools;
using AcManager.Tools.Objects;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class RaceGridPlayerEntry : RaceGridEntry {
        public override bool SpecialEntry => true;

        public override string DisplayName => ToolsStrings.RaceGrid_You;

        internal RaceGridPlayerEntry([NotNull] CarObject car) : base(car) {}
    }
}