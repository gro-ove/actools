using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.ViewModels;
using AcManager.CustomShowroom;
using AcManager.Pages.Selected;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Filters;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificSpecial;
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
            return CommonBatchActions.GetDefaultSet<CarObject>().Concat(new BatchAction[] {
                BatchAction_FixBrand.Instance,
                BatchAction_FixCarClass.Instance,
                BatchAction_SortAndCleanUpTags.Instance,
                BatchAction_FixSpecsFormat.Instance,
                BatchAction_SetBrandBadge.Instance,
                BatchAction_SyncCarLogo.Instance,
                BatchAction_PackCars.Instance,
                BatchAction_UpdatePreviews.Instance,
                BatchAction_UpdateAmbientShadows.Instance,
            });
        }

        public class BatchAction_FixBrand : BatchAction<CarObject> {
            public static readonly BatchAction_FixBrand Instance = new BatchAction_FixBrand();
            public BatchAction_FixBrand() : base("Fix Brand", "Try to guess brand if it’s not in the list", "UI", "Batch.FixBrand") {
                DisplayApply = "Try";
            }

            private bool _updateBrandBadge = ValuesStorage.GetBool("_ba.fixBrand.badge", SettingsHolder.Content.ChangeBrandIconAutomatically);

            public bool UpdateBrandBadge {
                get => _updateBrandBadge;
                set {
                    if (Equals(value, _updateBrandBadge)) return;
                    _updateBrandBadge = value;
                    ValuesStorage.Set("_ba.fixBrand.badge", value);
                    OnPropertyChanged();
                }
            }

            private bool _searchInTags = ValuesStorage.GetBool("_ba.fixBrand.fromTags", true);
            public bool SearchInTags {
                get => _searchInTags;
                set {
                    if (Equals(value, _searchInTags)) return;
                    _searchInTags = value;
                    ValuesStorage.Set("_ba.fixBrand.fromTags", value);
                    OnPropertyChanged();
                }
            }

            private bool _searchInTheMiddle = ValuesStorage.GetBool("_ba.fixBrand.fromMiddle", true);
            public bool SearchInTheMiddle {
                get => _searchInTheMiddle;
                set {
                    if (Equals(value, _searchInTheMiddle)) return;
                    _searchInTheMiddle = value;
                    ValuesStorage.Set("_ba.fixBrand.fromMiddle", value);
                    OnPropertyChanged();
                }
            }

            private bool _updateName = ValuesStorage.GetBool("_ba.fixBrand.name", true);

            public bool UpdateName {
                get => _updateName;
                set {
                    if (Equals(value, _updateName)) return;
                    _updateName = value;
                    ValuesStorage.Set("_ba.fixBrand.name", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FixNamesWithoutBrands));
                }
            }

            private bool _fixNamesWithoutBrands = ValuesStorage.GetBool("_ba.fixBrand.fixName", true);

            public bool FixNamesWithoutBrands {
                get => _fixNamesWithoutBrands && _updateName;
                set {
                    if (Equals(value, _fixNamesWithoutBrands)) return;
                    _fixNamesWithoutBrands = value;
                    ValuesStorage.Set("_ba.fixBrand.fixName", value);
                    OnPropertyChanged();
                    RaiseAvailabilityChanged();
                }
            }

            public override bool IsAvailable(CarObject obj) {
                return obj.Brand == null ||
                        FixNamesWithoutBrands && obj.Name?.StartsWith(obj.Brand) == false ||
                        !SuggestionLists.CarBrandsList.Contains(obj.Brand);
            }

            private string[] _brands;
            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                _brands = SuggestionLists.CarBrandsList.ToArray();
                return base.ApplyAsync(list, progress, cancellation);
            }

            protected override void ApplyOverride(CarObject obj) {
                if (obj.Brand != null &&
                    Array.IndexOf(_brands, obj.Brand) != -1 &&
                    (!FixNamesWithoutBrands || obj.Name?.StartsWith(obj.Brand) != false)) return;

                string newBrand;

                // First attempt — maybe it’s just in a wrong case
                if (obj.Brand != null) {
                    for (var i = 0; i < _brands.Length; i++) {
                        var brand = _brands[i];

                        if (string.Equals(brand, obj.Brand?.Trim(), StringComparison.InvariantCultureIgnoreCase)) {
                            newBrand = brand;
                            goto End;
                        }
                    }
                }

                // Brand might be in front of the name
                for (var i = 0; i < _brands.Length; i++) {
                    var brand = _brands[i];
                    if (Regex.IsMatch(obj.DisplayName, $@"^\W*{Regex.Escape(brand)}\b", RegexOptions.IgnoreCase)) {
                        newBrand = brand;
                        goto End;
                    }
                }

                // Or in the middle
                if (SearchInTheMiddle) {
                    for (var i = 0; i < _brands.Length; i++) {
                        var brand = _brands[i];
                        if (Regex.IsMatch(obj.DisplayName, $@"\b{Regex.Escape(brand)}\b", RegexOptions.IgnoreCase)) {
                            newBrand = brand;
                            goto End;
                        }
                    }
                }

                // Or in tags?
                if (SearchInTags) {
                    for (var i = 0; i < _brands.Length; i++) {
                        var brand = _brands[i];
                        if (obj.Tags.ContainsIgnoringCase(brand)) {
                            newBrand = brand;
                            goto End;
                        }
                    }
                }

                return;

                End:
                obj.Brand = newBrand;

                if (UpdateBrandBadge) {
                    var entry = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, newBrand + @".png");
                    if (entry.Exists) {
                        try {
                            File.Copy(entry.Filename, obj.BrandBadge, true);
                        } catch (Exception ex) {
                            Logging.Warning(ex);
                        }
                    }
                }

                if (UpdateName && obj.Name != null) {
                    var name = obj.Name;
                    if (newBrand != null) {
                        name = newBrand + " " + Regex.Replace(name, $@"\b{Regex.Escape(newBrand)}\b", "", RegexOptions.IgnoreCase);
                    }

                    name = Regex.Replace(name, @"\s+", " ").Trim();
                    obj.NameEditable = name;
                }
            }
        }

        public class BatchAction_SortAndCleanUpTags : BatchAction<CarObject> {
            public static readonly BatchAction_SortAndCleanUpTags Instance = new BatchAction_SortAndCleanUpTags();
            public BatchAction_SortAndCleanUpTags() : base("Sort & Clean Tags", "This way, they’ll be more readable", "UI", "Batch.SortAndCleanUpTags") {
                DisplayApply = "Fix";
            }

            private bool _sortTags = ValuesStorage.GetBool("_ba.sortOutTags.sort", true);
            public bool SortTags {
                get => _sortTags;
                set {
                    if (Equals(value, _sortTags)) return;
                    _sortTags = value;
                    ValuesStorage.Set("_ba.sortOutTags.sort", value);
                    OnPropertyChanged();
                    RaiseAvailabilityChanged();
                }
            }

            private bool _cleanUpTags = ValuesStorage.GetBool("_ba.sortOutTags.cleanUp", true);

            public bool CleanUpTags {
                get => _cleanUpTags;
                set {
                    if (Equals(value, _cleanUpTags)) return;
                    _cleanUpTags = value;
                    ValuesStorage.Set("_ba.sortOutTags.cleanUp", value);
                    OnPropertyChanged();
                    RaiseAvailabilityChanged();
                }
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            public override int OnSelectionChanged(IList list) {
                return SortTags || CleanUpTags ? base.OnSelectionChanged(list) : 0;
            }

            protected override void ApplyOverride(CarObject obj) {
                if (SortTags) {
                    if (CleanUpTags) {
                        obj.TagsCleanUpAndSortCommand.Execute();
                    } else {
                        obj.TagsSortCommand.Execute();
                    }
                } else if (CleanUpTags) {
                    obj.TagsCleanUpCommand.Execute();
                }
            }
        }

        public class BatchAction_UpdatePreviews : BatchAction<CarObject> {
            public static readonly BatchAction_UpdatePreviews Instance = new BatchAction_UpdatePreviews();
            public BatchAction_UpdatePreviews()
                    : base("Update Previews", "With previously used params", "Look", null) {
                DisplayApply = "Update";
                InternalWaitingDialog = true;
                Priority = 1;
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                return OfType(list).Select(obj => new ToUpdatePreview(obj)).Run();
            }
        }

        public class BatchAction_UpdateAmbientShadows : BatchAction<CarObject> {
            public static readonly BatchAction_UpdateAmbientShadows Instance = new BatchAction_UpdateAmbientShadows();

            public BatchAction_UpdateAmbientShadows() : base("Update Ambient Shadows", "Using previously used params", "Graphics", null) {
                DisplayApply = "Update";
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            private LiteShowroomTools.AmbientShadowParams _params;

            public override async Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                _params = LiteShowroomTools.LoadAmbientShadowParams();

                var l = OfType(list).ToList();
                for (var i = 0; i < l.Count; i++) {
                    var car = l[i];
                    var subProgress = progress.Subrange((double)i / l.Count, 1d / l.Count).ToDoubleProgress(car.DisplayName);

                    try {
                        await Task.Run(() => {
                            var kn5 = Kn5.FromFile(FileUtils.GetMainCarFilename(car.Location, car.AcdData));
                            if (kn5 == null) return;

                            using (var renderer = new AmbientShadowRenderer(kn5, car.AcdData) {
                                DiffusionLevel = (float)_params.AmbientShadowDiffusion / 100f,
                                SkyBrightnessLevel = (float)_params.AmbientShadowBrightness / 100f,
                                Iterations = _params.AmbientShadowIterations,
                                HideWheels = _params.AmbientShadowHideWheels,
                                Fade = _params.AmbientShadowFade,
                                CorrectLighting = _params.AmbientShadowAccurate,
                                BodyMultipler = _params.AmbientShadowBodyMultiplier,
                                WheelMultipler = _params.AmbientShadowWheelMultiplier,
                            }) {
                                renderer.Initialize();
                                renderer.Shot(subProgress, cancellation);
                            }
                        });
                    } catch (Exception e) {
                        NonfatalError.Notify(ControlsStrings.CustomShowroom_AmbientShadows_CannotUpdate, e);
                    }

                    subProgress?.Report(1d);
                    if (cancellation.IsCancellationRequested) return;
                }

                OnSelectionChanged(list);
            }
        }

        public class BatchAction_FixCarClass : BatchAction<CarObject> {
            public static readonly BatchAction_FixCarClass Instance = new BatchAction_FixCarClass();
            public BatchAction_FixCarClass() : base("Fix Car Class", "If wrong, try to guess from data and model", "UI", "Batch.FixCarClass") {
                DisplayApply = "Fix";
            }

            public override bool IsAvailable(CarObject obj) {
                return UpdateAll || obj.CarClass != "race" && obj.CarClass != "street";
            }

            private bool _updateAll = ValuesStorage.GetBool("_ba.fixCarClass.updateAll");

            public bool UpdateAll {
                get => _updateAll;
                set {
                    if (Equals(value, _updateAll)) return;
                    _updateAll = value;
                    ValuesStorage.Set("_ba.fixCarClass.updateAll", value);
                    OnPropertyChanged();
                    RaiseAvailabilityChanged();
                }
            }

            protected override async Task ApplyOverrideAsync(CarObject obj) {
                if (!IsAvailable(obj)) return;
                obj.CarClass = await Task.Run(() => CarUtils.TryToGuessCarClass(obj));
            }
        }

        public class BatchAction_FixSpecsFormat : BatchAction<CarObject> {
            public static readonly BatchAction_FixSpecsFormat Instance = new BatchAction_FixSpecsFormat();
            public BatchAction_FixSpecsFormat() : base("Fix Specs Format", "This way, they’ll be more readable", "UI", null) {
                DisplayApply = "Fix";
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            protected override void ApplyOverride(CarObject obj) {
                new SelectedCarPage_New.ViewModel(obj, true).FixFormatCommand.Execute(null);
            }
        }

        public class BatchAction_SetBrandBadge : BatchAction<CarObject> {
            public static readonly BatchAction_SetBrandBadge Instance = new BatchAction_SetBrandBadge();
            public BatchAction_SetBrandBadge() : base("Update Brand Badge", "Will be updated if exists in library", "UI", null) { }

            private List<FilesStorage.ContentEntry> _badges;

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

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

        public class BatchAction_SyncCarLogo : BatchAction<CarObject> {
            public static readonly BatchAction_SyncCarLogo Instance = new BatchAction_SyncCarLogo ();
            public BatchAction_SyncCarLogo() : base("Update Car’s Logo", "Replace them by brand badges", "Look", "Batch.SyncCarLogo") { }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            private bool _differentOnly = ValuesStorage.GetBool("_ba.syncCarLogos.differentOnly", true);
            public bool DifferentOnly {
                get => _differentOnly;
                set {
                    if (Equals(value, _differentOnly)) return;
                    _differentOnly = value;
                    ValuesStorage.Set("_ba.syncCarLogos.differentOnly", value);
                    OnPropertyChanged();
                }
            }

            private bool _preferHardlinks = ValuesStorage.GetBool("_ba.syncCarLogos.hardlinks", true);

            public bool PreferHardlinks {
                get => _preferHardlinks;
                set {
                    if (Equals(value, _preferHardlinks)) return;
                    _preferHardlinks = value;
                    ValuesStorage.Set("_ba.syncCarLogos.hardlinks", value);
                    OnPropertyChanged();
                }
            }

            protected override void ApplyOverride(CarObject obj) {
                var brandBadge = new FileInfo(obj.BrandBadge);
                if (!brandBadge.Exists) return;

                var logo = new FileInfo(obj.LogoIcon);
                if (DifferentOnly && logo.Exists && logo.Length == brandBadge.Length && logo.LastWriteTime == brandBadge.LastWriteTime) {
                    return;
                }

                if (PreferHardlinks) {
                    FileUtils.HardlinkOrCopy(brandBadge.FullName, logo.FullName, true);
                } else {
                    File.Copy(brandBadge.FullName, logo.FullName, true);
                }
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
