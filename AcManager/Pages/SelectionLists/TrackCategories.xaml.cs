using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using StringBasedFilter;

namespace AcManager.Pages.SelectionLists {
    public partial class TrackCategories {
        public TrackCategories() : base(TracksManager.Instance) {
            InitializeComponent();
        }

        protected override ITester<TrackObject> GetTester() {
            return TrackObjectTester.Instance;
        }

        protected override string GetCategory() {
            return ContentCategory.TrackCategories;
        }

        protected override string GetUriType() {
            return "track";
        }
    }
}
