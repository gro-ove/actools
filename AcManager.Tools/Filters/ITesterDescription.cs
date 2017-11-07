using System.Collections.Generic;

namespace AcManager.Tools.Filters {
    public interface ITesterDescription {
        IEnumerable<KeywordDescription> GetDescriptions();
    }
}