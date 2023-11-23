using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.CustomShowroom;
using AcManager.Pages.Drive;
using AcManager.Pages.Selected;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.AcdFile;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
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
            FancyHints.DoubleClickToQuickDrive.Trigger();

            if (Model.MainList.Count > 20) {
                FancyHints.MultiSelectionMode.Trigger();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Model.Unload();
        }

        private class ViewModel : AcListPageViewModel<CarObject> {
            public ViewModel(IFilter<CarObject> listFilter)
                    : base(CarsManager.Instance, listFilter) { }

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
            if (Application.Current?.MainWindow is MainWindow) {
                _selectNextCar = car.Id;
                _selectNextCarSkinId = carSkinId;
                NavigateToPage();
            }
        }

        public static void NavigateToPage() {
            (Application.Current?.MainWindow as MainWindow)?.NavigateTo(new Uri("/Pages/Lists/CarsListPage.xaml", UriKind.Relative));
        }

        private static string _selectNextCar;
        private static string _selectNextCarSkinId; // TODO

        #region Batch actions
        protected override IEnumerable<BatchAction> GetBatchActions() {
            var ret = CommonBatchActions.GetDefaultSet<CarObject>().Concat(new BatchAction[] {
                BatchAction_FixBrand.Instance,
                BatchAction_FixCarClass.Instance,
                BatchAction_RecalculateCurves.Instance,
                BatchAction_SortAndCleanUpTags.Instance,
                BatchAction_RecalculateWeight.Instance,
                BatchAction_UpdatePwRatio.Instance,
                BatchAction_FixSpecsFormat.Instance,
                BatchAction_SetBrandBadge.Instance,
                BatchAction_SyncCarLogo.Instance,
                BatchAction_PackCars.Instance,
                BatchAction_UpdatePreviews.Instance,
                BatchAction_UpdateAmbientShadows.Instance,
                BatchAction_AnalyzeCar.Instance,
            });
            if (SettingsHolder.Common.DeveloperMode) {
                ret = ret.Append(BatchAction_UnpackCarData.Instance);
                ret = ret.Append(BatchAction_PackCarData.Instance);
            }
            return ret;
        }

        public class BatchAction_FixBrand : BatchAction<CarObject> {
            public static readonly BatchAction_FixBrand Instance = new BatchAction_FixBrand();

            public BatchAction_FixBrand() : base("Fix brand", "Try to guess brand if it’s not in the list", "UI", "Batch.FixBrand") {
                DisplayApply = "Try";
            }

            private bool _updateBrandBadge = ValuesStorage.Get("_ba.fixBrand.badge", SettingsHolder.Content.ChangeBrandIconAutomatically);

            public bool UpdateBrandBadge {
                get => _updateBrandBadge;
                set {
                    if (Equals(value, _updateBrandBadge)) return;
                    _updateBrandBadge = value;
                    ValuesStorage.Set("_ba.fixBrand.badge", value);
                    OnPropertyChanged();
                }
            }

            private bool _searchInTags = ValuesStorage.Get("_ba.fixBrand.fromTags", true);

            public bool SearchInTags {
                get => _searchInTags;
                set {
                    if (Equals(value, _searchInTags)) return;
                    _searchInTags = value;
                    ValuesStorage.Set("_ba.fixBrand.fromTags", value);
                    OnPropertyChanged();
                }
            }

            private bool _searchInTheMiddle = ValuesStorage.Get("_ba.fixBrand.fromMiddle", true);

            public bool SearchInTheMiddle {
                get => _searchInTheMiddle;
                set {
                    if (Equals(value, _searchInTheMiddle)) return;
                    _searchInTheMiddle = value;
                    ValuesStorage.Set("_ba.fixBrand.fromMiddle", value);
                    OnPropertyChanged();
                }
            }

            private bool _updateName = ValuesStorage.Get("_ba.fixBrand.name", true);

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

            private bool _fixNamesWithoutBrands = ValuesStorage.Get("_ba.fixBrand.fixName", true);

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

        public class BatchAction_RecalculateCurves : BatchAction<CarObject> {
            public static readonly BatchAction_RecalculateCurves Instance = new BatchAction_RecalculateCurves();

            public BatchAction_RecalculateCurves() : base("Recalculate curves", "I don’t recommend to use it for Kunos cars", "UI", "Batch.RecalculateCurves") {
                DisplayApply = "Recalculate";
            }

            private bool _scaleToMaxValues = ValuesStorage.Get("_ba.recalculateCurves.scaleToMaxValues", true);

            public bool ScaleToMaxValues {
                get => _scaleToMaxValues;
                set {
                    if (Equals(value, _scaleToMaxValues)) return;
                    _scaleToMaxValues = value;
                    ValuesStorage.Set("_ba.recalculateCurves.scaleToMaxValues", value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UpdateMaxValues));
                    RaiseAvailabilityChanged();
                }
            }

            private double _transmissionLoss = ValuesStorage.Get("_ba.recalculateCurves.cleanUp", 0.13);

            public double TransmissionLoss {
                get => _transmissionLoss;
                set {
                    value = value.Clamp(0, 0.5);
                    if (Equals(value, _transmissionLoss)) return;
                    _transmissionLoss = value;
                    ValuesStorage.Set("_ba.recalculateCurves.cleanUp", value);
                    OnPropertyChanged();
                }
            }

            private bool _rebuildFromData = ValuesStorage.Get("_ba.recalculateCurves.rebuildFromData", true);

            public bool RebuildFromData {
                get => _rebuildFromData;
                set {
                    if (Equals(value, _rebuildFromData)) return;
                    _rebuildFromData = value;
                    ValuesStorage.Set("_ba.recalculateCurves.rebuildFromData", value);
                    OnPropertyChanged();
                    RaiseAvailabilityChanged();
                }
            }

            private bool _updateMaxValues = ValuesStorage.Get("_ba.recalculateCurves.updateMaxValues", true);

            public bool UpdateMaxValues {
                get => !_scaleToMaxValues && _updateMaxValues;
                set {
                    if (Equals(value, _updateMaxValues)) return;
                    _updateMaxValues = value;
                    ValuesStorage.Set("_ba.recalculateCurves.updateMaxValues", value);
                    OnPropertyChanged();
                }
            }

            public override bool IsAvailable(CarObject obj) {
                return ScaleToMaxValues || RebuildFromData;
            }

            protected override void ApplyOverride(CarObject obj) {
                Lut torqueCurve, powerCurve;

                if (RebuildFromData) {
                    var data = obj.AcdData;
                    if (data == null) return;

                    try {
                        torqueCurve = TorquePhysicUtils.LoadCarTorque(data);
                        powerCurve = TorquePhysicUtils.TorqueToPower(torqueCurve);
                    } catch (Exception e) {
                        Logging.Warning(e);
                        return;
                    }
                } else {
                    powerCurve = obj.SpecsPowerCurve?.ToLut();
                    torqueCurve = obj.SpecsTorqueCurve?.ToLut();
                }

                if (ScaleToMaxValues) {
                    var power = FlexibleParser.TryParseDouble(obj.SpecsBhp);
                    var torque = FlexibleParser.TryParseDouble(obj.SpecsTorque);
                    if (!power.HasValue && !torque.HasValue) return;

                    if (!torque.HasValue) {
                        if (powerCurve != null) {
                            powerCurve.ScaleToSelf(power.Value);

                            var temporaryCurve = TorquePhysicUtils.PowerToTorque(powerCurve);
                            temporaryCurve.UpdateBoundingBox();
                            torque = temporaryCurve.MaxY;
                            torqueCurve?.ScaleToSelf(torque.Value);
                        } else return;
                    } else if (!power.HasValue) {
                        if (torqueCurve != null) {
                            torqueCurve.ScaleToSelf(torque.Value);

                            var temporaryCurve = TorquePhysicUtils.TorqueToPower(torqueCurve);
                            temporaryCurve.UpdateBoundingBox();
                            power = temporaryCurve.MaxY;
                            powerCurve?.ScaleToSelf(power.Value);
                        } else return;
                    } else {
                        powerCurve?.ScaleToSelf(power.Value);
                        torqueCurve?.ScaleToSelf(torque.Value);
                    }
                } else {
                    var multipler = 1d / (1d - TransmissionLoss);
                    torqueCurve?.TransformSelf(x => x.Y * multipler);
                    powerCurve?.TransformSelf(x => x.Y * multipler);
                }

                if (powerCurve != null) {
                    obj.SpecsPowerCurve = new GraphData(powerCurve);
                }

                if (torqueCurve != null) {
                    obj.SpecsTorqueCurve = new GraphData(torqueCurve);
                }

                if (UpdateMaxValues) {
                    // MaxY values were updated while creating new GraphData instances above
                    if (torqueCurve != null) {
                        obj.SpecsTorque = SelectedAcObjectViewModel.SpecsFormat(AppStrings.CarSpecs_Torque_FormatTooltip,
                                torqueCurve.MaxY.ToString(@"F0", CultureInfo.InvariantCulture)) + (TransmissionLoss == 0d ? "*" : "");
                    }

                    if (powerCurve != null) {
                        obj.SpecsBhp = SelectedAcObjectViewModel.SpecsFormat(
                                TransmissionLoss == 0d ? AppStrings.CarSpecs_PowerAtWheels_FormatTooltip : AppStrings.CarSpecs_Power_FormatTooltip,
                                powerCurve.MaxY.ToString(@"F0", CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        public class BatchAction_SortAndCleanUpTags : BatchAction<CarObject> {
            public static readonly BatchAction_SortAndCleanUpTags Instance = new BatchAction_SortAndCleanUpTags();

            public BatchAction_SortAndCleanUpTags() : base("Sort & clean tags", "This way, they’ll be more readable", "UI", "Batch.SortAndCleanUpTags") {
                DisplayApply = "Fix";
            }

            private bool _sortTags = ValuesStorage.Get("_ba.sortOutTags.sort", true);

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

            private bool _cleanUpTags = ValuesStorage.Get("_ba.sortOutTags.cleanUp", true);

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

        public class BatchAction_RecalculateWeight : BatchAction<CarObject> {
            public static readonly BatchAction_RecalculateWeight Instance = new BatchAction_RecalculateWeight();

            public BatchAction_RecalculateWeight()
                    : base("Recalculate weight", "Set weight to physics value", "UI", null) {
                DisplayApply = "Recalculate";
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            protected override void ApplyOverride(CarObject obj) {
                var data = obj.AcdData;
                var weight = data?.GetIniFile("car.ini")["BASIC"].GetInt("TOTALMASS", 0);
                if (weight == null || data.IsEmpty || weight < CommonAcConsts.DriverWeight) {
                    return;
                }
                obj.SpecsWeight = SelectedAcObjectViewModel.SpecsFormat(AppStrings.CarSpecs_Weight_FormatTooltip,
                        (weight.Value - CommonAcConsts.DriverWeight).ToString(@"F0", CultureInfo.InvariantCulture));
            }
        }

        public class BatchAction_UnpackCarData : BatchAction<CarObject> {
            public static readonly BatchAction_UnpackCarData Instance = new BatchAction_UnpackCarData();

            public BatchAction_UnpackCarData()
                    : base("Unpack car data", "Extract “data.acd” into “data” folders (if there is already such a folder, new one will be created next to it)", "Developer", null) {
                DisplayApply = "Unpack";
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            protected override void ApplyOverride(CarObject obj) {
                try {
                    var source = Path.Combine(obj.Location, "data.a" + "cd");
                    if (!File.Exists(source)) return;
                    var destination = FileUtils.EnsureUnique(Path.Combine(obj.Location, "data"));
                    Acd.FromFile(source).ExportDirectory(destination);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground(ToolsStrings.Common_CannotReadData, e);
                }
            }
        }

        public class BatchAction_PackCarData : BatchAction<CarObject> {
            public static readonly BatchAction_PackCarData Instance = new BatchAction_PackCarData();

            public BatchAction_PackCarData()
                    : base("Pack car data", "Pack “data” folder into “data.acd” if there is no such file", "Developer", null) {
                DisplayApply = "Unpack";
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            protected override void ApplyOverride(CarObject obj) {
                try {
                    var destination = Path.Combine(obj.Location, "data.a" + "cd");
                    var dataDirectory = Path.Combine(obj.Location, "data");
                    if (!Directory.Exists(dataDirectory) || File.Exists(destination)) {
                        return;
                    }
                    Acd.FromDirectory(dataDirectory).Save(destination);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground(AppStrings.Car_CannotPackData, ToolsStrings.Common_MakeSureThereIsEnoughSpace, e);
                }
            }
        }

        public class BatchAction_UpdatePwRatio : BatchAction<CarObject> {
            public static readonly BatchAction_UpdatePwRatio Instance = new BatchAction_UpdatePwRatio();

            public BatchAction_UpdatePwRatio()
                    : base("Update P/W Ratio", "Simply divide car’s weight by its BHP", "UI", null) {
                DisplayApply = "Update";
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            protected override void ApplyOverride(CarObject obj) {
                if (!FlexibleParser.TryParseDouble(obj.SpecsBhp, out var power) ||
                        !FlexibleParser.TryParseDouble(obj.SpecsWeight, out var weight)) return;

                var ratio = weight / power;
                obj.SpecsPwRatio = SelectedAcObjectViewModel.SpecsFormat(AppStrings.CarSpecs_PwRatio_FormatTooltip, ratio.Round(0.01));
            }
        }

        public class BatchAction_UpdatePreviews : BatchAction<CarObject> {
            public static readonly BatchAction_UpdatePreviews Instance = new BatchAction_UpdatePreviews();

            public BatchAction_UpdatePreviews()
                    : base("Update previews", "With previously used params", "Look", null) {
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

        public class BatchAction_AnalyzeCar : BatchAction<CarObject> {
            public static readonly BatchAction_AnalyzeCar Instance = new BatchAction_AnalyzeCar();

            public BatchAction_AnalyzeCar()
                    : base("Analyze", "Check for common issues", null, null) {
                DisplayApply = "Analyze";
                InternalWaitingDialog = true;
                Priority = 1;
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                return ToolsListPage.Launch("Analyze cars", UriExtension.Create("/Pages/ContentTools/CarAnalyzer.xaml?Models=True&Rating=True&Filter={0}",
                        OfType(list).Select(x => $@"""{Filter.Encode(x.Id)}""").JoinToString('|')));
            }
        }

        public class BatchAction_UpdateAmbientShadows : BatchAction<CarObject> {
            public static readonly BatchAction_UpdateAmbientShadows Instance = new BatchAction_UpdateAmbientShadows();

            public BatchAction_UpdateAmbientShadows() : base("Update ambient shadows", "Using previously used params", "Graphics", null) {
                DisplayApply = "Update";
            }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            private AmbientShadowParams _params;

            public override async Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                _params = new AmbientShadowParams();

                var l = OfType(list).ToList();
                for (var i = 0; i < l.Count; i++) {
                    var car = l[i];
                    var subProgress = progress.Subrange((double)i / l.Count, 1d / l.Count).ToDoubleProgress(car.DisplayName);

                    try {
                        await Task.Run(() => {
                            var kn5 = Kn5.FromFile(AcPaths.GetMainCarFilename(car.Location, car.AcdData, false) ?? throw new Exception());
                            using (var renderer = new AmbientShadowRenderer(kn5, car.AcdData) {
                                DiffusionLevel = (float)_params.Diffusion / 100f,
                                SkyBrightnessLevel = (float)_params.Brightness / 100f,
                                Iterations = _params.Iterations,
                                HideWheels = _params.HideWheels,
                                Fade = _params.Fade,
                                CorrectLighting = _params.CorrectLighting,
                                PoissonSampling = _params.PoissonSampling,
                                UpDelta = _params.UpDelta,
                                BodyMultiplier = _params.BodyMultiplier,
                                WheelMultiplier = _params.WheelMultiplier,
                            }) {
                                renderer.Initialize();
                                renderer.Shot(subProgress, cancellation);
                            }
                        });
                    } catch (Exception e) {
                        NonfatalError.Notify(ControlsStrings.CustomShowroom_AmbientShadows_CannotUpdate, e);
                    }

                    subProgress.Report(1d);
                    if (cancellation.IsCancellationRequested) return;
                }

                OnSelectionChanged(list);
            }
        }

        public class BatchAction_FixCarClass : BatchAction<CarObject> {
            public static readonly BatchAction_FixCarClass Instance = new BatchAction_FixCarClass();

            public BatchAction_FixCarClass() : base("Fix car class", "If wrong, try to guess from data and model", "UI", "Batch.FixCarClass") {
                DisplayApply = "Fix";
            }

            public override bool IsAvailable(CarObject obj) {
                return UpdateAll || obj.CarClass != "race" && obj.CarClass != "street";
            }

            private bool _updateAll = ValuesStorage.Get<bool>("_ba.fixCarClass.updateAll");

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

            public BatchAction_FixSpecsFormat() : base("Fix specs format", "This way, they’ll be more readable", "UI", null) {
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
            public BatchAction_SetBrandBadge() : base("Update brand badge", "Will be updated if exists in library", "UI", null) { }

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
            public static readonly BatchAction_SyncCarLogo Instance = new BatchAction_SyncCarLogo();
            public BatchAction_SyncCarLogo() : base("Update car’s logo", "Replace them by brand badges", "Look", "Batch.SyncCarLogo") { }

            public override bool IsAvailable(CarObject obj) {
                return true;
            }

            private bool _differentOnly = ValuesStorage.Get("_ba.syncCarLogos.differentOnly", true);

            public bool DifferentOnly {
                get => _differentOnly;
                set {
                    if (Equals(value, _differentOnly)) return;
                    _differentOnly = value;
                    ValuesStorage.Set("_ba.syncCarLogos.differentOnly", value);
                    OnPropertyChanged();
                }
            }

            private bool _preferHardlinks = ValuesStorage.Get("_ba.syncCarLogos.hardlinks", true);

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
                    FileUtils.HardLinkOrCopy(brandBadge.FullName, logo.FullName, true);
                } else {
                    File.Copy(brandBadge.FullName, logo.FullName, true);
                }
            }
        }

        public class BatchAction_PackCars : CommonBatchActions.BatchAction_Pack<CarObject> {
            public static readonly BatchAction_PackCars Instance = new BatchAction_PackCars();

            public BatchAction_PackCars() : base("Batch.PackCars") { }

            #region Properies
            private bool _packData = ValuesStorage.Get("_ba.packCars.data", true);

            public bool PackData {
                get => _packData;
                set {
                    if (Equals(value, _packData)) return;
                    _packData = value;
                    ValuesStorage.Set("_ba.packCars.data", value);
                    OnPropertyChanged();
                }
            }

            private bool _includeTemplates = ValuesStorage.Get("_ba.packCars.templates", true);

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

        protected override void OnItemDoubleClick(AcObjectNew obj) {
            if (obj is CarObject car) {
                QuickDrive.Show(car);
            }
        }
    }
}