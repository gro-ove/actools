using AcManager.Pages.Dialogs;
using AcManager.Tools.SemiGui;

namespace AcManager.Tools {
    public class GameWrapperUiFactory : IGameUiFactory {
        public IGameUi Create() => new GameDialog();
    }
}