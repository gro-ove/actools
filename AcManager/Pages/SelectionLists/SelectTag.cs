using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Pages.SelectionLists {
    public sealed class SelectTag : SelectCategoryBase {
        public string TagValue { get; }

        private bool _accented;

        public bool Accented {
            get { return _accented; }
            set => Apply(value, ref _accented);
        }

        public SelectTag([NotNull] string name) : base(name.ApartFromFirst(@"#").TrimStart()) {
            TagValue = name;
            Accented = name.StartsWith(@"#");
        }

        public SelectTag([NotNull] string name, [NotNull] string tagValue) : base(name) {
            TagValue = tagValue;
            Accented = tagValue != name;
        }

        internal override string Serialize() {
            return TagValue;
        }

        [CanBeNull]
        internal static SelectTag Deserialize(string data) {
            return new SelectTag(data);
        }
    }
}