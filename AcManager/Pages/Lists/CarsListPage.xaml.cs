using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Selected;
using AcManager.Pages.Windows;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using Microsoft.Win32;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class CarsListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(CarObjectTester.Instance, filter));
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Model.Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private class ViewModel : AcListPageViewModel<CarObject> {
            public ViewModel(IFilter<CarObject> listFilter)
                    : base(CarsManager.Instance, listFilter) {}

            protected override string GetSubject() {
                return AppStrings.List_Cars;
            }

            protected override string LoadCurrentId() {
                if (_selectNextCar != null) {
                    var value = _selectNextCar;
                    SaveCurrentKey(value);
                    _selectNextCar = null;
                    return value;
                }

                return base.LoadCurrentId();
            }
        }

        public static void Show(CarObject car, string carSkinId = null) {
            var mainWindow = Application.Current?.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _selectNextCar = car.Id;
            _selectNextCarSkinId = carSkinId;

            NavigateToPage();
        }

        public static void NavigateToPage() {
            (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/CarsListPage.xaml", UriKind.Relative));
        }

        private static string _selectNextCar;
        private static string _selectNextCarSkinId; // TODO

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            return CommonBatchActions.DefaultSet.Concat(new BatchAction[] {
                BatchAction_AddTag.Instance,
                BatchAction_FixSpecsFormat.Instance,
                BatchAction_SetBrandBadge.Instance,
                BatchAction_PackCars.Instance,
            });
        }

        public class BatchAction_FixSpecsFormat : BatchAction<CarObject> {
            public static readonly BatchAction_FixSpecsFormat Instance = new BatchAction_FixSpecsFormat();
            public BatchAction_FixSpecsFormat() : base("Fix Specs Format", "This way, they’ll be more readable", "UI", null) { }

            protected override void ApplyOverride(CarObject obj) {
                new SelectedCarPage_New.ViewModel(obj, true).FixFormatCommand.Execute(null);
            }
        }

        public class BatchAction_SetBrandBadge : BatchAction<CarObject> {
            public static readonly BatchAction_SetBrandBadge Instance = new BatchAction_SetBrandBadge();
            public BatchAction_SetBrandBadge() : base("Update Brand Badge", "Will be updated if exists in library", "UI", null) { }

            private List<FilesStorage.ContentEntry> _badges;

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                _badges = FilesStorage.Instance.GetContentFiles(ContentCategory.BrandBadges).ToList();
                return base.ApplyAsync(list, progress, cancellation);
            }

            protected override void ApplyOverride(CarObject obj) {
                var badge = _badges.FirstOrDefault(x => string.Equals(x.Name, obj.Brand, StringComparison.OrdinalIgnoreCase));
                if (badge == null) return;

                if (File.Exists(obj.BrandBadge)) {
                    FileUtils.Recycle(obj.BrandBadge);
                }

                File.Copy(badge.Filename, obj.BrandBadge);
            }
        }

        public class BatchAction_AddTag : BatchAction<CarObject> {
            public static readonly BatchAction_AddTag Instance = new BatchAction_AddTag();

            public BatchAction_AddTag() : base("Add Tag", "Modify list of tags for several cars easily", "UI", "Batch.AddTag") {
                DisplayApply = "Apply";
                Tags = new BetterObservableCollection<string>();
            }

            #region Properies
            public BetterObservableCollection<string> Tags { get; }
            private List<string> _originalTags;

            private bool _sortTags = ValuesStorage.GetBool("_ba.addTag.sort", true);
            public bool SortTags {
                get => _sortTags;
                set {
                    if (Equals(value, _sortTags)) return;
                    _sortTags = value;
                    ValuesStorage.Set("_ba.addTag.sort", value);
                    OnPropertyChanged();
                }
            }

            private bool _cleanUp = ValuesStorage.GetBool("_ba.addTag.clean", false);
            public bool CleanUp {
                get => _cleanUp;
                set {
                    if (Equals(value, _cleanUp)) return;
                    _cleanUp = value;
                    ValuesStorage.Set("_ba.addTag.clean", value);
                    OnPropertyChanged();
                }
            }
            #endregion

            public override void OnSelectionChanged(IEnumerable<CarObject> enumerable) {
                IEnumerable<string> removed, added;

                if (_originalTags != null) {
                    removed = _originalTags.Where(x => !Tags.Contains(x)).ToList();
                    added = Tags.Where(x => !_originalTags.Contains(x)).ToList();
                } else {
                    removed = added = null;
                }

                List<string> list = null;
                foreach (var car in enumerable) {
                    if (list == null) {
                        list = car.Tags.ToList();
                    } else {
                        for (var i = list.Count - 1; i >= 0; i--) {
                            if (car.Tags.ContainsIgnoringCase(list[i])) continue;
                            list.RemoveAt(i);
                            if (list.Count == 0) goto End;
                        }
                    }
                }

                End:
                _originalTags = list ?? new List<string>(0);
                Tags.ReplaceEverythingBy_Direct(_originalTags.ApartFrom(removed)
                                                             .If(x => added == null ? x : x.Concat(added))
                                                             .OrderBy(x => x, TagsComparer.Instance));
            }

            protected override void ApplyOverride(CarObject obj) {
                var updatedList = obj.Tags.ApartFrom(_originalTags.Where(x => !Tags.Contains(x)))
                                     .Concat(Tags.Where(x => !_originalTags.Contains(x))).Distinct();

                if (_cleanUp) {
                    updatedList = TagsCollection.CleanUp(updatedList);
                }

                if (_sortTags) {
                    updatedList = updatedList.OrderBy(x => x, TagsComparer.Instance);
                }

                obj.Tags = new TagsCollection(updatedList);
            }
        }

        public class BatchAction_PackCars : CommonBatchActions.BatchAction_Pack<CarObject> {
            public static readonly BatchAction_PackCars Instance = new BatchAction_PackCars();

            public BatchAction_PackCars() : base("Batch.PackCars") {}

            #region Properies
            private bool _packData = ValuesStorage.GetBool("_ba.packCars.data", true);
            public bool PackData {
                get => _packData;
                set {
                    if (Equals(value, _packData)) return;
                    _packData = value;
                    ValuesStorage.Set("_ba.packCars.data", value);
                    OnPropertyChanged();
                }
            }

            private bool _includeTemplates = ValuesStorage.GetBool("_ba.packCars.templates", true);
            public bool IncludeTemplates {
                get => _includeTemplates;
                set {
                    if (Equals(value, _includeTemplates)) return;
                    _includeTemplates = value;
                    ValuesStorage.Set("_ba.packCars.templates", value);
                    OnPropertyChanged();
                }
            }
            #endregion

            protected override AcCommonObject.AcCommonObjectPackerParams GetParams() {
                return new CarObject.CarPackerParams {
                    PackData = PackData,
                    IncludeTemplates = IncludeTemplates
                };
            }
        }
        #endregion
    }
}
