using System.Collections.Generic;
using AcManager.Tools.AcErrors.Solutions;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors {
    public interface ISolutionsFactory {
        [CanBeNull]
        IEnumerable<ISolution> GetSolutions(AcError error);
    }
}