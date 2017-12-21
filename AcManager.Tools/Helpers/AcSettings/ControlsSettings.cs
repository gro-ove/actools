using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Tools.Helpers.AcSettingsControls;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX.DirectInput;
using StringBasedFilter;
using Application = System.Windows.Application;
using Key = System.Windows.Input.Key;

namespace AcManager.Tools.Helpers.AcSettings {
    public class ControlsSettings : IniSettings, IDisposable {
        public static TimeSpan OptionUpdatePeriod = TimeSpan.FromMilliseconds(16.67);
        public static TimeSpan OptionRescanPeriod = TimeSpan.FromSeconds(1d);
        public static IFilter<string> OptionIgnoreControlsFilter;

        public const string SubBuiltInPresets = "presets";
        public const string SubUserPresets = "savedsetups";
        public const string PresetExtension = ".ini";

        public IAcControlsConflictResolver ConflictResolver { get; set; }

        static ControlsSettings() {
            PresetsDirectory = Path.Combine(AcPaths.GetDocumentsCfgDirectory(), "controllers");
            UserPresetsDirectory = Path.Combine(PresetsDirectory, SubUserPresets);
        }

        internal ControlsSettings() : base("controls", false) {
            try {
                KeyboardSpecificButtonEntries = new[] {
                    new KeyboardSpecificButtonEntry("GAS", ToolsStrings.Controls_Throttle),
                    new KeyboardSpecificButtonEntry("BRAKE", ToolsStrings.Controls_Brakes),
                    new KeyboardSpecificButtonEntry("RIGHT", ToolsStrings.Controls_SteerRight),
                    new KeyboardSpecificButtonEntry("LEFT", ToolsStrings.Controls_SteerLeft)
                }.Union(WheelGearsButtonEntries.Select(x => x.KeyboardButton)).ToArray();

                _keyboardInput = new Dictionary<int, KeyboardInputButton>();
                _timer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) {
                    Interval = OptionUpdatePeriod
                };
            } catch (Exception e) {
                Logging.Warning("ControlsSettings exception: " + e);
            }
        }

        internal void FinishInitialization() {
            Reload();
            foreach (var entry in Entries) {
                entry.PropertyChanged += EntryPropertyChanged;
            }

            // TODO: double "already used as" message

            UpdateWheelHShifterDevice();

            _timer.Tick += OnTick;
            _timer.IsEnabled = true;
        }

        #region Devices
        [CanBeNull]
        private SlimDX.DirectInput.DirectInput _directInput;
        private readonly Dictionary<int, KeyboardInputButton> _keyboardInput;
        private readonly DispatcherTimer _timer;

        private int _used;

        public int Used {
            get => _used;
            set {
                if (Equals(_used, value)) return;

                Logging.Debug(_used);
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

            if (_rescanStopwatch.Elapsed > OptionRescanPeriod || Devices.Any(x => x.Unplugged)) {
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
            return devices.Select(x => x.InstanceGuid).JoinToString(@";");
        }

        private readonly List<PlaceholderInputDevice> _placeholderDevices = new List<PlaceholderInputDevice>(1);

        private PlaceholderInputDevice GetPlaceholderDevice([CanBeNull] string id, string displayName, int iniId) {
            var placeholder = _placeholderDevices.FirstOrDefault(y => y.DisplayName == displayName) ?? _placeholderDevices.GetByIdOrDefault(id);
            if (placeholder == null) {
                placeholder = new PlaceholderInputDevice(id, displayName, iniId);
                _placeholderDevices.Add(placeholder);
            } else {
                placeholder.OriginalIniIds.Add(iniId);
            }

            return placeholder;
        }

        private PlaceholderInputDevice GetPlaceholderDevice(DirectInputDevice device) {
            return GetPlaceholderDevice(device.Id, device.DisplayName, device.Index);
        }

        private void UpdatePlaceholders() {
            foreach (var source in _placeholderDevices.Where(x => Entries.OfType<IDirectInputEntry>().All(y => y.Device != x)).ToList()) {
                _placeholderDevices.Remove(source);
            }

            var next = Devices.Count;
            foreach (var placeholder in _placeholderDevices) {
                if (Devices.Any(x => x.Index == placeholder.Index)) {
                    placeholder.Index = next++;
                }
            }
        }

        private void RescanDevices() {
            IList<DeviceInstance> devices;

            try {
                if (_directInput == null) {
                    _directInput = new SlimDX.DirectInput.DirectInput();
                }

                devices = _directInput.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly);
            } catch (Exception e) {
                Logging.Error(e);
                devices = new List<DeviceInstance>();
                DisposeHelper.Dispose(ref _directInput);
            }

            if (OptionIgnoreControlsFilter != null) {
                devices = devices.Where(x => OptionIgnoreControlsFilter.Test(x.ProductName)).ToList();
            }

            var footprint = GetFootprint(devices);
            if (footprint == _devicesFootprint) return;

            _skip = true;

            try {
                var newDevices = devices.Select((x, i) => Devices.FirstOrDefault(y => y.Same(x)) ??
                        DirectInputDevice.Create(_directInput, x, i)).NonNull().ToList();
                _devicesFootprint = GetFootprint(newDevices.Select(x => x.Device));

                foreach (var entry in Entries.OfType<BaseEntry<DirectInputAxle>>()) {
                    var current = entry.Input?.Device;
                    if (current is PlaceholderInputDevice) {
                        var replacement = newDevices.FirstOrDefault(x => x.Same(current));
                        if (replacement != null) {
                            entry.Input = replacement.GetAxle(entry.Input.Id);
                        }
                    } else if (current is DirectInputDevice && !newDevices.Contains(current)) {
                        entry.Input = GetPlaceholderDevice((DirectInputDevice)current).GetAxle(entry.Input.Id);
                    }
                }

                foreach (var entry in Entries.OfType<BaseEntry<DirectInputButton>>()) {
                    var current = entry.Input?.Device;
                    if (current is PlaceholderInputDevice) {
                        var replacement = newDevices.FirstOrDefault(x => x.Same(current));
                        if (replacement != null) {
                            entry.Input = replacement.GetButton(entry.Input.Id);
                        }
                    } else if (current is DirectInputDevice && !newDevices.Contains(current)) {
                        entry.Input = GetPlaceholderDevice((DirectInputDevice)current).GetButton(entry.Input.Id);
                    }
                }

                foreach (var device in Devices.ApartFrom(newDevices)) {
                    for (var i = 0; i < device.Buttons.Length; i++) {
                        device.Buttons[i].PropertyChanged -= DeviceButtonEventHandler;
                    }

                    for (var i = 0; i < device.Axis.Length; i++) {
                        device.Axis[i].PropertyChanged -= DeviceAxleEventHandler;
                    }

                    device.Dispose();
                }

                Devices.ReplaceEverythingBy(newDevices);
                UpdatePlaceholders();

                foreach (var device in newDevices) {
                    ProductGuids[device.DisplayName] = device.Id;

                    for (var i = 0; i < device.Buttons.Length; i++) {
                        device.Buttons[i].PropertyChanged += DeviceButtonEventHandler;
                    }

                    for (var i = 0; i < device.Axis.Length; i++) {
                        device.Axis[i].PropertyChanged += DeviceAxleEventHandler;
                    }
                }
            } finally {
                _skip = false;
                UpdateWheelHShifterDevice();
            }
        }

        private static bool _busy;

        private async Task AssignInput<T>(T provider) where T : class, IInputProvider {
            if (_busy) return;
            _busy = true;

            try {
                var waiting = GetWaiting() as BaseEntry<T>;
                if (waiting == null) return;

                if (waiting.Input == provider) {
                    waiting.Waiting = false;
                    return;
                }

                var existing = Entries.OfType<BaseEntry<T>>().Where(x => x.Input == provider).ToList();
                if (existing.Any()) {
                    var solution = ConflictResolver == null ? AcControlsConflictSolution.ClearPrevious :
                            await ConflictResolver.Resolve(provider.DisplayName, existing.Select(x => x.DisplayName));
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
            } finally {
                _busy = false;
            }
        }

        private void DeviceAxleEventHandler(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(DirectInputAxle.RoundedValue) && sender is DirectInputAxle axle) {
                AxleEventHandler?.Invoke(this, new DirectInputAxleEventArgs(axle, axle.Delta));
                AssignInput(axle).Forget();
            }
        }

        private void DeviceButtonEventHandler(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(DirectInputButton.Value) && sender is DirectInputButton button && button.Value) {
                ButtonEventHandler?.Invoke(this, new DirectInputButtonEventArgs(button));
                AssignInput(button).Forget();
            }
        }

        public BetterObservableCollection<DirectInputDevice> Devices { get; } = new BetterObservableCollection<DirectInputDevice>();

        [CanBeNull]
        public KeyboardInputButton GetKeyboardInputButton(int keyCode) {
            if (keyCode == -1) return null;

            if (_keyboardInput.TryGetValue(keyCode, out var result)) return result;
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
        public static readonly string PresetsDirectory;
        public static readonly string UserPresetsDirectory;

        protected override void OnFileChanged(string filename) {
            if (FileUtils.Affects(PresetsDirectory, filename) || FileUtils.Affects(filename, PresetsDirectory)) {
                PresetsUpdated?.Invoke(this, EventArgs.Empty);
            }

            base.OnFileChanged(filename);
        }

        public event EventHandler PresetsUpdated;

        public static string GetCurrentPresetName(string filename) {
            filename = FileUtils.GetRelativePath(filename, PresetsDirectory);
            var f = new[] { SubBuiltInPresets, SubUserPresets }.FirstOrDefault(s => filename.StartsWith(s, StringComparison.OrdinalIgnoreCase));
            return (f == null ? filename : filename.SubstringExt(f.Length + 1)).ApartFromLast(PresetExtension, StringComparison.OrdinalIgnoreCase);
        }

        [CanBeNull]
        public string CurrentPresetName { get; private set; }

        private string _currentPresetFilename;

        [CanBeNull]
        public string CurrentPresetFilename {
            get => _currentPresetFilename;
            set {
                if (Equals(value, _currentPresetFilename)) return;
                _currentPresetFilename = value;
                OnPropertyChanged();

                CurrentPresetName = value == null ? null : GetCurrentPresetName(value);
                OnPropertyChanged(nameof(CurrentPresetName));
            }
        }

        private bool _currentPresetChanged = true;

        public bool CurrentPresetChanged {
            get => _currentPresetChanged;
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

        public void LoadPreset(string presetFilename, bool backup) {
            Replace(new IniFile(presetFilename) {
                ["__LAUNCHER_CM"] = {
                    ["PRESET_NAME"] = presetFilename.SubstringExt(PresetsDirectory.Length + 1),
                    ["PRESET_CHANGED"] = false
                }
            }, backup);
        }
        #endregion

        #region Entries lists
        public SettingEntry[] InputMethods { get; } = {
            new SettingEntry("WHEEL", ToolsStrings.Controls_SteeringWheel),
            new SettingEntry("X360", ToolsStrings.Controls_Xbox360Gamepad),
            new SettingEntry("KEYBOARD", ToolsStrings.Controls_KeyboardAndMouse)
        };
        #endregion

        #region Common
        private SettingEntry _inputMethod;

        public SettingEntry InputMethod {
            get => _inputMethod;
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
            get => _debouncingInterval;
            set {
                value = value.Clamp(0, 500);
                if (Equals(value, _debouncingInterval)) return;
                _debouncingInterval = value;
                OnPropertyChanged();
            }
        }

        public WheelAxleEntry[] WheelAxleEntries { get; } = {
            new WheelAxleEntry("STEER", ToolsStrings.Controls_Steering, false, true),
            new WheelAxleEntry("THROTTLE", ToolsStrings.Controls_Throttle),
            new WheelAxleEntry("BRAKES", ToolsStrings.Controls_Brakes, gammaMode: true),
            new WheelAxleEntry("CLUTCH", ToolsStrings.Controls_Clutch),
            new WheelAxleEntry(HandbrakeId, ToolsStrings.Controls_Handbrake)
        };

        private bool _combineWithKeyboardInput;

        public bool CombineWithKeyboardInput {
            get => _combineWithKeyboardInput;
            set {
                if (Equals(value, _combineWithKeyboardInput)) return;
                _combineWithKeyboardInput = value;
                OnPropertyChanged();
            }
        }

        public class WheelButtonCombined : NotifyPropertyChanged {
            public WheelButtonCombined([LocalizationRequired(false)] string id, string displayName, bool isNew = false) {
                IsNew = isNew;
                WheelButton = new WheelButtonEntry(id, displayName);
                KeyboardButton = new KeyboardButtonEntry(id, displayName);
            }

            public WheelButtonEntry WheelButton { get; }
            public KeyboardButtonEntry KeyboardButton { get; }
            public bool IsNew { get; }
        }

        public WheelButtonCombined[] WheelGearsButtonEntries { get; } = {
            new WheelButtonCombined("GEARUP", ToolsStrings.Controls_NextGear),
            new WheelButtonCombined("GEARDN", ToolsStrings.Controls_PreviousGear)
        };

        private bool _wheelUseHShifter;

        public bool WheelUseHShifter {
            get => _wheelUseHShifter;
            set {
                if (Equals(value, _wheelUseHShifter)) return;
                _wheelUseHShifter = value;
                OnPropertyChanged();
            }
        }

        private IDirectInputDevice _wheelHShifterDevice;

        public IDirectInputDevice WheelHShifterDevice {
            get => _wheelHShifterDevice;
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
            new WheelHShifterButtonEntry("GEAR_1", ToolsStrings.Controls_Gear_First, @"1"),
            new WheelHShifterButtonEntry("GEAR_2", ToolsStrings.Controls_Gear_Second, @"2"),
            new WheelHShifterButtonEntry("GEAR_3", ToolsStrings.Controls_Gear_Third, @"3"),
            new WheelHShifterButtonEntry("GEAR_4", ToolsStrings.Controls_Gear_Fourth, @"4"),
            new WheelHShifterButtonEntry("GEAR_5", ToolsStrings.Controls_Gear_Fifth, @"5"),
            new WheelHShifterButtonEntry("GEAR_6", ToolsStrings.Controls_Gear_Sixth, @"6"),
            new WheelHShifterButtonEntry("GEAR_7", ToolsStrings.Controls_Gear_Seventh, @"7"),
            new WheelHShifterButtonEntry("GEAR_R", ToolsStrings.Controls_Gear_Rear, @"R")
        };

        public WheelButtonCombined[] WheelCarButtonEntries { get; } = {
            new WheelButtonCombined("KERS", ToolsStrings.Controls_Kers),
            new WheelButtonCombined("DRS", ToolsStrings.Controls_Drs),
            new WheelButtonCombined(HandbrakeId, ToolsStrings.Controls_Handbrake),
            new WheelButtonCombined("ACTION_HEADLIGHTS", ToolsStrings.Controls_Headlights),
            new WheelButtonCombined("ACTION_HEADLIGHTS_FLASH", ToolsStrings.Controls_FlashHeadlights),
            new WheelButtonCombined("ACTION_HORN", ToolsStrings.Controls_Horn),
        };

        public WheelButtonCombined[] WheelCarBrakeButtonEntries { get; } = {
            new WheelButtonCombined("BALANCEUP", ToolsStrings.Controls_MoveToFront),
            new WheelButtonCombined("BALANCEDN", ToolsStrings.Controls_MoveToRear),
        };

        public WheelButtonCombined[] WheelCarTurboButtonEntries { get; } = {
            new WheelButtonCombined("TURBOUP", ToolsStrings.Controls_Increase),
            new WheelButtonCombined("TURBODN", ToolsStrings.Controls_Decrease),
        };

        public WheelButtonCombined[] WheelCarTractionControlButtonEntries { get; } = {
            new WheelButtonCombined("TCUP", ToolsStrings.Controls_Increase),
            new WheelButtonCombined("TCDN", ToolsStrings.Controls_Decrease),
        };

        public WheelButtonCombined[] WheelCarAbsButtonEntries { get; } = {
            new WheelButtonCombined("ABSUP", ToolsStrings.Controls_Increase),
            new WheelButtonCombined("ABSDN", ToolsStrings.Controls_Decrease),
        };

        public WheelButtonCombined[] WheelCarEngineBrakeButtonEntries { get; } = {
            new WheelButtonCombined("ENGINE_BRAKE_UP", ToolsStrings.Controls_Increase),
            new WheelButtonCombined("ENGINE_BRAKE_DN", ToolsStrings.Controls_Decrease),
        };

        public WheelButtonCombined[] WheelCarMgukButtonEntries { get; } = {
            new WheelButtonCombined("MGUK_DELIVERY_UP", ToolsStrings.Controls_Mguk_IncreaseDelivery),
            new WheelButtonCombined("MGUK_DELIVERY_DN", ToolsStrings.Controls_Mguk_DecreaseDelivery),
            new WheelButtonCombined("MGUK_RECOVERY_UP", ToolsStrings.Controls_Mguk_IncreaseRecovery),
            new WheelButtonCombined("MGUK_RECOVERY_DN", ToolsStrings.Controls_Mguk_DecreaseRecovery),
            new WheelButtonCombined("MGUH_MODE", ToolsStrings.Controls_Mguh_Mode)
        };

        public WheelButtonCombined[] WheelViewButtonEntries { get; } = {
            new WheelButtonCombined("GLANCELEFT", ToolsStrings.Controls_GlanceLeft),
            new WheelButtonCombined("GLANCERIGHT", ToolsStrings.Controls_GlanceRight),
            new WheelButtonCombined("GLANCEBACK", ToolsStrings.Controls_GlanceBack),
            new WheelButtonCombined("ACTION_CHANGE_CAMERA", ToolsStrings.Controls_ChangeCamera)
        };

        public WheelButtonCombined[] WheelGesturesButtonEntries { get; } = {
            new WheelButtonCombined("ACTION_CELEBRATE", ToolsStrings.Controls_Celebrate),
            new WheelButtonCombined("ACTION_CLAIM", ToolsStrings.Controls_Complain)
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
            get => _wheelFfbInvert;
            set {
                if (Equals(value, _wheelFfbInvert)) return;
                _wheelFfbInvert = value;
                OnPropertyChanged();
            }
        }

        private int _wheelFfbGain;

        public int WheelFfbGain {
            get => _wheelFfbGain;
            set {
                value = value.Clamp(0, 300);
                if (Equals(value, _wheelFfbGain)) return;
                _wheelFfbGain = value;
                OnPropertyChanged();
            }
        }

        private int _wheelFfbFilter;

        public int WheelFfbFilter {
            get => _wheelFfbFilter;
            set {
                value = value.Clamp(0, 99);
                if (Equals(value, _wheelFfbFilter)) return;
                _wheelFfbFilter = value;
                OnPropertyChanged();
            }
        }

        private double _wheelFfbMinForce;

        public double WheelFfbMinForce {
            get => _wheelFfbMinForce;
            set {
                value = value.Clamp(0, 50);
                if (Equals(value, _wheelFfbMinForce)) return;
                _wheelFfbMinForce = value;
                OnPropertyChanged();
            }
        }

        private double _wheelFfbWheelCenterBoostGain;

        public double WheelFfbWheelCenterBoostGain {
            get => _wheelFfbWheelCenterBoostGain;
            set {
                value = value.Clamp(0, 2000).Round(0.1);
                if (Equals(value, _wheelFfbWheelCenterBoostGain)) return;
                _wheelFfbWheelCenterBoostGain = value;
                OnPropertyChanged();
            }
        }

        private double _wheelFfbWheelCenterBoostRange;

        public double WheelFfbWheelCenterBoostRange {
            get => _wheelFfbWheelCenterBoostRange;
            set {
                value = value.Clamp(0, 2000).Round(0.1);
                if (Equals(value, _wheelFfbWheelCenterBoostRange)) return;
                _wheelFfbWheelCenterBoostRange = value;
                OnPropertyChanged();
            }
        }

        private int _wheelFfbKerbEffect;

        public int WheelFfbKerbEffect {
            get => _wheelFfbKerbEffect;
            set {
                value = value.Clamp(0, 2000);
                if (Equals(value, _wheelFfbKerbEffect)) return;
                _wheelFfbKerbEffect = value;
                OnPropertyChanged();
            }
        }

        private int _wheelFfbRoadEffect;

        public int WheelFfbRoadEffect {
            get => _wheelFfbRoadEffect;
            set {
                value = value.Clamp(0, 2000);
                if (Equals(value, _wheelFfbRoadEffect)) return;
                _wheelFfbRoadEffect = value;
                OnPropertyChanged();
            }
        }

        private int _wheelFfbSlipEffect;

        public int WheelFfbSlipEffect {
            get => _wheelFfbSlipEffect;
            set {
                value = value.Clamp(0, 2000);
                if (Equals(value, _wheelFfbSlipEffect)) return;
                _wheelFfbSlipEffect = value;
                OnPropertyChanged();
            }
        }

        private int _wheelFfbAbsEffect;

        public int WheelFfbAbsEffect {
            get => _wheelFfbAbsEffect;
            set {
                value = value.Clamp(0, 2000);
                if (Equals(value, _wheelFfbAbsEffect)) return;
                _wheelFfbAbsEffect = value;
                OnPropertyChanged();
            }
        }

        private bool _wheelFfbEnhancedUndersteer;

        public bool WheelFfbEnhancedUndersteer {
            get => _wheelFfbEnhancedUndersteer;
            set {
                if (Equals(value, _wheelFfbEnhancedUndersteer)) return;
                _wheelFfbEnhancedUndersteer = value;
                OnPropertyChanged();
            }
        }

        private int _wheelFfbSkipSteps;

        public int WheelFfbSkipSteps {
            get => _wheelFfbSkipSteps;
            set {
                value = value.Clamp(0, 1000);
                if (Equals(value, _wheelFfbSkipSteps)) return;
                _wheelFfbSkipSteps = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Keyboard
        private double _keyboardSteeringSpeed;

        public double KeyboardSteeringSpeed {
            get => _keyboardSteeringSpeed;
            set {
                value = value.Clamp(0.4, 3.0);
                if (Equals(value, _keyboardSteeringSpeed)) return;
                _keyboardSteeringSpeed = value;
                OnPropertyChanged();
            }
        }

        private double _keyboardOppositeLockSpeed;

        public double KeyboardOppositeLockSpeed {
            get => _keyboardOppositeLockSpeed;
            set {
                value = value.Clamp(1.0, 5.0);
                if (Equals(value, _keyboardOppositeLockSpeed)) return;
                _keyboardOppositeLockSpeed = value;
                OnPropertyChanged();
            }
        }

        private double _keyboardReturnRate;

        public double KeyboardReturnRate {
            get => _keyboardReturnRate;
            set {
                value = value.Clamp(1.0, 5.0);
                if (Equals(value, _keyboardReturnRate)) return;
                _keyboardReturnRate = value;
                OnPropertyChanged();
            }
        }

        private bool _keyboardMouseSteering;

        public bool KeyboardMouseSteering {
            get => _keyboardMouseSteering;
            set {
                if (Equals(value, _keyboardMouseSteering)) return;
                _keyboardMouseSteering = value;
                OnPropertyChanged();
            }
        }

        private bool _keyboardMouseButtons;

        public bool KeyboardMouseButtons {
            get => _keyboardMouseButtons;
            set {
                if (Equals(value, _keyboardMouseButtons)) return;
                _keyboardMouseButtons = value;
                OnPropertyChanged();
            }
        }

        private double _keyboardMouseSteeringSpeed;

        public double KeyboardMouseSteeringSpeed {
            get => _keyboardMouseSteeringSpeed;
            set {
                value = value.Clamp(0.1, 1.0);
                if (Equals(value, _keyboardMouseSteeringSpeed)) return;
                _keyboardMouseSteeringSpeed = value;
                OnPropertyChanged();
            }
        }

        public KeyboardButtonEntry[] KeyboardSpecificButtonEntries { get; }
        #endregion

        #region Main
        private const string HandbrakeId = "HANDBRAKE";

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

            switch (e.PropertyName) {
                case nameof(BaseEntry<IInputProvider>.Input):
                    var senderEntry = sender as IDirectInputEntry;
                    if (senderEntry?.Id == HandbrakeId) {
                        try {
                            _skip = true;
                            foreach (var entry in Entries.OfType<IDirectInputEntry>().Where(x => x.Id == HandbrakeId && x.Device?.Same(senderEntry.Device) != true)) {
                                entry.Clear();
                            }
                        } finally {
                            _skip = false;
                        }
                    }

                    if (sender is WheelHShifterButtonEntry) {
                        UpdateWheelHShifterDevice();
                    }
                    break;
                case nameof(WheelAxleEntry.Value):
                    return;
            }

            Save();
        }

        private CommandBase _toggleWaitingCommand;

        public ICommand ToggleWaitingCommand => _toggleWaitingCommand ?? (_toggleWaitingCommand = new DelegateCommand<IEntry>(o => {
            foreach (var entry in Entries.ApartFrom(o)) {
                entry.Waiting = false;
            }

            o.Waiting = !o.Waiting;
        }, o => o != null));

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

            if (GetWaiting() is KeyboardButtonEntry) {
                AssignInput(GetKeyboardInputButton(KeyInterop.VirtualKeyFromKey(key))).Forget();
                return true;
            }

            return false;
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
            WheelFfbWheelCenterBoostGain = section.GetDouble("CENTER_BOOST_GAIN", 0.0) * 100d;
            WheelFfbWheelCenterBoostRange = section.GetDouble("CENTER_BOOST_RANGE", 0.1) * 100d;

            section = ini["FF_ENHANCEMENT"];
            WheelFfbKerbEffect = section.GetDouble("CURBS", 0.4).ToIntPercentage();
            WheelFfbRoadEffect = section.GetDouble("ROAD", 0.5).ToIntPercentage();
            WheelFfbSlipEffect = section.GetDouble("SLIPS", 0.0).ToIntPercentage();
            WheelFfbAbsEffect = section.GetDouble("ABS", 0.25).ToIntPercentage();

            section = ini["FF_ENHANCEMENT_2"];
            WheelFfbEnhancedUndersteer = section.GetBool("UNDERSTEER", false);

            section = ini["FF_SKIP_STEPS"];
            WheelFfbSkipSteps = section.GetInt("VALUE", 0);
        }

        public void SaveFfbToIni(IniFile ini) {
            var section = ini["STEER"];
            section.Set("FF_GAIN", (WheelFfbInvert ? -1d : 1d) * WheelFfbGain.ToDoublePercentage());
            section.Set("FILTER_FF", WheelFfbFilter.ToDoublePercentage());

            section = ini["FF_TWEAKS"];
            section.Set("MIN_FF", WheelFfbMinForce / 100d);
            section.Set("CENTER_BOOST_GAIN", WheelFfbWheelCenterBoostGain / 100d);
            section.Set("CENTER_BOOST_RANGE", WheelFfbWheelCenterBoostRange / 100d);

            section = ini["FF_ENHANCEMENT"];
            section.Set("CURBS", WheelFfbKerbEffect.ToDoublePercentage());
            section.Set("ROAD", WheelFfbRoadEffect.ToDoublePercentage());
            section.Set("SLIPS", WheelFfbSlipEffect.ToDoublePercentage());
            section.Set("ABS", WheelFfbAbsEffect.ToDoublePercentage());

            section = ini["FF_ENHANCEMENT_2"];
            section.Set("UNDERSTEER", WheelFfbEnhancedUndersteer);

            section = ini["FF_SKIP_STEPS"];
            section.Set("VALUE", WheelFfbSkipSteps);
        }

        private static readonly Dictionary<string, string> ProductGuids = new Dictionary<string, string>();

        protected override void LoadFromIni() {
            if (Devices.Count == 0) {
                // RescanDevices();
            }

            InputMethod = Ini["HEADER"].GetEntry("INPUT_METHOD", InputMethods);
            CombineWithKeyboardInput = Ini["ADVANCED"].GetBool("COMBINE_WITH_KEYBOARD_CONTROL", false);
            WheelUseHShifter = Ini["SHIFTER"].GetBool("ACTIVE", false);
            DebouncingInterval = Ini["STEER"].GetInt("DEBOUNCING_MS", 50);

            foreach (var device in Devices.OfType<IDirectInputDevice>().Union(_placeholderDevices)) {
                device.OriginalIniIds.Clear();
            }

            var section = Ini["CONTROLLERS"];
            var devices = section.Keys.Where(x => x.StartsWith(@"CON")).Select(x => new {
                IniId = FlexibleParser.TryParseInt(x.Substring(3)),
                Name = section.GetNonEmpty(x)
            }).TakeWhile(x => x.Name != null && x.IniId.HasValue).Select(x => {
                var id = section.GetNonEmpty($"PGUID{x.IniId}");
                if (id != null) {
                    ProductGuids[x.Name] = id;
                }

                var device = Devices.FirstOrDefault(y => y.Device.InstanceName == x.Name) ?? Devices.GetByIdOrDefault(id);
                if (device != null) {
                    device.OriginalIniIds.Add(x.IniId.Value);
                    return device;
                }

                return (IDirectInputDevice)GetPlaceholderDevice(id, x.Name, x.IniId.Value);
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

            section = Ini["__EXTRA_CM"];
            AcSettingsHolder.FfPostProcess.Import(section.GetNonEmpty("FF_POST_PROCESS").FromCutBase64());
            AcSettingsHolder.System.ImportFfb(section.GetNonEmpty("SYSTEM").FromCutBase64());

            section = Ini["__LAUNCHER_CM"];
            var name = section.GetNonEmpty("PRESET_NAME");
            CurrentPresetFilename = name == null ? null : Path.Combine(PresetsDirectory, name);
            CurrentPresetChanged = CurrentPresetName != null && section.GetBool("PRESET_CHANGED", true);
        }

        public void SaveControllers() {
            FileUtils.EnsureDirectoryExists(PresetsDirectory);

            foreach (var device in Devices) {
                var filename = Path.Combine(PresetsDirectory, device.Device.InstanceGuid + ".ini");

                var ini = new IniFile {
                    ["CONTROLLER"] = {
                        ["NAME"] = device.DisplayName,
                        ["PRODUCT"] = device.Device.ProductGuid.ToString()
                    }
                };

                foreach (var entry in WheelAxleEntries.Where(x => x.Input?.Device == device)) {
                    ini[entry.Id].Set("AXLE", entry.Input?.Id);
                }

                foreach (var entry in WheelButtonEntries.Select(x => x.WheelButton).Where(x => x.Input?.Device == device)) {
                    ini[entry.Id].Set("BUTTON", entry.Input?.Id);
                }

                foreach (var entry in WheelHShifterButtonEntries.Where(x => x.Input?.Device == device)) {
                    ini["SHIFTER"].Set(entry.Id, entry.Input?.Id);
                }

                ini.Save(filename);
            }
        }

        protected override void SetToIni() {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            Ini["HEADER"].Set("INPUT_METHOD", InputMethod);

            UpdatePlaceholders();
            Ini["CONTROLLERS"] = Devices
                    .Select(x => (IDirectInputDevice)x)
                    .Union(_placeholderDevices)
                    .Aggregate(new IniFileSection(null), (s, d, i) => {
                        s.Set("CON" + d.Index, d.DisplayName);
                        s.Set("PGUID" + d.Index, d.Id ?? ProductGuids.GetValueOrDefault(d.DisplayName));
                        return s;
                    });

            Ini["ADVANCED"].Set("COMBINE_WITH_KEYBOARD_CONTROL", CombineWithKeyboardInput);
            Ini["SHIFTER"].Set("ACTIVE", WheelUseHShifter);
            Ini["SHIFTER"].Set("JOY", WheelHShifterButtonEntries.FirstOrDefault(x => x.Input != null)?.Input?.Device.Index);
            Ini["STEER"].Set("DEBOUNCING_MS", DebouncingInterval);

            foreach (var entry in Entries.OfType<IDirectInputEntry>()) {
                Ini[entry.Id]["JOY"] = -1;
            }

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

            section = Ini["__EXTRA_CM"];
            section.Set("FF_POST_PROCESS", AcSettingsHolder.FfPostProcess.Export().ToCutBase64());
            section.Set("SYSTEM", AcSettingsHolder.System.ExportFfb().ToCutBase64());

            section = Ini["__LAUNCHER_CM"];
            section.Set("PRESET_NAME", CurrentPresetFilename?.SubstringExt(PresetsDirectory.Length + 1));
            section.Set("PRESET_CHANGED", CurrentPresetChanged);

            SaveControllers();

#if DEBUG
            Logging.Write($"Controls saving time: {sw.Elapsed.TotalMilliseconds:F2} ms");
#endif
        }

        public void SavePreset(string filename) {
            CurrentPresetFilename = filename;
            CurrentPresetChanged = false;

            SaveImmediately(); // save original PRESET_NAME
            Ini.Save(filename); // and as a preset
        }
        #endregion
    }
}
