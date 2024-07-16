using AcTools.Utils.Helpers;

namespace AcTools.ExtraKn5Utils.LodGenerator {
    public interface ICarLodGeneratorStage : IWithId {
        string ToolConfigurationFilename { get; }
        
        int TrianglesCount { get; }
        
        bool ApplyWeldingFix { get; }
        
        bool KeepTemporaryFiles { get; }
    }
}