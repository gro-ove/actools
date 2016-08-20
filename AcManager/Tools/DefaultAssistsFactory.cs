using AcManager.Controls.ViewModels;
using AcManager.Tools.SemiGui;
using AcTools.Processes;

namespace AcManager.Tools {
    public class DefaultAssistsFactory : IAnyFactory<Game.AssistsProperties> {
        Game.AssistsProperties IAnyFactory<Game.AssistsProperties>.Create() => AssistsViewModel.Instance.GameProperties;
    }
}