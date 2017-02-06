using System;
using System.ComponentModel;
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
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Pages.ContentTools;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.Profile;
using AcManager.Tools.SharedMemory;
using AcTools;
using AcTools.AcdFile;
using AcTools.LapTimes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using StringBasedFilter;
using MenuItem = System.Windows.Controls.MenuItem;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Pages.Selected {
    public partial class SelectedCarPage_New : ILoadableContent, IParametrizedUriContent, IImmediateContent {
        public static bool OptionExtendedMode = true;

        public class ViewModel : SelectedAcObjectViewModel<CarObject> {
            private double? _totalDrivenDistance;

            public double? TotalDrivenDistance {
                get { return _totalDrivenDistance; }
                set {
                    if (Equals(value, _totalDrivenDistance)) return;
                    _totalDrivenDistance = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel([NotNull] CarObject acObject) : base(acObject) {
                TotalDrivenDistance = SelectedObject.TotalDrivenDistance / 1e3;
                InitializeLater().Forget();
            }

            public async Task InitializeLater() {
                await SelectedObject.GetSoundOrigin();
                await Task.Delay(500);
                await LapTimesManager.Instance.UpdateAsync();
                UpdateLapTimes();
            }

            private void UpdateLapTimes() {
                LapTimes = new BetterObservableCollection<LapTimeWrapped>(
                        LapTimesManager.Instance.Entries.Where(x => x.CarId == SelectedObject.Id)
                                       .OrderBy(x => PlayerStatsManager.Instance.GetDistanceDrivenAtTrackAcId(x.TrackAcId))
                                       .Take(10)
                                       .Select(x => new LapTimeWrapped(x)));
            }

            private BetterObservableCollection<LapTimeWrapped> _lapTimes;

            public BetterObservableCollection<LapTimeWrapped> LapTimes {
                get { return _lapTimes; }
                set {
                    if (Equals(value, _lapTimes)) return;
                    _lapTimes = value;
                    OnPropertyChanged();
                }
            }

            public override void Load() {
                base.Load();
                SelectedObject.PropertyChanged += OnObjectPropertyChanged;
                LapTimesManager.Instance.NewEntryAdded += OnNewLapTimeAdded;
            }

            public override void Unload() {
                base.Unload();
                SelectedObject.PropertyChanged -= OnObjectPropertyChanged;
                LapTimesManager.Instance.NewEntryAdded -= OnNewLapTimeAdded;
                _helper.Dispose();
                ShowroomPresets = QuickDrivePresets = UpdatePreviewsPresets = null;
            }

            private void OnNewLapTimeAdded(object sender, EventArgs e) {
                Logging.Here();
                UpdateLapTimes();
            }

            private void OnObjectPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(CarObject.TotalDrivenDistance):
                        TotalDrivenDistance = SelectedObject.TotalDrivenDistance / 1e3;
                        break;
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
                                } catch (Exception ex) {
                                    Logging.Warning(ex);
                                }
                            }
                        }
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
                }

                base.FilterExec(type);
            }

            #region Open In Showroom
            private CommandBase _openInShowroomCommand;

            public ICommand OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new DelegateCommand<object>(o => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    OpenInCustomShowroomCommand.Execute(o);
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(SelectedObject, SelectedObject.SelectedSkin?.Id)) {
                    OpenInShowroomOptionsCommand.Execute(null);
                }
            }, o => SelectedObject.Enabled && SelectedObject.SelectedSkin != null));

            private CommandBase _openInShowroomOptionsCommand;

            public ICommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new DelegateCommand(() => {
                new CarOpenInShowroomDialog(SelectedObject, SelectedObject.SelectedSkin?.Id).ShowDialog();
            }, () => SelectedObject.Enabled && SelectedObject.SelectedSkin != null));

            private CommandBase _openInCustomShowroomCommand;

            public ICommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ??
                    (_openInCustomShowroomCommand = new AsyncCommand<CustomShowroomMode?>(type => type.HasValue
                            ? CustomShowroomWrapper.StartAsync(type.Value, SelectedObject, SelectedObject.SelectedSkin)
                            : CustomShowroomWrapper.StartAsync(SelectedObject, SelectedObject.SelectedSkin)));

            private CommandBase _driveCommand;

            public ICommand DriveCommand => _driveCommand ?? (_driveCommand = new DelegateCommand(() => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !QuickDrive.Run(SelectedObject, SelectedObject.SelectedSkin?.Id)) {
                    DriveOptionsCommand.Execute(null);
                }
            }, () => SelectedObject.Enabled));

            private CommandBase _driveOptionsCommand;

            public ICommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => {
                QuickDrive.Show(SelectedObject, SelectedObject.SelectedSkin?.Id);
            }, () => SelectedObject.Enabled));
            #endregion

            #region Auto-Update Previews
            private ICommand _updatePreviewsCommand;

            public ICommand UpdatePreviewsCommand => _updatePreviewsCommand ?? (_updatePreviewsCommand = new DelegateCommand(() => {
                new CarUpdatePreviewsDialog(SelectedObject, GetAutoUpdatePreviewsDialogMode()).ShowDialog();
            }, () => SelectedObject.Enabled));

            private ICommand _updatePreviewsManuallyCommand;

            public ICommand UpdatePreviewsManuallyCommand => _updatePreviewsManuallyCommand ?? (_updatePreviewsManuallyCommand = new DelegateCommand(() => {
                new CarUpdatePreviewsDialog(SelectedObject, CarUpdatePreviewsDialog.DialogMode.StartManual).ShowDialog();
            }, () => SelectedObject.Enabled));

            private ICommand _updatePreviewsOptionsCommand;

            public ICommand UpdatePreviewsOptionsCommand => _updatePreviewsOptionsCommand ?? (_updatePreviewsOptionsCommand = new DelegateCommand(() => {
                new CarUpdatePreviewsDialog(SelectedObject, CarUpdatePreviewsDialog.DialogMode.Options).ShowDialog();
            }, () => SelectedObject.Enabled));

            public static CarUpdatePreviewsDialog.DialogMode GetAutoUpdatePreviewsDialogMode() {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return CarUpdatePreviewsDialog.DialogMode.Options;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return CarUpdatePreviewsDialog.DialogMode.StartManual;
                return CarUpdatePreviewsDialog.DialogMode.Start;
            }
            #endregion

            #region Presets
            public HierarchicalItemsView ShowroomPresets {
                get { return _showroomPresets; }
                set {
                    if (Equals(value, _showroomPresets)) return;
                    _showroomPresets = value;
                    OnPropertyChanged();
                }
            }

            public HierarchicalItemsView UpdatePreviewsPresets {
                get { return _updatePreviewsPresets; }
                set {
                    if (Equals(value, _updatePreviewsPresets)) return;
                    _updatePreviewsPresets = value;
                    OnPropertyChanged();
                }
            }

            public HierarchicalItemsView QuickDrivePresets {
                get { return _quickDrivePresets; }
                set {
                    if (Equals(value, _quickDrivePresets)) return;
                    _quickDrivePresets = value;
                    OnPropertyChanged();
                }
            }

            private HierarchicalItemsView _showroomPresets, _updatePreviewsPresets, _quickDrivePresets;
            private readonly PresetsMenuHelper _helper = new PresetsMenuHelper();

            public void InitializeShowroomPresets() {
                if (ShowroomPresets == null) {
                    ShowroomPresets = _helper.Create(CarOpenInShowroomDialog.PresetableKeyValue, p => {
                        CarOpenInShowroomDialog.RunPreset(p.Filename, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeQuickDrivePresets() {
                if (QuickDrivePresets == null) {
                    QuickDrivePresets = _helper.Create(QuickDrive.PresetableKeyValue, p => {
                        QuickDrive.RunPreset(p.Filename, SelectedObject, SelectedObject.SelectedSkin?.Id);
                    });
                }
            }

            public void InitializeUpdatePreviewsPresets() {
                if (UpdatePreviewsPresets == null) {
                    UpdatePreviewsPresets = _helper.Create(CarUpdatePreviewsDialog.PresetableKeyValue, p => {
                        new CarUpdatePreviewsDialog(SelectedObject, GetAutoUpdatePreviewsDialogMode(), p.Filename).ShowDialog();
                    });
                }
            }
            #endregion

            private CommandBase _manageSkinsCommand;

            public ICommand ManageSkinsCommand => _manageSkinsCommand ?? (_manageSkinsCommand = new DelegateCommand(() => {
                new CarSkinsDialog(SelectedObject) {
                    ShowInTaskbar = false
                }.ShowDialogWithoutBlocking();
            }));

            private CommandBase _manageSetupsCommand;

            public ICommand ManageSetupsCommand => _manageSetupsCommand ?? (_manageSetupsCommand = new DelegateCommand(() => {
                new CarSetupsDialog(SelectedObject) {
                    ShowInTaskbar = false
                }.ShowDialogWithoutBlocking();
            }));

            private string DataDirectory => Path.Combine(SelectedObject.Location, "data");

            private CommandBase _readDataCommand;

            public ICommand ReadDataCommand => _readDataCommand ?? (_readDataCommand = new DelegateCommand(() => {
                var source = Path.Combine(SelectedObject.Location, "data.a" + "cd");
                try {
                    var destination = FileUtils.EnsureUnique(DataDirectory);
                    Acd.FromFile(source).ExportDirectory(destination);
                    WindowsHelper.ViewDirectory(destination);
                } catch (Exception e) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotReadData, e);
                }
            }, () => SettingsHolder.Common.MsMode && SelectedObject.AcdData?.IsPacked == true));

            private CommandBase _packDataCommand;

            public ICommand PackDataCommand => _packDataCommand ?? (_packDataCommand = new DelegateCommand(() => {
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
            }, () => SettingsHolder.Common.DeveloperMode && Directory.Exists(DataDirectory)));

            private CommandBase _replaceSoundCommand;

            public ICommand ReplaceSoundCommand => _replaceSoundCommand ?? (_replaceSoundCommand = new AsyncCommand(() => {
                var donor = SelectCarDialog.Show();
                return donor == null ? Task.Delay(0) : SelectedObject.ReplaceSound(donor);
            }));

            private AsyncCommand _replaceTyresCommand;

            public AsyncCommand ReplaceTyresCommand => _replaceTyresCommand ??
                    (_replaceTyresCommand = new AsyncCommand(() => CarReplaceTyresDialog.Run(SelectedObject)));

            private AsyncCommand _migrationHelperCommand;

            public AsyncCommand MigrationHelperCommand => _migrationHelperCommand ?? (_migrationHelperCommand = new AsyncCommand(() => {
                var dialog = new ModernDialog {
                    Title = "Migration Helper",
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.CanResizeWithGrip,
                    LocationAndSizeKey = @"lsMigrationHelper",
                    MinWidth = 800,
                    MinHeight = 480,
                    Width = 800,
                    Height = 640,
                    MaxWidth = 99999,
                    MaxHeight = 99999,
                    Content = new ModernFrame {
                        Source = UriExtension.Create("/Pages/ContentTools/MigrationHelper.xaml?Id={0}&Models=True", SelectedObject.Id)
                    }
                };
                return dialog.ShowAndWaitAsync();
            }));

            #region Specs editor
            private const string KeyRecalculatePwRatioAutomatically = "SelectedCarPage.RecalculatePwRatioAutomatically";
            private bool _recalculatePwRatioAutomatically = ValuesStorage.GetBool(KeyRecalculatePwRatioAutomatically, true);

            public bool RecalculatePwRatioAutomatically {
                get { return _recalculatePwRatioAutomatically; }
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

                double power, weight;
                if (!FlexibleParser.TryParseDouble(obj.SpecsBhp, out power) ||
                        !FlexibleParser.TryParseDouble(obj.SpecsWeight, out weight)) return;

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

            private static string SpecsFormat(string format, object value) {
                return format.Replace(@"…", value.ToInvariantString());
            }

            private DelegateCommand<string> _fixFormatCommand;

            public DelegateCommand<string> FixFormatCommand => _fixFormatCommand ?? (_fixFormatCommand = new DelegateCommand<string>(key => {
                if (key == null) {
                    foreach (var k in new[] { @"power", @"torque", @"weight", @"topspeed", @"acceleration", @"pwratio" }) {
                        FixFormat(k);
                    }
                } else {
                    FixFormat(key);
                }
            }, key => key == null || !IsFormatCorrect(key)));

            [NotNull]
            private static string GetFormat(string key) {
                switch (key) {
                    case "power":
                        return AppStrings.CarSpecs_Power_FormatTooltip;
                    case "torque":
                        return AppStrings.CarSpecs_Torque_FormatTooltip;
                    case "weight":
                        return AppStrings.CarSpecs_Weight_FormatTooltip;
                    case "topspeed":
                        return AppStrings.CarSpecs_MaxSpeed_FormatTooltip;
                    case "acceleration":
                        return AppStrings.CarSpecs_Acceleration_FormatTooltip;
                    case "pwratio":
                        return AppStrings.CarSpecs_PwRatio_FormatTooltip;
                    default:
                        return @"…";
                }
            }

            [CanBeNull]
            private string GetSpecsValue(string key) {
                switch (key) {
                    case "power":
                        return SelectedObject.SpecsBhp;
                    case "torque":
                        return SelectedObject.SpecsTorque;
                    case "weight":
                        return SelectedObject.SpecsWeight;
                    case "topspeed":
                        return SelectedObject.SpecsTopSpeed;
                    case "acceleration":
                        return SelectedObject.SpecsAcceleration;
                    case "pwratio":
                        return SelectedObject.SpecsPwRatio;
                    default:
                        return null;
                }
            }
            
            private void SetSpecsValue(string key, string value) {
                switch (key) {
                    case "power":
                        SelectedObject.SpecsBhp = value;
                        return;
                    case "torque":
                        SelectedObject.SpecsTorque = value;
                        return;
                    case "weight":
                        SelectedObject.SpecsWeight = value;
                        return;
                    case "topspeed":
                        SelectedObject.SpecsTopSpeed = value;
                        return;
                    case "acceleration":
                        SelectedObject.SpecsAcceleration = value;
                        return;
                    case "pwratio":
                        SelectedObject.SpecsPwRatio = value;
                        return;
                    default:
                        Logging.Warning("Unexpected key: " + key);
                        return;
                }
            }

            private bool IsFormatCorrect(string key) {
                var format = GetFormat(key);
                var value = GetSpecsValue(key);
                return value == null || Regex.IsMatch(value, @"^" + Regex.Escape(format).Replace(@"…", @"-?\d+(?:\.\d+)?") + @"$");
            }

            private static readonly Regex FixAccelerationRegex = new Regex(@"0\s*[-–—]\s*\d\d+", RegexOptions.Compiled);

            private void FixFormat(string key) {
                var format = GetFormat(key);
                var value = GetSpecsValue(key);
                if (value == null) return;

                value = FixAccelerationRegex.Replace(value, "");

                double actualValue;
                var replacement = FlexibleParser.TryParseDouble(value, out actualValue) ? actualValue.Round(0.01).ToInvariantString() : @"--";
                value = SpecsFormat(format, replacement);
                SetSpecsValue(key, value);
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

                var dlg = new CarTransmissionLossSelector(o);
                dlg.ShowDialog();
                if (!dlg.IsResultOk) return;

                var lossMultipler = 100.0 / (100.0 - dlg.Value);

                var data = o.AcdData;
                if (data == null) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotDo_Title, "Data is damaged");
                    return;
                }

                Lut torque;
                try {
                    torque = TorquePhysicUtils.LoadCarTorque(data);
                } catch (Exception ex) {
                    NonfatalError.Notify(ToolsStrings.Common_CannotDo_Title, ex);
                    return;
                }

                torque.TransformSelf(x => x.Y * lossMultipler);
                var power = TorquePhysicUtils.TorqueToPower(torque);

                o.SpecsTorqueCurve = new GraphData(torque);
                o.SpecsPowerCurve = new GraphData(power);

                if (ModernDialog.ShowMessage(AppStrings.CarSpecs_CopyNewPowerAndTorque, AppStrings.Common_OneMoreThing, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    // MaxY values were updated while creating new GraphData instances above
                    o.SpecsTorque = SpecsFormat(AppStrings.CarSpecs_Torque_FormatTooltip, torque.MaxY.ToString(@"F0", CultureInfo.InvariantCulture));
                    o.SpecsBhp = SpecsFormat(AppStrings.CarSpecs_Power_FormatTooltip, power.MaxY.ToString(@"F0", CultureInfo.InvariantCulture));
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
            UpdateExtendedMode();

            if (SettingsHolder.CustomShowroom.LiteByDefault) {
                LiteCustomShowroomMenuItem.InputGestureText = @"Alt+H";
                FancyCustomShowroomMenuItem.InputGestureText = @"Ctrl+Alt+H";
            } else {
                LiteCustomShowroomMenuItem.InputGestureText = @"Ctrl+Alt+H";
                FancyCustomShowroomMenuItem.InputGestureText = @"Alt+H";
            }
        }

        private void SetModel() {
            _model?.Unload();
            InitializeAcObjectPage(_model = new ViewModel(_object));
            InputBindings.AddRange(new[] {
                new InputBinding(_model.UpdatePreviewsCommand, new KeyGesture(Key.P, ModifierKeys.Control)),
                new InputBinding(_model.UpdatePreviewsOptionsCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(_model.UpdatePreviewsManuallyCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Alt)),

                new InputBinding(_model.RecalculatePwRatioCommand, new KeyGesture(Key.W, ModifierKeys.Alt)),
                new InputBinding(_model.FixFormatCommand, new KeyGesture(Key.F, ModifierKeys.Alt)),

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
            });
        }

        #region Skins
        private void OnPreviewClick(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2 && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;
                CarOpenInShowroomDialog.Run(_model.SelectedObject, _model.SelectedObject.SelectedSkin?.Id);
            } else if (e.ClickCount == 1 && ReferenceEquals(sender, SelectedSkinPreviewImage) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                e.Handled = true;

                var skins = _model.SelectedObject.EnabledOnlySkins.ToList();
                new ImageViewer(
                        from skin in skins select skin.PreviewImage,
                        skins.IndexOf(_model.SelectedObject.SelectedSkin),
                        1022).ShowDialog();
            }
        }

        private void SelectedSkinPreview_MouseUp(object sender, MouseButtonEventArgs e) {
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
            item.Click += (sender, args) => new CarUpdatePreviewsDialog(_model.SelectedObject, new[] { skin.Id },
                    ViewModel.GetAutoUpdatePreviewsDialogMode()).ShowDialog();
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());

            item = new MenuItem { Header = AppStrings.Toolbar_ChangeLivery };
            item.Click += (sender, args) => new LiveryIconEditor(skin).ShowDialog();
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
        private void ToolbarButtonShowroom_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeShowroomPresets();
        }

        private void ToolbarButtonQuickDrive_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeQuickDrivePresets();
        }

        private void ToolbarButtonUpdatePreviews_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            _model.InitializeUpdatePreviewsPresets();
        }
        #endregion

        #region Icons & Specs
        private void AcObjectBase_IconMouseDown(object sender, MouseButtonEventArgs e) {
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

        private void ParentBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new ChangeCarParentDialog((CarObject)SelectedAcObject).ShowDialog();
            }
        }

        private void SpecsInfoBlock_OnMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) {
                e.Handled = true;
                new CarSpecsEditor((CarObject)SelectedAcObject).ShowDialog();
            }
        }
        #endregion

        private bool _extendedMode;

        public bool ExtendedMode {
            get { return _extendedMode; }
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

        private void UpdateExtendedMode() {
            ExtendedMode = OptionExtendedMode && ActualWidth > 1200d;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateExtendedMode();
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
    }
}
