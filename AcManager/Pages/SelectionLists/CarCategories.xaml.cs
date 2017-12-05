using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Pages.SelectionLists {
    public partial class CarCategories {
        public CarCategories() : base(CarsManager.Instance) {
            InitializeComponent();
        }

        protected override ITester<CarObject> GetTester() {
            return CarObjectTester.Instance;
        }

        protected override string GetCategory() {
            return ContentCategory.CarCategories;
        }

        protected override string GetUriType() {
            return "car";
        }
    }
}
