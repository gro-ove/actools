using AcTools.Processes;

namespace AcManager.Tools.Starters {
    public interface IAcsPlatformSpecificStarter : IAcsStarter {
        bool Use32Version { get; set; }
    }
}