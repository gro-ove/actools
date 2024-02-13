using JetBrains.Annotations;

namespace AcManager.Pages.Drive {
    public interface IRaceGridModeViewModel {
        void SetRaceGridData([NotNull] string serializedRaceGrid);
    }
}