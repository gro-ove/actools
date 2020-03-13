using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Selected;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class ShowroomsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(ShowroomObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class ViewModel : AcListPageViewModel<ShowroomObject> {
            public ViewModel(IFilter<ShowroomObject> listFilter)
                : base(ShowroomsManager.Instance, listFilter) {
            }

            protected override string GetSubject() {
                return AppStrings.List_Showrooms;
            }
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.GetDefaultSet<ShowroomObject>().Concat(new BatchAction[] {
                BatchAction_FixNormals.Instance,
                BatchAction_UpdatePreviews.Instance,
                BatchAction_PackShowrooms.Instance,
            });
        }

        public class BatchAction_FixNormals : BatchAction<ShowroomObject> {
            public static readonly BatchAction_FixNormals Instance = new BatchAction_FixNormals();
            public BatchAction_FixNormals()
                    : base("Fix 360° Lighting", "Fix lighting for 360° panorama showroom", "Graphics", null) {
                DisplayApply = "Fix";
            }

            public override bool IsAvailable(ShowroomObject obj) {
                return true;
            }

            protected override void ApplyOverride(ShowroomObject obj) {
                SelectedShowroomPage.FixLighting(obj);
            }
        }

        public class BatchAction_UpdatePreviews : BatchAction<ShowroomObject> {
            public static readonly BatchAction_UpdatePreviews Instance = new BatchAction_UpdatePreviews();
            public BatchAction_UpdatePreviews()
                    : base("Update previews", "Re-shot previews using reflective sphere", "Look", null) {
                DisplayApply = "Update";
            }

            public override bool IsAvailable(ShowroomObject obj) {
                return true;
            }

            protected override void ApplyOverride(ShowroomObject obj) {
                SelectedShowroomPage.UpdatePreview(obj, true);
            }

            public override async Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                try {
                    await base.ApplyAsync(list, progress, cancellation).ConfigureAwait(false);
                } finally {
                    SelectedShowroomPage.RemovePreviewSphere();
                }
            }
        }

        public class BatchAction_PackShowrooms : CommonBatchActions.BatchAction_Pack<ShowroomObject> {
            public static readonly BatchAction_PackShowrooms Instance = new BatchAction_PackShowrooms();

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return null;
            }
        }
        #endregion
    }
}
