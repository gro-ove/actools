using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Tools.Helpers.AcSettingsControls;
using AcManager.Tools.Helpers.DirectInput;
using AcManager.Tools.Lists;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX.DirectInput;
using Key = System.Windows.Input.Key;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {

        public static event EventHandler ControlsPresetLoading;

        public class ControlsSettings : IniSettings, IDisposable {
            public IAcControlsConflictResolver ConflictResolver { get; set; }

            internal ControlsSettings() : base(@"controls", false) {
                try {
                    KeyboardButtonEntries = WheelButtonEntries.Select(x => x.KeyboardButton).ToArray();
                    KeyboardSpecificButtonEntries = new[] {
                        new KeyboardSpecificButtonEntry("GAS", "Throttle"),
                        new KeyboardSpecificButtonEntry("BRAKE", "Brakes"),
                        new KeyboardSpecificButtonEntry("RIGHT", "Steer right"),
                        new KeyboardSpecificButtonEntry("LEFT", "Steer left")
                    }.Union(WheelGearsButtonEntries.Select(x => x.KeyboardButton)).ToArray();

                    _directInput = new SlimDX.DirectInput.DirectInput();
                    _keyboardInput = new Dictionary<int, KeyboardInputButton>();
                    PresetsDirectory = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "controllers");
                    UserPresetsDirectory = Path.Combine(PresetsDirectory, "savedsetups");

                    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
                } catch (Exception e) {
                    Logging.Warning("ControlsSettings exception: " + e);
                }
            }

            internal void FinishInitialization() {
                Reload();
                foreach (var entry in Entries) {
                    entry.PropertyChanged += EntryPropertyChanged;
                }

                UpdateWheelHShifterDevice();

                _timer.IsEnabled = true;
                _timer.Tick += OnTick;
            }

            #region Devices
            private SlimDX.DirectInput.DirectInput _directInput;
            private readonly Dictionary<int, KeyboardInputButton> _keyboardInput;
            private readonly DispatcherTimer _timer;

            private int _used;

            public int Used {
                get { return _used; }
                set {
                    if (Equals(_used, value)) return;

                    if (_used == 0) {
                        RescanDevices();
                    }

                    _used = value;
                    OnPropertyChanged(false);
                    UpdateInputs();
                }
            }

            public event EventHandler<DirectInputAxleEventArgs> AxleEventHandler;
            public event EventHandler<DirectInputButtonEventArgs> ButtonEventHandler;

            private Stopwatch _rescanStopwatch;

            private void UpdateInputs() {
                if (Used == 0 && AxleEventHandler == null && ButtonEventHandler == null) return;

                if (_rescanStopwatch == null) {
                    _rescanStopwatch = Stopwatch.StartNew();
                }
                
                if (_rescanStopwatch.ElapsedMilliseconds > 1000 || Devices.Any(x => x.Unplugged)) {
                    RescanDevices();
                    _rescanStopwatch.Restart();
                }

                foreach (var device in Devices) {
                    device.OnTick();
                }

                foreach (var key in _keyboardInput.Values.Where(x => x.Used > 0)) {
                    key.Value = User32.IsKeyPressed(key.Key);
                }
            }

            private void OnTick(object sender, EventArgs e) {
                UpdateInputs();
            }

            public void Dispose() {
                _timer.Stop();
                Devices.DisposeEverything();
                DisposeHelper.Dispose(ref _directInput);
            }

            private string _devicesFootprint;

            private string GetFootprint(IEnumerable<DeviceInstance> devices) {
                return devices.Select(x => x.InstanceGuid).JoinToString(";");
            }

            private readonly List<PlaceholderInputDevice> _placeholderDevices = new List<PlaceholderInputDevice>(1);

            private PlaceholderInputDevice GetPlaceholderDevice(string id, string displayName, int iniId) {
                var placeholder = _placeholderDevices.GetByIdOrDefault(id);
                if (placeholder == null) {
                    placeholder = new PlaceholderInputDevice(id, displayName, iniId);
                    _placeholderDevices.Add(placeholder);
                }

                return placeholder;
            }

            private PlaceholderInputDevice GetPlaceholderDevice(DirectInputDevice device) {
                return GetPlaceholderDevice(device.Id, device.DisplayName, device.IniId);
            }

            private void RescanDevices() {
                var devices = _directInput.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly);
                var footprint = GetFootprint(devices);
                if (footprint == _devicesFootprint) return;

                _skip = true;

                try {
                    var newDevices = devices.Select((x, i) => DirectInputDevice.Create(_directInput, x, i)).NonNull().ToList();
                    _devicesFootprint = GetFootprint(newDevices.Select(x => x.Device));

                    foreach (var entry in WheelAxleEntries) {
                        if (entry.Input == null) continue;
                        if (entry.Input.Device is PlaceholderInputDevice) {
                            var replacement = newDevices.GetByIdOrDefault(entry.Input.Device.Id);
                            if (replacement != null) {
                                entry.Input = replacement.GetAxle(entry.Input.Id);
                            }
                        } else if (entry.Input.Device is DirectInputDevice && !newDevices.Contains(entry.Input.Device)) {
                            entry.Input = GetPlaceholderDevice((DirectInputDevice)entry.Input.Device).GetAxle(entry.Input.Id);
                        }
                    }

                    foreach (var entry in WheelButtonEntries.Select(x => x.WheelButton).Union(WheelHShifterButtonEntries)) {
                        if (entry.Input == null) continue;
                        if (entry.Input.Device is PlaceholderInputDevice) {
                            var replacement = newDevices.GetByIdOrDefault(entry.Input.Device.Id);
                            if (replacement != null) {
                                entry.Input = replacement.GetButton(entry.Input.Id);
                            }
                        } else if (entry.Input.Device is DirectInputDevice && !newDevices.Contains(entry.Input.Device)) {
                            entry.Input = GetPlaceholderDevice((DirectInputDevice)entry.Input.Device).GetButton(entry.Input.Id);
                        }
                    }

                    foreach (var device in Devices) {
                        foreach (var button in device.Buttons) {
                            button.PropertyChanged -= DeviceButtonEventHandler;
                        }

                        foreach (var axle in device.Axles) {
                            axle.PropertyChanged -= DeviceAxleEventHandler;
                        }

                        device.Dispose();
                    }

                    Devices.ReplaceEverythingBy(newDevices);

                    foreach (var device in Devices) {
                        foreach (var button in device.Buttons) {
                            button.PropertyChanged += DeviceButtonEventHandler;
                        }

                        foreach (var axle in device.Axles) {
                            axle.PropertyChanged += DeviceAxleEventHandler;
                        }
                    }
                } finally {
                    _skip = false;
                    UpdateWheelHShifterDevice();
                }
            }

            private void AssignInput<T>(T provider) where T : class, IInputProvider {
                var waiting = GetWaiting() as BaseEntry<T>;
                if (waiting == null) return;

                if (waiting.Input == provider) {
                    waiting.Waiting = false;
                    return;
                }

                var existing = Entries.OfType<BaseEntry<T>>().Where(x => x.Input == provider).ToList();
                if (existing.Any()) {
                    var solution = ConflictResolver?.Resolve(provider.DisplayName, existing.Select(x => x.DisplayName));
                    switch (solution) {
                        case AcControlsConflictSolution.Cancel:
                            return;

                        case AcControlsConflictSolution.Flip:
                            foreach (var entry in existing) {
                                entry.Input = waiting.Input;
                            }
                            break;

                        case AcControlsConflictSolution.ClearPrevious:
                            foreach (var entry in existing) {
                                entry.Clear();
                            }
                            break;
                    }
                }

                waiting.Input = provider;
            }

            private void DeviceAxleEventHandler(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName != nameof(DirectInputAxle.RoundedValue)) return;

                var axle = sender as DirectInputAxle;
                if (axle == null) return;
                AxleEventHandler?.Invoke(this, new DirectInputAxleEventArgs(axle, axle.Delta));

                AssignInput(axle);
            }

            private void DeviceButtonEventHandler(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName != nameof(DirectInputButton.Value)) return;

                var button = sender as DirectInputButton;
                if (button == null) return;
                ButtonEventHandler?.Invoke(this, new DirectInputButtonEventArgs(button));

                AssignInput(button);
            }

            public BetterObservableCollection<DirectInputDevice> Devices { get; } = new BetterObservableCollection<DirectInputDevice>();

            [CanBeNull]
            public KeyboardInputButton GetKeyboardInputButton(int keyCode) {
                if (keyCode == -1) return null;

                KeyboardInputButton result;
                if (_keyboardInput.TryGetValue(keyCode, out result)) return result;
                if (!Enum.IsDefined(typeof(Keys), keyCode)) {
                    Logging.Warning("Invalid key: " + keyCode);
                    return null;
                }

                result = new KeyboardInputButton(keyCode);
                _keyboardInput[keyCode] = result;
                return result;
            }
            #endregion

            #region Presets
            public readonly string PresetsDirectory;
            public readonly string UserPresetsDirectory;

            protected override void OnRenamed(object sender, RenamedEventArgs e) {
                if (FileUtils.IsAffected(PresetsDirectory, e.OldFullPath) || FileUtils.IsAffected(PresetsDirectory, e.FullPath) ||
                        FileUtils.IsAffected(e.OldFullPath, PresetsDirectory) || FileUtils.IsAffected(e.FullPath, PresetsDirectory)) {
                    PresetsUpdated?.Invoke(this, EventArgs.Empty);
                }

                base.OnRenamed(sender, e);
            }

            protected override void OnChanged(object sender, FileSystemEventArgs e) {
                if (FileUtils.IsAffected(PresetsDirectory, e.FullPath) || FileUtils.IsAffected(e.FullPath, PresetsDirectory)) {
                    PresetsUpdated?.Invoke(this, EventArgs.Empty);
                }

                base.OnChanged(sender, e);
            }

            public event EventHandler PresetsUpdated;

            private string _currentPresetName;

            public string CurrentPresetName {
                get { return _currentPresetName; }
                set {
                    if (Equals(value, _currentPresetName)) return;
                    _currentPresetName = value;
                    OnPropertyChanged(false);
                }
            }

            private string GetCurrentPresetName(string filename) {
                filename = FileUtils.GetRelativePath(filename, PresetsDirectory);
                var f = new[] { "presets", "savedsetups" }.FirstOrDefault(s => filename.StartsWith(s, StringComparison.OrdinalIgnoreCase));
                return (f == null ? filename : filename.SubstringExt(f.Length + 1)).ApartFromLast(".ini", StringComparison.OrdinalIgnoreCase);
            }

            private bool _currentPresetChanged = true;

            public bool CurrentPresetChanged {
                get { return _currentPresetChanged; }
                set {
                    if (Equals(value, _currentPresetChanged)) return;
                    _currentPresetChanged = value;
                    OnPropertyChanged(false);
                }
            }

            protected override void Save() {
                if (IsLoading) return;

                if (CurrentPresetName != null) {
                    CurrentPresetChanged = true;
                }

                base.Save();
            }

            public void LoadPreset(string presetFilename) {
                ControlsPresetLoading?.Invoke(this, EventArgs.Empty);
                new IniFile(presetFilename) {
                    ["__LAUNCHER_CM"] = {
                        ["PRESET_NAME"] = presetFilename.SubstringExt(PresetsDirectory.Length + 1),
                        ["PRESET_CHANGED"] = false
                    }
                }.SaveAs(Filename, true);
            }
            #endregion

            #region Entries lists
            public SettingEntry[] InputMethods { get; } = {
                new SettingEntry("WHEEL", "Wheel"),
                new SettingEntry("X360", "Xbox 360 gamepad"),
                new SettingEntry("KEYBOARD", "Keyboard & mouse")
            };
            #endregion

            #region Common
            private SettingEntry _inputMethod;

            public SettingEntry InputMethod {
                get { return _inputMethod; }
                set {
                    if (!InputMethods.Contains(value)) value = InputMethods[0];
                    if (Equals(value, _inputMethod)) return;
                    _inputMethod = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Wheel
            private int _debouncingInterval;

            public int DebouncingInterval {
                get { return _debouncingInterval; }
                set {
                    value = value.Clamp(0, 500);
                    if (Equals(value, _debouncingInterval)) return;
                    _debouncingInterval = value;
                    OnPropertyChanged();
                }
            }

            public WheelAxleEntry[] WheelAxleEntries { get; } = {
                new WheelAxleEntry("STEER", "Steering", false, true),
                new WheelAxleEntry("THROTTLE", "Throttle"),
                new WheelAxleEntry("BRAKES", "Brakes", gammaMode: true),
                new WheelAxleEntry("CLUTCH", "Clutch"),
                new WheelAxleEntry("HANDBRAKE", "Handbrake")
            };

            private bool _combineWithKeyboardInput;

            public bool CombineWithKeyboardInput {
                get { return _combineWithKeyboardInput; }
                set {
                    if (Equals(value, _combineWithKeyboardInput)) return;
                    _combineWithKeyboardInput = value;
                    OnPropertyChanged();
                }
            }

            public class WheelButtonCombined : NotifyPropertyChanged {
                public WheelButtonCombined(string id, string displayName, bool isNew = false) {
                    IsNew = isNew;
                    WheelButton = new WheelButtonEntry(id, displayName);
                    KeyboardButton = new KeyboardButtonEntry(id, displayName);
                }

                public WheelButtonEntry WheelButton { get; }

                public KeyboardButtonEntry KeyboardButton { get; }

                public bool IsNew { get; }
            }

            public WheelButtonCombined[] WheelGearsButtonEntries { get; } = {
                new WheelButtonCombined("GEARUP", "Next gear"),
                new WheelButtonCombined("GEARDN", "Previous gear")
            };

            private bool _wheelUseHShifter;

            public bool WheelUseHShifter {
                get { return _wheelUseHShifter; }
                set {
                    if (Equals(value, _wheelUseHShifter)) return;
                    _wheelUseHShifter = value;
                    OnPropertyChanged();
                }
            }

            private IDirectInputDevice _wheelHShifterDevice;

            public IDirectInputDevice WheelHShifterDevice {
                get { return _wheelHShifterDevice; }
                set {
                    if (Equals(value, _wheelHShifterDevice)) return;
                    _wheelHShifterDevice = value;
                    OnPropertyChanged(false);
                }
            }

            private void UpdateWheelHShifterDevice() {
                WheelHShifterDevice = WheelHShifterButtonEntries.Select(x => x.Input?.Device).FirstOrDefault(x => x != null);
                foreach (var entry in WheelHShifterButtonEntries.Where(x => x.Input != null && x.Input.Device != WheelHShifterDevice)) {
                    entry.Clear();
                }
            }

            public WheelHShifterButtonEntry[] WheelHShifterButtonEntries { get; } = {
                new WheelHShifterButtonEntry("GEAR_1", "First gear", "1"),
                new WheelHShifterButtonEntry("GEAR_2", "Second gear", "2"),
                new WheelHShifterButtonEntry("GEAR_3", "Third gear", "3"),
                new WheelHShifterButtonEntry("GEAR_4", "Fourth gear", "4"),
                new WheelHShifterButtonEntry("GEAR_5", "Fifth gear", "5"),
                new WheelHShifterButtonEntry("GEAR_6", "Sixth gear", "6"),
                new WheelHShifterButtonEntry("GEAR_7", "Seventh gear", "7"),
                new WheelHShifterButtonEntry("GEAR_R", "Rear gear", "R")
            };

            public WheelButtonCombined[] WheelCarButtonEntries { get; } = {
                new WheelButtonCombined("KERS", "KERS activation"),
                new WheelButtonCombined("DRS", "DRS activation"),
                new WheelButtonCombined("HANDBRAKE", "Handbrake"),
                new WheelButtonCombined("ACTION_HEADLIGHTS", "Headlights"),
                new WheelButtonCombined("ACTION_HEADLIGHTS_FLASH", "Flash Headlights"),
                new WheelButtonCombined("ACTION_HORN", "Horn"),
            };

            public WheelButtonCombined[] WheelCarBrakeButtonEntries { get; } = {
                new WheelButtonCombined("BALANCEUP", "Move to front"),
                new WheelButtonCombined("BALANCEDN", "Move to rear"),
            };

            public WheelButtonCombined[] WheelCarTurboButtonEntries { get; } = {
                new WheelButtonCombined("TURBOUP", "Increase"),
                new WheelButtonCombined("TURBODN", "Decrease"),
            };

            public WheelButtonCombined[] WheelCarTractionControlButtonEntries { get; } = {
                new WheelButtonCombined("TCUP", "Increase"),
                new WheelButtonCombined("TCDN", "Decrease"),
            };

            public WheelButtonCombined[] WheelCarAbsButtonEntries { get; } = {
                new WheelButtonCombined("ABSUP", "Increase"),
                new WheelButtonCombined("ABSDN", "Decrease"),
            };

            public WheelButtonCombined[] WheelCarEngineBrakeButtonEntries { get; } = {
                new WheelButtonCombined("ENGINE_BRAKE_UP", "Increase"),
                new WheelButtonCombined("ENGINE_BRAKE_DN", "Decrease"),
            };

            public WheelButtonCombined[] WheelCarMgukButtonEntries { get; } = {
                new WheelButtonCombined("MGUK_DELIVERY_UP", "Increase Delivery"),
                new WheelButtonCombined("MGUK_DELIVERY_DN", "Decrease Delivery"),
                new WheelButtonCombined("MGUK_RECOVERY_UP", "Increase Recovery"),
                new WheelButtonCombined("MGUK_RECOVERY_DN", "Decrease Recovery"),
                new WheelButtonCombined("MGUH_MODE", "MGU-H Mode")
            };

            public WheelButtonCombined[] WheelViewButtonEntries { get; } = {
                new WheelButtonCombined("GLANCELEFT", "Glance left"),
                new WheelButtonCombined("GLANCERIGHT", "Glance right"),
                new WheelButtonCombined("GLANCEBACK", "Glance back"),
                new WheelButtonCombined("ACTION_CHANGE_CAMERA", "Change camera")
            };

            public WheelButtonCombined[] WheelGesturesButtonEntries { get; } = {
                new WheelButtonCombined("ACTION_CELEBRATE", "Celebrate"),
                new WheelButtonCombined("ACTION_CLAIM", "Complain")
            };

            private WheelButtonCombined[] _wheelButtonEntries;

            private IEnumerable<WheelButtonCombined> WheelButtonEntries => _wheelButtonEntries ?? (_wheelButtonEntries = WheelGearsButtonEntries
                    .Union(WheelCarButtonEntries)
                    .Union(WheelCarBrakeButtonEntries)
                    .Union(WheelCarTurboButtonEntries)
                    .Union(WheelCarTractionControlButtonEntries)
                    .Union(WheelCarAbsButtonEntries)
                    .Union(WheelCarEngineBrakeButtonEntries)
                    .Union(WheelCarMgukButtonEntries)
                    .Union(WheelViewButtonEntries)
                    .Union(WheelGesturesButtonEntries)
                    .ToArray());
            #endregion

            #region Wheel FFB
            private bool _wheelFfbInvert;

            public bool WheelFfbInvert {
                get { return _wheelFfbInvert; }
                set {
                    if (Equals(value, _wheelFfbInvert)) return;
                    _wheelFfbInvert = value;
                    OnPropertyChanged();
                }
            }

            private int _wheelFfbGain;

            public int WheelFfbGain {
                get { return _wheelFfbGain; }
                set {
                    value = value.Clamp(0, 300);
                    if (Equals(value, _wheelFfbGain)) return;
                    _wheelFfbGain = value;
                    OnPropertyChanged();
                }
            }

            private int _wheelFfbFilter;

            public int WheelFfbFilter {
                get { return _wheelFfbFilter; }
                set {
                    value = value.Clamp(0, 99);
                    if (Equals(value, _wheelFfbFilter)) return;
                    _wheelFfbFilter = value;
                    OnPropertyChanged();
                }
            }

            private double _wheelFfbMinForce;

            public double WheelFfbMinForce {
                get { return _wheelFfbMinForce; }
                set {
                    value = value.Clamp(0, 50);
                    if (Equals(value, _wheelFfbMinForce)) return;
                    _wheelFfbMinForce = value;
                    OnPropertyChanged();
                }
            }

            private int _wheelFfbKerbEffect;

            public int WheelFfbKerbEffect {
                get { return _wheelFfbKerbEffect; }
                set {
                    value = value.Clamp(0, 200);
                    if (Equals(value, _wheelFfbKerbEffect)) return;
                    _wheelFfbKerbEffect = value;
                    OnPropertyChanged();
                }
            }

            private int _wheelFfbRoadEffect;

            public int WheelFfbRoadEffect {
                get { return _wheelFfbRoadEffect; }
                set {
                    value = value.Clamp(0, 200);
                    if (Equals(value, _wheelFfbRoadEffect)) return;
                    _wheelFfbRoadEffect = value;
                    OnPropertyChanged();
                }
            }

            private int _wheelFfbSlipEffect;

            public int WheelFfbSlipEffect {
                get { return _wheelFfbSlipEffect; }
                set {
                    value = value.Clamp(0, 200);
                    if (Equals(value, _wheelFfbSlipEffect)) return;
                    _wheelFfbSlipEffect = value;
                    OnPropertyChanged();
                }
            }

            private bool _wheelFfbEnhancedUndersteer;

            public bool WheelFfbEnhancedUndersteer {
                get { return _wheelFfbEnhancedUndersteer; }
                set {
                    if (Equals(value, _wheelFfbEnhancedUndersteer)) return;
                    _wheelFfbEnhancedUndersteer = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Keyboard
            private double _keyboardSteeringSpeed;

            public double KeyboardSteeringSpeed {
                get { return _keyboardSteeringSpeed; }
                set {
                    value = value.Clamp(0.4, 3.0);
                    if (Equals(value, _keyboardSteeringSpeed)) return;
                    _keyboardSteeringSpeed = value;
                    OnPropertyChanged();
                }
            }

            private double _keyboardOppositeLockSpeed;

            public double KeyboardOppositeLockSpeed {
                get { return _keyboardOppositeLockSpeed; }
                set {
                    value = value.Clamp(1.0, 5.0);
                    if (Equals(value, _keyboardOppositeLockSpeed)) return;
                    _keyboardOppositeLockSpeed = value;
                    OnPropertyChanged();
                }
            }

            private double _keyboardReturnRate;

            public double KeyboardReturnRate {
                get { return _keyboardReturnRate; }
                set {
                    value = value.Clamp(1.0, 5.0);
                    if (Equals(value, _keyboardReturnRate)) return;
                    _keyboardReturnRate = value;
                    OnPropertyChanged();
                }
            }

            private bool _keyboardMouseSteering;

            public bool KeyboardMouseSteering {
                get { return _keyboardMouseSteering; }
                set {
                    if (Equals(value, _keyboardMouseSteering)) return;
                    _keyboardMouseSteering = value;
                    OnPropertyChanged();
                }
            }

            private bool _keyboardMouseButtons;

            public bool KeyboardMouseButtons {
                get { return _keyboardMouseButtons; }
                set {
                    if (Equals(value, _keyboardMouseButtons)) return;
                    _keyboardMouseButtons = value;
                    OnPropertyChanged();
                }
            }

            private double _keyboardMouseSteeringSpeed;

            public double KeyboardMouseSteeringSpeed {
                get { return _keyboardMouseSteeringSpeed; }
                set {
                    value = value.Clamp(0.1, 1.0);
                    if (Equals(value, _keyboardMouseSteeringSpeed)) return;
                    _keyboardMouseSteeringSpeed = value;
                    OnPropertyChanged();
                }
            }

            public KeyboardButtonEntry[] KeyboardButtonEntries { get; }

            public KeyboardButtonEntry[] KeyboardSpecificButtonEntries { get; }
            #endregion

            #region Main
            private IEnumerable<IEntry> Entries
                => WheelAxleEntries
                        .Select(x => (IEntry)x)
                        .Union(WheelButtonEntries.Select(x => x.KeyboardButton))
                        .Union(WheelButtonEntries.Select(x => x.WheelButton))
                        .Union(WheelHShifterButtonEntries)
                        .Union(KeyboardSpecificButtonEntries);

            private bool _skip;

            private void EntryPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (_skip) return;
                if (e.PropertyName == nameof(WheelHShifterButtonEntry.Input) && sender is WheelHShifterButtonEntry) {
                    UpdateWheelHShifterDevice();
                } else if (e.PropertyName != nameof(WheelAxleEntry.Value)) {
                    Save();
                }
            }

            private RelayCommand _toggleWaitingCommand;

            public RelayCommand ToggleWaitingCommand => _toggleWaitingCommand ?? (_toggleWaitingCommand = new RelayCommand(o => {
                var b = o as IEntry;
                if (b == null) return;

                foreach (var entry in Entries.ApartFrom(b)) {
                    entry.Waiting = false;
                }

                b.Waiting = !b.Waiting;
            }, o => o is IEntry));

            [CanBeNull]
            public IEntry GetWaiting() {
                return Entries.FirstOrDefault(x => x.Waiting);
            }

            public bool StopWaiting() {
                var found = false;
                foreach (var entry in Entries.Where(x => x.Waiting)) {
                    entry.Waiting = false;
                    found = true;
                }

                return found;
            }

            public bool ClearWaiting() {
                var found = false;
                foreach (var entry in Entries.Where(x => x.Waiting)) {
                    entry.Clear();
                    found = true;
                }

                return found;
            }

            public bool AssignKey(Key key) {
                if (!key.IsInputAssignable()) return false;

                var waiting = GetWaiting() as KeyboardButtonEntry;
                if (waiting == null) return false;

                AssignInput(GetKeyboardInputButton(KeyInterop.VirtualKeyFromKey(key)));
                return true;
            }
            #endregion

            #region Loading/Saving
            public void LoadFfbFromIni(IniFile ini) {
                var section = ini["STEER"];
                var ffbGain = section.GetDouble("FF_GAIN", 1d);
                WheelFfbInvert = ffbGain < 0;
                WheelFfbGain = ffbGain.Abs().ToIntPercentage();
                WheelFfbFilter = section.GetDouble("FILTER_FF", 0d).ToIntPercentage();

                section = ini["FF_TWEAKS"];
                WheelFfbMinForce = section.GetDouble("MIN_FF", 0.05) * 100d;

                section = ini["FF_ENHANCEMENT"];
                WheelFfbKerbEffect = section.GetDouble("CURBS", 0.4).ToIntPercentage();
                WheelFfbRoadEffect = section.GetDouble("ROAD", 0.5).ToIntPercentage();
                WheelFfbSlipEffect = section.GetDouble("SLIPS", 0.0).ToIntPercentage();

                section = ini["FF_ENHANCEMENT_2"];
                WheelFfbEnhancedUndersteer = section.GetBool("UNDERSTEER", false);
            }

            public void SaveFfbToIni(IniFile ini) {
                var section = ini["STEER"];
                section.Set("FF_GAIN", (WheelFfbInvert ? -1d : 1d) * WheelFfbGain.ToDoublePercentage());
                section.Set("FILTER_FF", WheelFfbFilter.ToDoublePercentage());

                section = ini["FF_TWEAKS"];
                section.Set("MIN_FF", WheelFfbMinForce / 100d);

                section = ini["FF_ENHANCEMENT"];
                section.Set("CURBS", WheelFfbKerbEffect.ToDoublePercentage());
                section.Set("ROAD", WheelFfbRoadEffect.ToDoublePercentage());
                section.Set("SLIPS", WheelFfbSlipEffect.ToDoublePercentage());

                section = ini["FF_ENHANCEMENT_2"];
                section.Set("UNDERSTEER", WheelFfbEnhancedUndersteer);
            }

            protected override void LoadFromIni() {
                if (Devices.Count == 0) {
                    // RescanDevices();
                }

                InputMethod = Ini["HEADER"].GetEntry("INPUT_METHOD", InputMethods);
                CombineWithKeyboardInput = Ini["ADVANCED"].GetBool("COMBINE_WITH_KEYBOARD_CONTROL", false);
                WheelUseHShifter = Ini["SHIFTER"].GetBool("ACTIVE", false);
                DebouncingInterval = Ini["STEER"].GetInt("DEBOUNCING_MS", 50);

                var section = Ini["CONTROLLERS"];
                var devices = LinqExtension.RangeFrom().Select(x => new {
                    Id = section.Get($"PGUID{x}"),
                    Name = section.Get($"CON{x}")
                }).TakeWhile(x => x.Id != null).Select((x, i) => {
                    var device = Devices.GetByIdOrDefault(x.Id);
                    if (device == null) {
                        return (IDirectInputDevice)GetPlaceholderDevice(x.Id, x.Name, i);
                    }

                    device.IniId = i;
                    return device;
                }).ToList();

                foreach (var entry in Entries) {
                    entry.Load(Ini, devices);
                }

                LoadFfbFromIni(Ini);

                section = Ini["KEYBOARD"];
                KeyboardSteeringSpeed = section.GetDouble("STEERING_SPEED", 1.75);
                KeyboardOppositeLockSpeed = section.GetDouble("STEERING_OPPOSITE_DIRECTION_SPEED", 2.5);
                KeyboardReturnRate = section.GetDouble("STEER_RESET_SPEED", 1.8);
                KeyboardMouseSteering = section.GetBool("MOUSE_STEER", false);
                KeyboardMouseButtons = section.GetBool("MOUSE_ACCELERATOR_BRAKE", false);
                KeyboardMouseSteeringSpeed = section.GetDouble("MOUSE_SPEED", 0.1);

                section = Ini["__LAUNCHER_CM"];
                CurrentPresetName = section.ContainsKey("PRESET_NAME") ? GetCurrentPresetName(section.Get("PRESET_NAME")) : null;
                CurrentPresetChanged = CurrentPresetName != null && section.GetBool("PRESET_CHANGED", true);
            }

            public void SaveControllers() {
                foreach (var device in Devices) {
                    var filename = Path.Combine(PresetsDirectory, device.Device.InstanceGuid + ".ini");

                    var ini = new IniFile {
                        ["CONTROLLER"] = {
                            ["NAME"] = device.DisplayName,
                            ["PRODUCT"] = device.Device.ProductGuid.ToString()
                        }
                    };

                    foreach (var entry in WheelAxleEntries.Where(x => x.Input != null)) {
                        ini[entry.Id].Set("AXLE", entry.Input?.Id);
                    }

                    foreach (var entry in WheelButtonEntries.Select(x => x.WheelButton).Where(x => x.Input != null)) {
                        ini[entry.Id].Set("BUTTON", entry.Input?.Id);
                    }

                    foreach (var entry in WheelHShifterButtonEntries.Where(x => x.Input != null)) {
                        ini["SHIFTER"].Set(entry.Id, entry.Input?.Id);
                    }

                    ini.SaveAs(filename);
                }
            }

            protected override void SetToIni() {
#if DEBUG
                var sw = Stopwatch.StartNew();
#endif

                Ini["HEADER"].Set("INPUT_METHOD", InputMethod);
                Ini["ADVANCED"].Set("COMBINE_WITH_KEYBOARD_CONTROL", CombineWithKeyboardInput);
                Ini["SHIFTER"].Set("ACTIVE", WheelUseHShifter);
                Ini["SHIFTER"].Set("JOY", WheelHShifterButtonEntries.FirstOrDefault(x => x.Input != null)?.Input?.Device.IniId);
                Ini["STEER"].Set("DEBOUNCING_MS", DebouncingInterval);

                Ini["CONTROLLERS"] = Devices
                        .Select(x => (IDirectInputDevice)x)
                        .Union(_placeholderDevices)
                        .Where(x => Entries.OfType<IDirectInputEntry>().Any(y => y.Device == x))
                        .Aggregate(new IniFileSection(), (s, d, i) => {
                            s.Set("CON" + i, d.DisplayName);
                            s.Set("PGUID" + i, d.Id);
                            d.IniId = i;
                            return s;
                        });

                foreach (var entry in Entries) {
                    entry.Save(Ini);
                }

                SaveFfbToIni(Ini);

                var section = Ini["KEYBOARD"];
                section.Set("STEERING_SPEED", KeyboardSteeringSpeed);
                section.Set("STEERING_OPPOSITE_DIRECTION_SPEED", KeyboardOppositeLockSpeed);
                section.Set("STEER_RESET_SPEED", KeyboardReturnRate);
                section.Set("MOUSE_STEER", KeyboardMouseSteering);
                section.Set("MOUSE_ACCELERATOR_BRAKE", KeyboardMouseButtons);
                section.Set("MOUSE_SPEED", KeyboardMouseSteeringSpeed);

                section = Ini["__LAUNCHER_CM"];
                section.Set("PRESET_NAME", CurrentPresetName);
                section.Set("PRESET_CHANGED", CurrentPresetChanged);

                SaveControllers();

#if DEBUG
                Logging.Write($"Controls saving time: {sw.Elapsed.TotalMilliseconds:F2} ms");
#endif
            }

            public void SavePreset(string filename) {
                SetToIni();

                var section = Ini["__LAUNCHER_CM"];
                section.Set("PRESET_NAME", filename.SubstringExt(PresetsDirectory.Length + 1));
                section.Set("PRESET_CHANGED", false);

                Ini.SaveAs(filename);

                CurrentPresetName = GetCurrentPresetName(section.Get("PRESET_NAME"));
                CurrentPresetChanged = false;
            }
            #endregion
        }

        private static ControlsSettings _controls;

        public static ControlsSettings Controls {
            get {
                if (_controls != null) return _controls;
                _controls = new ControlsSettings();
                _controls.FinishInitialization();
                return _controls;
            }
        }
    }
}
