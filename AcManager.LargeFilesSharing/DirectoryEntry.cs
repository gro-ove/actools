using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.LargeFilesSharing {
    public class DirectoryEntry : Displayable, IWithId {
        public string Id { get; set; }

        public DirectoryEntry[] Children { get; set; }

        public override string ToString() {
            return $"{DisplayName} ({Id}){{ {Children.JoinToString(@", ")} }}";
        }
    }
}