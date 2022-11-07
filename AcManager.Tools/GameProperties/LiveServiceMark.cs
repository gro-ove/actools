using System.ComponentModel;

namespace AcManager.Tools.GameProperties {
    public class LiveServiceMark {
        public LiveServiceMark([Localizable(false)] string sourceId) {
            SourceId = sourceId;
        }

        public string SourceId { get; }
    }
}