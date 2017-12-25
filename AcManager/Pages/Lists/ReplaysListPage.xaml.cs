using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class ReplaysListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(ReplayObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ((ViewModel)DataContext).Unload();
        }

        private class CategoryGroupDescription : GroupDescription {
            public override object GroupNameFromItem(object item, int level, CultureInfo culture) {
                var category = ((item as AcItemWrapper)?.Value as ReplayObject)?.EditableCategory;
                return category == ReplayObject.AutosaveCategory ? "Autosave"  : category ?? "";
            }
        }

        private class ViewModel : AcListPageViewModel<ReplayObject> {
            public ViewModel(IFilter<ReplayObject> listFilter)
                    : base(ReplaysManager.Instance, listFilter) {
                GroupBy(nameof(ReplayObject.EditableCategory), new CategoryGroupDescription());
            }

            protected override string GetSubject() {
                return AppStrings.List_Replays;
            }
        }

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.DefaultSet.Append(CommonBatchActions.BatchAction_Delete.Instance)
                                     .If(AcSettingsHolder.Replay.Autosave, x => x.Append(BatchAction_KeepAutosaveReplay.Instance));
        }

        public class BatchAction_KeepAutosaveReplay : BatchAction<ReplayObject> {
            public static readonly BatchAction_KeepAutosaveReplay Instance = new BatchAction_KeepAutosaveReplay();
            public BatchAction_KeepAutosaveReplay() : base("Keep replay", "Keep auto-saved replays in selection", null, null) {
                DisplayApply = "Keep";
                Priority = 2;
            }

            public override bool IsAvailable(ReplayObject obj) {
                return obj.EditableCategory == ReplayObject.AutosaveCategory && AcSettingsHolder.Replay.Autosave;
            }

            protected override async Task ApplyOverrideAsync(ReplayObject obj) {
                if (obj.EditableCategory == ReplayObject.AutosaveCategory && AcSettingsHolder.Replay.Autosave) {
                    obj.EditableCategory = null;
                    await Task.Delay(10);
                    obj.SaveCommand.Execute();
                }
            }
        }
        #endregion

        protected override void OnItemDoubleClick(AcObjectNew obj) {
            if (obj is ReplayObject replay) {
                GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                    Name = replay.Id,
                    TrackId = replay.TrackId,
                    TrackConfiguration = replay.TrackConfiguration,
                    WeatherId = replay.WeatherId
                }));
            }
        }
    }
}
