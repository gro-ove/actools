using AcManager.Pages.Dialogs;
using AcManager.Tools.SemiGui;

namespace AcManager.Tools {
    public class GameWrapperUiFactory : IUiFactory<IGameUi>, IUiFactory<IBookingUi> {
        IGameUi IUiFactory<IGameUi>.Create() => new GameDialog();

        IBookingUi IUiFactory<IBookingUi>.Create() => new BookingDialog();
    }
}