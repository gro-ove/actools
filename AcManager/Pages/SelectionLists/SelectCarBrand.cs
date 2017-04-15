using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using JetBrains.Annotations;

namespace AcManager.Pages.SelectionLists {
    public class SelectCarBrand : SelectCategoryBase {
        public string Icon { get; set; }

        private readonly bool _builtInIcon;

        public SelectCarBrand([NotNull] string name, [CanBeNull] string carBrandBadge) : base(name) {
            Icon = GetBrandIcon(name, carBrandBadge, out _builtInIcon);
        }

        private static string GetBrandIcon([NotNull] string brand, [CanBeNull] string carBrandBadge, out bool builtInIcon) {
            var entry = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, brand + @".png");
            builtInIcon = entry.Exists;
            return builtInIcon ? entry.Filename : carBrandBadge ?? CarsManager.Instance.LoadedOnly.FirstOrDefault(x => x.Brand == brand)?.BrandBadge;
        }

        internal override string Serialize() {
            return (_builtInIcon ? "" : Icon) + @"|" + DisplayName;
        }

        private SelectCarBrand([NotNull] string name) : base(name) {}

        [CanBeNull]
        internal static SelectCarBrand Deserialize(string data) {
            var s = data.Split(new[] { '|' }, 2);
            if (s.Length != 2) return null;
            return s[0].Length == 0 ? new SelectCarBrand(s[1], null) : new SelectCarBrand(s[1]) { Icon = s[0] };
        }
    }
}