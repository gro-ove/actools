using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using AcManager.Pages.Dialogs;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
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
