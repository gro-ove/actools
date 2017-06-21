using System.Collections.Generic;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors.Solutions;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors {
    public interface ISolutionsFactory {
        [CanBeNull]
        Task<IEnumerable<ISolution>> GetSolutionsAsync(AcError error);
    }
}