using AcTools.Processes;

namespace AcManager.Tools.Starters {
    public interface IAcsPrepareableStarter : IAcsStarter {
        bool TryToPrepare();
    }
}