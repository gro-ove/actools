using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;
using AcManager.Controls;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.CustomShowroom;
using AcManager.Pages.ContentTools;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcTools;
using AcTools.AcdFile;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using StringBasedFilter;
using MenuItem = System.Windows.Controls.MenuItem;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarPage_New : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public static bool OptionExtendedMode = true;

        public class ViewModel : SelectedAcObjectViewModel<CarObject> {
            public ViewModel([NotNull] CarObject acObject, bool liteMode = false) : base(acObject) {
                InitializeSpecs();

                if (!liteMode) {
                    AcContext.Instance.CurrentCar = acObject;
                    InitializeLater().Forget();
                }

                if (acObject.Author != AcCommonObject.AuthorKunos) {
                    FancyHints.RecalculateCurves.Trigger();
                    FancyHints.TagsContextMenu.Trigger();
                }
            }

            public async Task InitializeLater() {
                await SelectedObject.GetSoundOrigin();
            }

            public override void Load() {
                base.Load();
                SelectedObject.PropertyChanged += OnObjectPropertyChanged;
            }

            public override void Unload() {
                base.Unload();
                SelectedObject.PropertyChanged -= OnObjectPropertyChanged;
                _helper.Dispose();
                ShowroomPresets = QuickDrivePresets = UpdatePreviewsPresets = null;
            }

            private void OnObjectPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(CarObject.SpecsBhp):
                    case nameof(CarObject.SpecsWeight):
                        if (RecalculatePwRatioAutomatically) {
                            RecalculatePwRatioCommand.Execute();
                        }
                        break;
                    case nameof(AcCommonObject.Year):
                        InnerFilterCommand?.RaiseCanExecuteChanged();
                        break;
                    case nameof(CarObject.Brand):
                        if (SettingsHolder.Content.ChangeBrandIconAutomatically) {
                            var entry = FilesStorage.Instance.GetContentFile(ContentCategory.BrandBadges, SelectedObject.Brand + @".png");
                            if (entry.Exists) {
                                try {
                                    FileUtils.Recycle(SelectedObject.BrandBadge);
                                    File.Copy(entry.Filename, SelectedObject.BrandBadge);
                                    BetterImage.Refresh(SelectedObject.BrandBadge);
                                } catch (Exception ex) {
                                    Logging.Warning(ex);
                                }
                            }
                        }
                        break;
                    case nameof(CarObject.SoundDonorId):
                        SelectedObject.GetSoundOrigin().Forget();
                        break;
                }
            }

            protected override void FilterExec(string type) {
                switch (type) {
                    case "class":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.CarClass) ? @"class-" : $@"class:{Filter.Encode(SelectedObject.CarClass)}");
                        break;

                    case "brand":
                        NewFilterTab(string.IsNullOrWhiteSpace(SelectedObject.Brand) ? @"brand-" : $@"brand:{Filter.Encode(SelectedObject.Brand)}");
                        break;

                    case "power":
                        FilterRange("power", SelectedObject.SpecsBhp);
                        break;

                    case "torque":
                        FilterRange("torque", SelectedObject.SpecsTorque);
                        break;

                    case "weight":
                        FilterRange("weight", SelectedObject.SpecsWeight);
                        break;

                    case "pwratio":
                        FilterRange("pwratio", SelectedObject.SpecsPwRatio, roundTo: 0.01);
                        break;

                    case "topspeed":
                        FilterRange("topspeed", SelectedObject.SpecsTopSpeed);
                        break;

                    case "acceleration":
                        FilterRange("acceleration", SelectedObject.SpecsAcceleration, roundTo: 0.1);
                        break;

                    case "driven":
                        FilterDistance("driven", SelectedObject.TotalDrivenDistance, roundTo: 0.1, range: 0.3);
                        break;

                    case "topspeedachieved":
                        FilterDistance("topspeedachieved", SelectedObject.MaxSpeedAchieved, roundTo: 0.1, range: 0.3);
                        break;
                }

                base.FilterExec(type);
            }

            #region Open In Showroom
            private DelegateCommand<object> _openInShowroomCommand;

            public DelegateCommand<object> OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new DelegateCommand<object>(o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    OpenInCustomShowroomCommand.Execute();
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(SelectedObject, SelectedObject.SelectedSkin?.Id)) {
                    OpenInShowroomOptionsCommand.Execute();
                }
            }, o => SelectedObject.Enabled && SelectedObject.SelectedSkin != null));

            private DelegateCommand _openInShowroomOptionsCommand;

            public DelegateCommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new DelegateCommand(() => {
                new CarOpenInShowroomDialog(SelectedObject, SelectedObject.SelectedSkin?.Id).ShowDialog();
            }, () => SelectedObject.Enabled && SelectedObject.SelectedSkin != null));

            private AsyncCommand _openInCustomShowroomCommand;

            public AsyncCommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ??
                    (_openInCustomShowroomCommand = new AsyncCommand(() => CustomShowroomWrapper.StartAsync(SelectedObject, SelectedObject.SelectedSkin)));

            private AsyncCommand _driveCommand;

            public AsyncCommand DriveCommand => _driveCommand ?? (_driveCommand = new AsyncCommand(async () => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !await QuickDrive.RunAsync(SelectedObject, SelectedObject.SelectedSkin?.Id)) {
                    DriveOptionsCommand.Execute();
                }
            }, () => SelectedObject.Enabled));

            private DelegateCommand _driveOptionsCommand;

            public DelegateCommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => {
                QuickDrive.Show(SelectedObject, SelectedObject.SelectedSkin?.Id);
            }, () => SelectedObject.Enabled));
            #endregion

            #region Auto-Update Previews
            private AsyncCommand _updatePreviewsCommand;

            public AsyncCommand UpdatePreviewsCommand => _updatePreviewsCommand ??
                    (_updatePreviewsCommand = new AsyncCommand(() => new ToUpdatePreview(SelectedObject).Run(), () => SelectedObject.Enabled));

            private AsyncCommand _updatePreviewsManuallyCommand;

            public AsyncCommand UpdatePreviewsManuallyCommand => _updatePreviewsManuallyCommand ??
                    (_updatePreviewsManuallyCommand = new AsyncCommand(() => new ToUpdatePreview(SelectedObject).Run(UpdatePreviewMode.StartManual),
                            () => SelectedObject.Enabled));

            private AsyncCommand _updatePreviewsOptionsCommand;

            public AsyncCommand UpdatePreviewsOptionsCommand => _updatePreviewsOptionsCommand ??
                    (_updatePreviewsOptionsCommand = new AsyncCommand(() => new ToUpdatePreview(SelectedObject).Run(UpdatePreviewMode.Options),
                            () => SelectedObject.Enabled));
            #endregion

            #region Presets
            public HierarchicalItemsView ShowroomPresets {
                get => _showroomPresets;
                set {
                    if (Equals(value, _showroomPresets)) return;
                    _showroomPresets = value;
                    OnPropertyChanged();
                }
            }

            public HierarchicalItemsView CustomShowroomPresets {
                get => _customShowroomPresets;
                set {
                    if (Equals(value, _customShowroomPresets)) return;
                    _customShowroomPresets = value;
                    OnPropertyChanged();
                }
            }

            public HierarchicalItemsView UpdatePreviewsPresets {
                get => _updatePreviewsPresets;
                set {
                    if (Equals(value, _updatePreviewsPresets)) return;
                    _updatePreviewsPresets = value;
                    OnPropertyChanged();
                }
            }

            public HierarchicalItemsView QuickDrivePresets {
                get => _quickDrivePresets;
                set {
                    if (Equals(value, _quickDrivePresets)) return;
                    _quickDrivePresets = value;
                    OnPropertyChanged();
                }
            }

            private HierarchicalItemsView _showroomPresets, _customShowroomPresets, _updatePreviewsPresets, _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public void InitializeShowroomPresets() {
                if (ShowroomPresets == null) {
                    ShowroomPresets = _helper.Create(new PresetsCategory(CarOpenInShowroomDialog.PresetableKeyValue), p => {
                        CarOpenInShowroomDialog.RunPreset(p.Filename, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeCustomShowroomPresets() {
                if (CustomShowroomPresets == null) {
                    CustomShowroomPresets = _helper.Create(new PresetsCategory(DarkRendererSettings.DefaultPresetableKeyValue), p => {
                        CustomShowroomWrapper.StartAsync(SelectedObject, SelectedObject.SelectedSkin, p.Filename);
                    });
                }
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(new PresetsCategory(QuickDrive.PresetableKeyValue), p => {
                        QuickDrive.RunAsync(SelectedObject, SelectedObject.SelectedSkin?.Id, presetFilename: p.Filename).Forget();
                    });
                }
            }

            public void InitializeUpdatePreviewsPresets() {
                if (UpdatePreviewsPresets == null) {
                    UpdatePreviewsPresets = _helper.Create(new PresetsCategory(
                            SettingsHolder.CustomShowroom.CustomShowroomPreviews
                                    ? CmPreviewsSettings.DefaultPresetableKeyValue : CarUpdatePreviewsDialog.PresetableKeyValue),
                            p => new ToUpdatePreview(SelectedObject).Run(p.Filename));
                }
            }
            #endregion

            private DelegateCommand _manageSkinsCommand;

            public DelegateCommand ManageSkinsCommand => _manageSkinsCommand ?? (_manageSkinsCommand = new DelegateCommand(() => {
                CarSkinsListPage.Open(SelectedObject);
            }));

            private DelegateCommand _manageSetupsCommand;

            public DelegateCommand ManageSetupsCommand => _manageSetupsCommand ?? (_manageSetupsCommand = new DelegateCommand(() => {
                CarSetupsListPage.Open(SelectedObject);
            }));

            private string DataDirectory => Path.Combine(SelectedObject.Location, "data");

            private DelegateCommand _readDataCommand;

            public DelegateCommand ReadDataCommand => _readDataCommand ?? (_readDataCommand = new DelegateCommand(() => {
                var source = Path.Combine(SelectedObject.Location, "data.a" + "cd");
                try {
                    var destination = FileUtils.EnsureUnique(DataDirectory);
                    Acd.FromFile(source).ExportDirectory(destination);
                    WindowsHelper.ViewDirectory(destination);
                } catch (Exception e) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotReadData, e);
                }
            }, () => SettingsHolder.Common.MsMode && SelectedObject.AcdData?.IsPacked == true));

            private DelegateCommand _packDataCommand;

            public DelegateCommand PackDataCommand => _packDataCommand ?? (_packDataCommand = new DelegateCommand(() => {
                try {
                    var destination = Path.Combine(SelectedObject.Location, "data.a" + "cd");
                    var exists = File.Exists(destination);

                    if (SelectedObject.Author == AcCommonObject.AuthorKunos && ModernDialog.ShowMessage(
                            AppStrings.Car_PackKunosDataMessage, ToolsStrings.Common_Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes ||
                            SelectedObject.Author != AcCommonObject.AuthorKunos && exists && ModernDialog.ShowMessage(
                                    AppStrings.Car_PackExistingDataMessage, ToolsStrings.Common_Warning, MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                        return;
                    }

                    if (exists) {
                        FileUtils.Recycle(destination);
                    }

                    Acd.FromDirectory(DataDirectory).Save(destination);
                    WindowsHelper.ViewFile(destination);
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.Car_CannotPackData, ToolsStrings.Common_MakeSureThereIsEnoughSpace, e);
                }
            }, () => Directory.Exists(DataDirectory)));

            private DelegateCommand _extractSoundCommand;

            public DelegateCommand ExtractSoundCommand => _extractSoundCommand ?? (_extractSoundCommand = new DelegateCommand(async () => {
                try {
                    var filename = PluginsManager.Instance.GetPluginFilename("FmodHelper", "helper.exe");
                    if (!File.Exists(filename)) {
                        throw new InformativeException("Can’t extract sounds", "Helper executable not found");
                    }

                    if (!File.Exists(SelectedObject.SoundbankFilename)) {
                        throw new InformativeException("Can’t extract sounds", "Car soundbank not found");
                    }

                    var destination = FileUtils.EnsureUnique(Path.Combine(SelectedObject.Location, "sfx-samples"));
                    using (WaitingDialog.Create("Extracting…"))
                    using (var process = ProcessExtension.Start(filename, new[] {
                        "/C", "/V1", "/S1", "/L:sfx-samples/log.txt", "/O:sfx-samples",
                        FileUtils.GetRelativePath(SelectedObject.SoundbankFilename, SelectedObject.Location)
                    }, new ProcessStartInfo {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = SelectedObject.Location
                    })) {
                        await process.WaitForExitAsync();
                        if (process.HasExitedSafe() && process.ExitCode != 0) {
                            throw new InformativeException("Can’t extract sounds", $"Exit code: {process.ExitCode}");
                        }

                        WindowsHelper.ViewDirectory(destination);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t extract sounds", ToolsStrings.Common_MakeSureThereIsEnoughSpace, e);
                }
            }, () => SettingsHolder.Common.MsMode && PluginsManager.Instance.IsPluginEnabled("FmodHelper")));

            private AsyncCommand _replaceSoundCommand;

            public AsyncCommand ReplaceSoundCommand => _replaceSoundCommand ??
                    (_replaceSoundCommand = new AsyncCommand(() => CarSoundReplacer.Replace(SelectedObject)));

            private AsyncCommand _replaceTyresCommand;

            public AsyncCommand ReplaceTyresCommand => _replaceTyresCommand ??
                    (_replaceTyresCommand = new AsyncCommand(() => CarReplaceTyresDialog.RunAsync(SelectedObject)));

            private DelegateCommand _carAnalyzerCommand;

            public DelegateCommand CarAnalyzerCommand => _carAnalyzerCommand ?? (_carAnalyzerCommand = new DelegateCommand(() => {
                CarAnalyzer.Run(SelectedObject);
            }));

            #region Specs editor
            private const string KeyRecalculatePwRatioAutomatically = "SelectedCarPage.RecalculatePwRatioAutomatically";
            private bool _recalculatePwRatioAutomatically = ValuesStorage.Get(KeyRecalculatePwRatioAutomatically, true);

            public bool RecalculatePwRatioAutomatically {
                get => _recalculatePwRatioAutomatically;
                set {
                    if (Equals(value, _recalculatePwRatioAutomatically)) return;
                    _recalculatePwRatioAutomatically = value;
                    OnPropertyChanged();

                    ValuesStorage.Set(KeyRecalculatePwRatioAutomatically, value);
                    if (value) {
                        RecalculatePwRatioCommand.Execute();
                    }
                }
            }

            private DelegateCommand _recalculatePwRatioCommand;

            public DelegateCommand RecalculatePwRatioCommand => _recalculatePwRatioCommand ?? (_recalculatePwRatioCommand = new DelegateCommand(() => {
                var obj = SelectedObject;
                if (!FlexibleParser.TryParseDouble(obj.SpecsBhp, out var power) ||
                        !FlexibleParser.TryParseDouble(obj.SpecsWeight, out var weight)) return;
                var ratio = weight / power;
                obj.SpecsPwRatio = SpecsFormat(AppStrings.CarSpecs_PwRatio_FormatTooltip, ratio.Round(0.01));
            }));

            private DelegateCommand _recalculateWeightCommand;

            public DelegateCommand RecalculateWeightCommand => _recalculateWeightCommand ?? (_recalculateWeightCommand = new DelegateCommand(() => {
                var data = SelectedObject.AcdData;
                var weight = data?.GetIniFile("car.ini")["BASIC"].GetInt("TOTALMASS", 0);
                if (weight == null || data.IsEmpty || weight < CommonAcConsts.DriverWeight) {
                    MessageBox.Show("Data is damaged", ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OK);
                    return;
                }

                SelectedObject.SpecsWeight = SpecsFormat(AppStrings.CarSpecs_Weight_FormatTooltip,
                        (weight.Value - CommonAcConsts.DriverWeight).ToString(@"F0", CultureInfo.InvariantCulture));
            }));

            private void InitializeSpecs() {
                RegisterSpec("power", AppStrings.CarSpecs_Power_FormatTooltip, nameof(SelectedObject.SpecsBhp));
                RegisterSpec("torque", AppStrings.CarSpecs_Torque_FormatTooltip, nameof(SelectedObject.SpecsTorque));
                RegisterSpec("weight", AppStrings.CarSpecs_Weight_FormatTooltip, nameof(SelectedObject.SpecsWeight));
                RegisterSpec("topspeed", AppStrings.CarSpecs_MaxSpeed_FormatTooltip, nameof(SelectedObject.SpecsTopSpeed));
                RegisterSpec("acceleration", AppStrings.CarSpecs_Acceleration_FormatTooltip, nameof(SelectedObject.SpecsAcceleration));
                RegisterSpec("pwratio", AppStrings.CarSpecs_PwRatio_FormatTooltip, nameof(SelectedObject.SpecsPwRatio));
            }

            private static readonly Regex FixAccelerationRegex = new Regex(@"0\s*[-–—]\s*\d\d+", RegexOptions.Compiled);

            protected override void FixFormat(string key) {
                if (key == @"power") {
                    var originalValue = GetSpecsValue(key);
                    if (originalValue == null) return;
                    if (originalValue.IndexOf('*') != -1 || originalValue.IndexOf("whp", StringComparison.OrdinalIgnoreCase) != -1) {
                        var fixedValue = FixFormatCommon(originalValue);
                        var replacement = FlexibleParser.TryParseDouble(fixedValue, out var actualValue) ? actualValue.Round(0.01).ToInvariantString() : @"--";
                        fixedValue = SpecsFormat(AppStrings.CarSpecs_PowerAtWheels_FormatTooltip, replacement);
                        SetSpecsValue(key, fixedValue);
                        return;
                    }
                }

                base.FixFormat(key);
            }

            protected override string FixFormatCommon(string value) {
                return FixAccelerationRegex.Replace(value, "");
            }

            private DelegateCommand _scaleCurvesCommand;

            public DelegateCommand ScaleCurvesCommand => _scaleCurvesCommand ?? (_scaleCurvesCommand = new DelegateCommand(() => {
                var o = SelectedObject;

                var power = FlexibleParser.TryParseDouble(o.SpecsBhp);
                var torque = FlexibleParser.TryParseDouble(o.SpecsTorque);
                if (!power.HasValue && !torque.HasValue) {
                    ModernDialog.ShowMessage(AppStrings.CarSpecs_SpecifyPowerAndTorqueFirst, ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OK);
                    return;
                }

                Lut powerCurve = null, torqueCurve = null;

                if (!torque.HasValue) {
                    powerCurve = o.SpecsPowerCurve?.ToLut();
                    if (powerCurve != null) {
                        powerCurve.ScaleToSelf(power.Value);

                        var temporaryCurve = TorquePhysicUtils.PowerToTorque(powerCurve);
                        temporaryCurve.UpdateBoundingBox();
                        torque = temporaryCurve.MaxY;
                    } else return;
                } else if (!power.HasValue) {
                    torqueCurve = o.SpecsTorqueCurve?.ToLut();
                    if (torqueCurve != null) {
                        torqueCurve.ScaleToSelf(torque.Value);

                        var temporaryCurve = TorquePhysicUtils.TorqueToPower(torqueCurve);
                        temporaryCurve.UpdateBoundingBox();
                        power = temporaryCurve.MaxY;
                    } else return;
                }

                if (powerCurve == null) {
                    powerCurve = o.SpecsPowerCurve?.ToLut();
                    powerCurve?.ScaleToSelf(power.Value);
                }

                if (torqueCurve == null) {
                    torqueCurve = o.SpecsTorqueCurve?.ToLut();
                    torqueCurve?.ScaleToSelf(torque.Value);
                }

                if (powerCurve != null) {
                    o.SpecsPowerCurve = new GraphData(powerCurve);
                }

                if (torqueCurve != null) {
                    o.SpecsTorqueCurve = new GraphData(torqueCurve);
                }
            }));

            private DelegateCommand _recalculateAndScaleCurvesCommand;

            public DelegateCommand RecalculateAndScaleCurvesCommand => _recalculateAndScaleCurvesCommand ??
                    (_recalculateAndScaleCurvesCommand = new DelegateCommand(() => {
                        var o = SelectedObject;

                        var power = FlexibleParser.TryParseDouble(o.SpecsBhp);
                        var torque = FlexibleParser.TryParseDouble(o.SpecsTorque);
                        if (!power.HasValue && !torque.HasValue) {
                            ModernDialog.ShowMessage(AppStrings.CarSpecs_SpecifyPowerAndTorqueFirst, ToolsStrings.Common_CannotDo_Title, MessageBoxButton.OK);
                            return;
                        }

                        var data = o.AcdData;
                        if (data == null) {
                            NonfatalError.Notify(ToolsStrings.Common_CannotDo_Title, "Data is damaged");
                            return;
                        }

                        Lut torqueCurve, powerCurve;
                        try {
                            torqueCurve = TorquePhysicUtils.LoadCarTorque(data);
                            powerCurve = TorquePhysicUtils.TorqueToPower(torqueCurve);
                        } catch (Exception ex) {
                            NonfatalError.Notify(ToolsStrings.Common_CannotDo_Title, ex);
                            return;
                        }

                        if (power.HasValue) {
                            powerCurve.ScaleToSelf(power.Value);
                        }

                        if (torque.HasValue) {
                            torqueCurve.ScaleToSelf(torque.Value);
                        }

                        if (!torque.HasValue) {
                            var temporaryCurve = TorquePhysicUtils.PowerToTorque(powerCurve);
                            temporaryCurve.UpdateBoundingBox();
                            torque = temporaryCurve.MaxY;

                            torqueCurve.ScaleToSelf(torque.Value);
                        } else if (!power.HasValue) {
                            var temporaryCurve = TorquePhysicUtils.TorqueToPower(torqueCurve);
                            temporaryCurve.UpdateBoundingBox();
                            power = temporaryCurve.MaxY;

                            powerCurve.ScaleToSelf(power.Value);
                        }

                        o.SpecsPowerCurve = new GraphData(powerCurve);
                        o.SpecsTorqueCurve = new GraphData(torqueCurve);
                    }));

            private DelegateCommand _recalculateCurvesCommand;

            public DelegateCommand RecalculateCurvesCommand => _recalculateCurvesCommand ?? (_recalculateCurvesCommand = new DelegateCommand(() => {
                var o = SelectedObject;

                var data = o.AcdData;
                if (data == null) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotDo_Title, "Data is damaged");
                    return;
                }

                Lut torque, power;
                try {
                    torque = TorquePhysicUtils.LoadCarTorque(data);
                    power = TorquePhysicUtils.TorqueToPower(torque);
                } catch (Exception ex) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotDo_Title, ex);
                    return;
                }

                var dlg = new CarTransmissionLossSelector(o, torque.MaxY, power.MaxY);
                dlg.ShowDialog();
                if (!dlg.IsResultOk) return;

                torque.TransformSelf(x => x.Y * dlg.Multipler);
                power.TransformSelf(x => x.Y * dlg.Multipler);

                o.SpecsTorqueCurve = new GraphData(torque);
                o.SpecsPowerCurve = new GraphData(power);

                if (ModernDialog.ShowMessage(AppStrings.CarSpecs_CopyNewPowerAndTorque, AppStrings.Common_OneMoreThing, MessageBoxButton.YesNo,
                        "copyNewPowerAndTorque") == MessageBoxResult.Yes) {
                    // MaxY values were updated while creating new GraphData instances above
                    o.SpecsTorque = SpecsFormat(AppStrings.CarSpecs_Torque_FormatTooltip, torque.MaxY.ToString(@"F0", CultureInfo.InvariantCulture))
                            + (dlg.Multipler == 1d ? "*" : "");
                    o.SpecsBhp = SpecsFormat(dlg.Multipler == 1d ? AppStrings.CarSpecs_PowerAtWheels_FormatTooltip : AppStrings.CarSpecs_Power_FormatTooltip,
                            power.MaxY.ToString(@"F0", CultureInfo.InvariantCulture));
                }
            }));
            #endregion
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception(ToolsStrings.Common_IdIsMissing);
            }
        }

        private CarObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await CarsManager.Instance.GetByIdAsync(_id);
            if (_object == null) return;
            await _object.SkinsManager.EnsureLoadedAsync();
        }

        void ILoadableContent.Load() {
            _object = CarsManager.Instance.GetById(_id);
            _object?.SkinsManager.EnsureLoaded();
        }

        bool IImmediateContent.ImmediateChange(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) return false;

            var obj = CarsManager.Instance.GetById(id);
            if (obj == null) return false;

            _object.SkinsManager.EnsureLoadedAsync().Forget();

            _id = id;
            _object = obj;
            SetModel();
            return true;
        }

        private ViewModel _model;

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException(AppStrings.Common_CannotFindObjectById);

            SetModel();
            InitializeComponent();
            this.AddWidthCondition(800).Add(x => ExtendedMode = OptionExtendedMode && x);
            this.AddWidthCondition(x => x >= 1200 ? 180 : x >= 1000 ? 144 : 108).Add(x => SkinsListExtendedModeParent.MaxHeight = x);

            if (!AppAppearanceManager.Instance.PopupToolBars) {
                FancyHints.AccidentallyRemoved.Trigger();
            }
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewsCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.UpdatePreviewsCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt)),
                new InputBinding(_model.UpdatePreviewsOptionsCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.UpdatePreviewsManuallyCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt)),

                new InputBinding(_model.RecalculatePwRatioCommand, new KeyGesture(Key.W, ModifierKeys.Alt)),

                new InputBinding(_model.DriveCommand, new KeyGesture(Key.G, ModifierKeys.Control)),
                new InputBinding(_model.DriveOptionsCommand, new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift)),

                new InputBinding(_model.OpenInShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Control)),
                new InputBinding(_model.OpenInShowroomOptionsCommand, new KeyGesture(Key.H, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt)),
                new InputBinding(_model.OpenInCustomShowroomCommand, new KeyGesture(Key.H, ModifierKeys.Alt | ModifierKeys.Control)),

                new InputBinding(_model.ManageSkinsCommand, new KeyGesture(Key.K, ModifierKeys.Control)),
                new InputBinding(_model.ManageSetupsCommand, new KeyGesture(Key.U, ModifierKeys.Control)),

                new InputBinding(_model.PackDataCommand, new KeyGesture(Key.J, ModifierKeys.Control)),
                new InputBinding(_model.ReadDataCommand, new KeyGesture(Key.J, ModifierKeys.Alt)),

                new InputBinding(_model.CarAnalyzerCommand, new KeyGesture(Key.A, ModifierKeys.Alt)),
                new InputBinding(_model.ReplaceTyresCommand, new KeyGesture(Key.T, ModifierKeys.Alt)),
                new InputBinding(_model.ReplaceSoundCommand, new KeyGesture(Key.S, ModifierKeys.Alt)),

                new InputBinding(_model.FixFormatCommand, new KeyGesture(Key.F, ModifierKeys.Alt)),
            });
        }

        #region Skins
        private void OnPreviewClick(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                e.Handled = true;
                if (SettingsHolder.CustomShowroom.CustomShowroomInstead) {
                    CustomShowroomWrapper.StartAsync(_model.SelectedObject, _model.SelectedObject.SelectedSkin);
                } else {
                    CarOpenInShowroomDialog.Run(_model.SelectedObject, _model.SelectedObject.SelectedSkin?.Id);
                }
            } else if (e.ClickCount == 1 && ReferenceEquals(sender, SelectedSkinPreviewImage) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;
                var skins = _model.SelectedObject.EnabledOnlySkins.ToList();
                new ImageViewer(
                        from skin in skins select skin.PreviewImage,
                        skins.IndexOf(_model.SelectedObject.SelectedSkin),
                        CommonAcConsts.PreviewWidth,
                        details: CarBlock.GetSkinImageViewerDetailsCallback(_model.SelectedObject)).ShowDialog();
            }
        }

        private void OnPreviewRightClick(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            var context = ((FrameworkElement)sender).DataContext;
            var wrapper = context as AcItemWrapper;
            OpenSkinContextMenu((wrapper?.Value ?? context) as CarSkinObject);
        }

        private void OpenSkinContextMenu(CarSkinObject skin) {
            if (skin == null) return;

            var contextMenu = new ContextMenu {
                Items = {
                    new MenuItem {
                        Header = string.Format(AppStrings.Car_SkinFormat, skin.DisplayName?.Replace(@"_", @"__") ?? @"?"),
                        StaysOpenOnClick = true
                    }
                }
            };

            var item = new MenuItem { Header = ControlsStrings.Car_OpenInShowroom, InputGestureText = @"Ctrl+H" };
            item.Click += (sender, args) => CarOpenInShowroomDialog.Run(_model.SelectedObject, skin.Id);
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = ControlsStrings.Car_OpenInCustomShowroom, InputGestureText = @"Alt+H" };
            item.Click += (sender, args) => CustomShowroomWrapper.StartAsync(_model.SelectedObject, skin);
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new MenuItem {
                Header = AppStrings.Toolbar_Folder,
                Command = skin.ViewInExplorerCommand
            });

            contextMenu.Items.Add(new Separator());

            item = new MenuItem { Header = AppStrings.Toolbar_UpdatePreview };
            item.Click += (sender, args) => new ToUpdatePreview(_model.SelectedObject, skin).Run();
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());

            item = new MenuItem { Header = AppStrings.Toolbar_ChangeLivery };
            item.Click += (sender, args) => new LiveryIconEditorDialog(skin).ShowDialog();
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = AppStrings.Toolbar_GenerateLivery, ToolTip = AppStrings.Solution_GenerateLivery_Details };
            item.Click += (sender, args) => LiveryIconEditor.GenerateAsync(skin).Forget();
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = AppStrings.Toolbar_GenerateRandomLivery, ToolTip = AppStrings.Solution_RandomLivery_Details };
            item.Click += (sender, args) => LiveryIconEditor.GenerateRandomAsync(skin).Forget();
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());

            contextMenu.Items.Add(new MenuItem {
                Header = AppStrings.Car_DeleteSkin,
                ToolTip = AppStrings.Car_DeleteSkin_Tooltip,
                Command = skin.DeleteCommand
            });

            contextMenu.IsOpen = true;
        }
        #endregion

        #region Presets (Dynamic Loading)
        private void OnShowroomButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeShowroomPresets();
        }

        private void OnCustomShowroomButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeCustomShowroomPresets();
        }

        private void OnDriveButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }

        private void OnUpdatePreviewsButtonMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeUpdatePreviewsPresets();
        }
        #endregion

        #region Icons & Specs
        private void OnIconClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                new BrandBadgeEditor((CarObject)SelectedAcObject).ShowDialog();
            }
        }

        private void OnUpgradeIconClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new UpgradeIconEditor((CarObject)SelectedAcObject).ShowDialog();
            }
        }

        private void OnParentBlockClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new ChangeCarParentDialog((CarObject)SelectedAcObject).ShowDialog();
            }
        }
        #endregion

        private bool _extendedMode;

        public bool ExtendedMode {
            get => _extendedMode;
            set {
                if (Equals(value, _extendedMode)) return;
                _extendedMode = value;

                Decorator oldParent, newParent;
                if (value) {
                    oldParent = SkinsListCompactModeParent;
                    newParent = SkinsListExtendedModeParent;
                } else {
                    oldParent = SkinsListExtendedModeParent;
                    newParent = SkinsListCompactModeParent;
                }

                oldParent.Child = null;
                oldParent.Visibility = Visibility.Collapsed;
                newParent.Child = SkinsList;
                newParent.Visibility = Visibility.Visible;

                SkinsList.ItemsPanel = TryFindResource(value ? @"ExtendedSkinsPanel" : @"CompactSkinsPanel") as ItemsPanelTemplate;
                ScrollViewer.SetHorizontalScrollBarVisibility(SkinsList, value ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
                ScrollViewer.SetVerticalScrollBarVisibility(SkinsList, value ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
                HorizontalScrollBehavior.IsEnabled = !value;
            }
        }

        private void OnPowerGraphContextMenuClick(object sender, ContextMenuButtonEventArgs e) {
            e.Menu = new ContextMenu()
                    .AddItem(AppStrings.CarSpecs_ScaleCurvesToPowerTorqueHeader, _model.ScaleCurvesCommand,
                            toolTip: AppStrings.CarSpecs_ScaleCurvesToPowerTorque_Tooltip)
                    .AddItem(AppStrings.CarSpecs_RecalculateCurvesUsingDataAndPowerTorqueHeader, _model.RecalculateAndScaleCurvesCommand,
                            toolTip: AppStrings.CarSpecs_RecalculateCurvesUsingDataAndPowerTorque_Tooltip)
                    .AddItem(AppStrings.CarSpecs_RecalculateCurvesUsingDataOnlyHeader, _model.RecalculateCurvesCommand,
                            toolTip: AppStrings.CarSpecs_RecalculateCurvesUsingDataOnly_Tooltip);
        }

        public static readonly string TatuusId = @"tatuusfa1";

        private void OnSoundBlockClick(object sender, MouseButtonEventArgs e) {
            var obj = _model.SelectedObject;
            if (obj.SoundDonorId == TatuusId) {
                ModernDialog.ShowMessage(
                        $"Most likely, sound is not from {obj.SoundDonor?.DisplayName ?? obj.SoundDonorId}, but instead it’s based on Kunos sample soundbank and its author forgot to change GUIDs before compiling it. Usually it’s not a problem, but in a race with two different cars having same GUIDs, one of them will use sound of another one.\n\nIf you’re the author, please, consider [url=\"https://www.youtube.com/watch?v=BdKsHBn8wh4\"]fixing it[/url].",
                        ToolsStrings.Common_Warning, MessageBoxButton.OK);
            }
        }

        private void OnTsmSetupsButtonClick(object sender, RoutedEventArgs e) {
            CarSetupsListPage.Open(_object, CarSetupsRemoteSource.TheSetupMarket);
        }
    }
}
