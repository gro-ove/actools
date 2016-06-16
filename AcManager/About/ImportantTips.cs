using AcManager.Tools.About;

namespace AcManager.About {
    public sealed class ImportantTips : PieceOfInformation {
        private ImportantTips(string displayName, string description, string id = null)
                : base(displayName, description, id) {}

        public static readonly ImportantTips[] Tips = {
            new ImportantTips(@"I Need Your Help!",
                    @"", "iNeedYourHelp")
        };
    }
}