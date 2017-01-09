using JetBrains.Annotations;

namespace AcManager.Pages.SelectionLists {
    public sealed class SelectTag : SelectCategoryBase {
        public SelectTag([NotNull] string name) : base(name) {}
        
        internal override string Serialize() {
            return DisplayName;
        }

        [CanBeNull]
        internal static SelectTag Deserialize(string data) {
            return new SelectTag(data);
        }
    }
}