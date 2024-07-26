using System.Collections.Generic;
using AcTools.ExtraKn5Utils.LodGenerator;
using JetBrains.Annotations;

namespace AcManager.Tools.LodsTools {
    public interface ILodsToolDetails : ICarLodGeneratorToolParams {
        string Key { get; }          
            
        IEnumerable<string> DefaultToolLocation { get; }

        [CanBeNull]
        string FindTool([CanBeNull] string currentLocation);

        ICarLodGeneratorService Create(string toolExecutable, IReadOnlyList<ICarLodGeneratorStage> stages);
    }
}