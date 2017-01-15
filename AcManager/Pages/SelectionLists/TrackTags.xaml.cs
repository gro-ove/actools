using System;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Pages.SelectionLists {
    public partial class TrackTags {
        public TrackTags() : base(TracksManager.Instance) {
            InitializeComponent();
        }

        protected override Uri GetPageAddress(SelectTag category) {
            return SelectTrackDialog.TagUri(category.DisplayName);
        }

        protected override bool IsIgnored(TrackObject obj, string tagValue) {
            return string.Equals(obj.Name, tagValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
