using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.GameProperties.InGameApp;
using AcManager.Tools.Helpers.AcSettingsControls;
using AcManager.Tools.Helpers.DirectInput;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.WheelAngles;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using SlimDX.DirectInput;
using StringBasedFilter;
using Application = System.Windows.Application;
using Key = System.Windows.Input.Key;

namespace AcManager.Tools.Helpers.AcSettings {
    public class ControlsSettings : IniSettings, IDisposable {
        public static bool OptionDebugControlles = false;
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
            _keyboardInput = new Dictionary<int, KeyboardInputButton>();

            WheelAxleEntries = new[] {
                SteerAxleEntry,
                new WheelAxleEntry("THROTTLE", ToolsStrings.Controls_Throttle),
                new WheelAxleEntry("BRAKES", ToolsStrings.Controls_Brakes, gammaMode: true),
                new WheelAxleEntry("CLUTCH", ToolsStrings.Controls_Clutch),
                new WheelAxleEntry(HandbrakeId, ToolsStrings.Controls_Handbrake)
            };

            KeyboardSpecificButtonEntries = new[] {
                new KeyboardSpecificButtonEntry("GAS", ToolsStrings.Controls_Throttle),
                new KeyboardSpecificButtonEntry("BRAKE", ToolsStrings.Controls_Brakes),
                new KeyboardSpecificButtonEntry("RIGHT", ToolsStrings.Controls_SteerRight),
                new KeyboardSpecificButtonEntry("LEFT", ToolsStrings.Controls_SteerLeft)
            }.Union(WheelGearsButtonEntries.Select(x => x.KeyboardButton)).ToArray();

            #region Constructing system entries
            SystemRaceButtonEntries = new[] {
                new SystemButtonEntryCombined("__CM_PAUSE", "Pause race", fixedValueCallback: x => new[]{ Keys.Escape }),
                new SystemButtonEntryCombined("__CM_START_SESSION", "Start race", customCommand: true),
                new SystemButtonEntryCombined("__CM_START_STOP_SESSION", "Start/setup", customCommand: true,
                        toolTip: "Combined command: if you’re in pits, driving will started, otherwise, you’ll get to pits", delayed: true),
                new SystemButtonEntryCombined("RESET_RACE", "Restart race", delayed: true),
                new SystemButtonEntryCombined("__CM_RESET_SESSION", "Restart session", customCommand: true, delayed: true),
                new SystemButtonEntryCombined("__CM_TO_PITS", "Teleport to pits", customCommand: true, delayed: true),
                new SystemButtonEntryCombined("__CM_SETUP_CAR", "Setup in pits", customCommand: true, delayed: true),
                new SystemButtonEntryCombined("__CM_EXIT", "Exit the race", customCommand: true, delayed: true),
            };

            SystemUiButtonEntries = new[] {
                new SystemButtonEntryCombined("HIDE_APPS", "Apps", defaultKey: Keys.H),
                new SystemButtonEntryCombined("__CM_NEXT_APPS_DESKTOP", "Next desktop", fixedValueCallback: x => new[] { Keys.Control, Keys.U }),
                new SystemButtonEntryCombined("HIDE_DAMAGE", "Damage display", defaultKey: Keys.Q),
                new SystemButtonEntryCombined("DRIVER_NAMES", "Driver names", defaultKey: Keys.L),
                new SystemButtonEntryCombined("IDEAL_LINE", "Ideal line", defaultKey: Keys.I),
            };

            SystemReplayButtonEntries = new[] {
                new SystemButtonEntryCombined("START_REPLAY", "Start replay", defaultKey: Keys.R),
                new SystemButtonEntryCombined("PAUSE_REPLAY", "Pause replay", defaultKey: Keys.Space),
                new SystemButtonEntryCombined("NEXT_LAP", "Next lap", defaultKey: Keys.N),
                new SystemButtonEntryCombined("PREVIOUS_LAP", "Previous lap", defaultKey: Keys.P),
                new SystemButtonEntryCombined("NEXT_CAR", "Next car", defaultKey: Keys.NumPad3),
                new SystemButtonEntryCombined("PREVIOUS_CAR", "Previous car", defaultKey: Keys.NumPad1),
                new SystemButtonEntryCombined("SLOWMO", "Slow motion", defaultKey: Keys.S),
                new SystemButtonEntryCombined("FFWD", "Fast-forward", defaultKey: Keys.F),
                new SystemButtonEntryCombined("REV", "Rewind", defaultKey: Keys.D),
            };

            SystemOnlineButtonEntries = new[] {
                new SystemButtonEntryCombined("__CM_ONLINE_POLL_YES", "Poll: vote Yes", fixedValueCallback: x => new[]{ Keys.Y }, delayed: true),
                new SystemButtonEntryCombined("__CM_ONLINE_POLL_NO", "Poll: vote No", fixedValueCallback: x => new[]{ Keys.N }, delayed: true)
            };

            SystemDiscordButtonEntries = new[] {
                new SystemButtonEntryCombined("__CM_DISCORD_REQUEST_ACCEPT", "Accept join request", fixedValueCallback: x => new[]{ Keys.Enter }, delayed: true),
                new SystemButtonEntryCombined("__CM_DISCORD_REQUEST_DENY", "Deny join request", fixedValueCallback: x => new[]{ Keys.Back }, delayed: true)
            };

            var systemAbs = new SystemButtonEntryCombined("ABS", "ABS", true, defaultKey: Keys.A);
            var systemTractionControl = new SystemButtonEntryCombined("TRACTION_CONTROL", "Traction control", true, defaultKey: Keys.T);

            SystemCarButtonEntries = new[] {
                new SystemButtonEntryCombined("MOUSE_STEERING", "Mouse steering", defaultKey: Keys.M),
                new SystemButtonEntryCombined("ACTIVATE_AI", "Toggle AI", defaultKey: Keys.C),
                new SystemButtonEntryCombined("AUTO_SHIFTER", "Auto shifter", defaultKey: Keys.G),
                systemAbs,
                new SystemButtonEntryCombined("__CM_ABS_DECREASE", "ABS (decrease)", fixedValueCallback: key => key.HasValue ? new [] {
                    Keys.Control, Keys.Shift, key.Value
                } : null, buttonReference: systemAbs.SystemButton),
                systemTractionControl,
                new SystemButtonEntryCombined("__CM_TRACTION_CONTROL_DECREASE", "Traction c. (decrease)", fixedValueCallback: key => key.HasValue ? new [] {
                    Keys.Control, Keys.Shift, key.Value
                } : null, buttonReference: systemTractionControl.SystemButton),
                // new SystemButtonEntryCombined("KERS", "KERS", defaultKey: Keys.K),
            };
            #endregion

            AcSettingsHolder.Python.AppActiveStateChanged += OnAppActiveStateChanged;
            AcSettingsHolder.Video.PropertyChanged += OnVideoPropertyChanged;
            RefreshOverlayAvailable();
        }

        private void OnVideoPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(AcSettingsHolder.Video.Fullscreen)) {
                RefreshOverlayAvailable();
            }
        }

        private void OnAppActiveStateChanged(object sender, EventArgs e) {
            RefreshOverlayAvailable();
        }

        internal void FinishInitialization() {
            Reload();
            foreach (var entry in Entries) {
                entry.PropertyChanged += EntryPropertyChanged;
            }

            UpdateWheelHShifterDevice();
        }

        #region Devices
        private DirectInputScanner.Watcher _devicesScan;

        [CanBeNull]
        public DirectInputScanner.Watcher DevicesScan {
            get => _devicesScan;
            set {
                if (Equals(value, _devicesScan)) return;
                DisposeHelper.Dispose(ref _devicesScan);
                _devicesScan = value;
                OnPropertyChanged();
            }
        }

        private void DisposeDevicesScan() {
            _devicesScan?.Dispose();
            DevicesScan = null;
        }

        private readonly Dictionary<int, KeyboardInputButton> _keyboardInput;

        private int _used;

        public int Used {
            get => _used;
            set {
                if (Equals(_used, value)) return;

                if (_used == 0) {
                    DisposeDevicesScan();
                    DevicesScan = DirectInputScanner.Watch();
                    DevicesScan.Update += OnDevicesUpdate;
                    IsScanningInProgress = !DevicesScan.HasData;
                    RescanDevices(DevicesScan.Get());

                    CompositionTargetEx.Rendering -= OnTick;
                    CompositionTargetEx.Rendering += OnTick;
                    Logging.Debug("Subscribed");
                }

                _used = value;
                OnPropertyChanged(false);
                UpdateInputs();

                if (value == 0) {
                    DisposeDevicesScan();
                    CompositionTargetEx.Rendering -= OnTick;
                    Logging.Debug("Unsubscribed");
                }
            }
        }

        private bool _isScanningInProgress;

        public bool IsScanningInProgress {
            get => _isScanningInProgress;
            set => Apply(value, ref _isScanningInProgress);
        }

        private void OnDevicesUpdate(object sender, EventArgs eventArgs) {
            IsScanningInProgress = false;
            var watcher = (DirectInputScanner.Watcher)sender;
            RescanDevices(watcher.Get());

            if (DevicesScan?.ScanTime.TotalSeconds > 1 && !ValuesStorage.Contains("Settings.DriveSettings.ScanControllersAutomatically")) {
                SettingsHolder.Drive.ScanControllersAutomatically = true;
            }

            object options = null;
            IsHardwareLockSupported = Devices.Any(x => {
                if (!WheelSteerLock.IsSupported(x.Id, out var o)) return false;
                if (o != null) options = o;
                return true;
            });
            HardwareLockOptions = options;

            Save();
        }

        private DelegateCommand _runControlPanelCommand;

        public DelegateCommand RunControlPanelCommand => _runControlPanelCommand ?? (_runControlPanelCommand = new DelegateCommand(() => {
            Devices.FirstOrDefault()?.RunControlPanel();
        }));

        private Stopwatch _rescanStopwatch;

        private void UpdateInputs() {
            if (Used == 0) return;

            if (_rescanStopwatch == null) {
                _rescanStopwatch = Stopwatch.StartNew();
            }

            foreach (var device in Devices) {
                device.OnTick();
            }

            foreach (var key in _keyboardInput.Values.Where(x => x.Used > 0)) {
                key.Value = User32.IsAsyncKeyPressed(key.Key);
            }
        }

        private void OnTick(object sender, EventArgs e) {
            if (Application.Current?.MainWindow?.IsVisible == true) {
                UpdateInputs();
            }
        }

        public void Dispose() {
            CompositionTargetEx.Rendering -= OnTick;
            Devices.DisposeEverything();
            DisposeDevicesScan();
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

        private void RescanDevices([CanBeNull] IList<DeviceInstance> devices) {
            _skip = true;

            try {
                var directInput = DirectInputScanner.DirectInput;
                var newDevices = devices == null || directInput == null ? new List<DirectInputDevice>()
                        : devices.Select((x, i) => Devices.FirstOrDefault(y => y.Same(x)) ??
                                DirectInputDevice.Create(directInput, x, i)).NonNull().ToList();

                var checkedPlaceholders = new List<PlaceholderInputDevice>();
                void EnsureIndicesMatch(PlaceholderInputDevice placeholder, DirectInputDevice actualDevice) {
                    if (checkedPlaceholders.Contains(placeholder)) return;
                    checkedPlaceholders.Add(placeholder);

                    if (!placeholder.OriginalIniIds.Contains(actualDevice.Index)) {
                        if (OptionDebugControlles) {
                            Logging.Warning(
                                    $"{placeholder.DisplayName} index changed: saved as {placeholder.OriginalIniIds.JoinToString("+")}, actual — {actualDevice.Index}");
                        }

                        FixMessedOrderAsync().Forget();
                    }
                }

                foreach (var entry in Entries.OfType<BaseEntry<DirectInputAxle>>()) {
                    var current = entry.Input?.Device;
                    if (current is PlaceholderInputDevice placeholder) {
                        var replacement = newDevices.FirstOrDefault(x => x.Same(current));
                        if (replacement != null) {
                            EnsureIndicesMatch(placeholder, replacement);
                            entry.Input = replacement.GetAxle(entry.Input.Id);
                        }
                    } else if (current is DirectInputDevice && !newDevices.Contains(current)) {
                        entry.Input = GetPlaceholderDevice((DirectInputDevice)current).GetAxle(entry.Input.Id);
                    }
                }

                void GetActualInput(BaseEntry<DirectInputButton> entry, IDirectInputDevice replacement) {
                    if (entry.Input is DirectInputPov pov) {
                        entry.Input = replacement.GetPov(entry.Input.Id, pov.Direction);
                    } else if (entry.Input != null) {
                        entry.Input = replacement.GetButton(entry.Input.Id);
                    }
                }

                foreach (var entry in Entries.OfType<BaseEntry<DirectInputButton>>()) {
                    var current = entry.Input?.Device;
                    if (current is PlaceholderInputDevice placeholder) {
                        var replacement = newDevices.FirstOrDefault(x => x.Same(current));
                        if (replacement != null) {
                            EnsureIndicesMatch(placeholder, replacement);
                            GetActualInput(entry, replacement);
                        }
                    } else if (current is DirectInputDevice && !newDevices.Contains(current)) {
                        GetActualInput(entry, GetPlaceholderDevice((DirectInputDevice)current));
                    }
                }

                foreach (var device in Devices.ApartFrom(newDevices)) {
                    for (var i = 0; i < device.Axis.Length; i++) {
                        device.Axis[i].PropertyChanged -= DeviceAxleEventHandler;
                    }

                    for (var i = 0; i < device.Buttons.Length; i++) {
                        device.Buttons[i].PropertyChanged -= DeviceButtonEventHandler;
                    }

                    for (var i = 0; i < device.Povs.Length; i++) {
                        device.Povs[i].PropertyChanged -= DevicePovEventHandler;
                    }

                    device.Dispose();
                }

                Devices.ReplaceEverythingBy(newDevices);
                UpdatePlaceholders();

                foreach (var device in newDevices) {
                    ProductGuids[device.DisplayName] = device.Id;

                    for (var i = 0; i < device.Axis.Length; i++) {
                        device.Axis[i].PropertyChanged += DeviceAxleEventHandler;
                    }

                    for (var i = 0; i < device.Buttons.Length; i++) {
                        device.Buttons[i].PropertyChanged += DeviceButtonEventHandler;
                    }

                    for (var i = 0; i < device.Povs.Length; i++) {
                        device.Povs[i].PropertyChanged += DevicePovEventHandler;
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
                if (OptionDebugControlles) {
                    Logging.Debug($"Waiting entry: {waiting?.DisplayName ?? "none"}");
                }
                if (waiting == null) return;

                if (waiting.Input == provider) {
                    waiting.IsWaiting = false;
                    if (OptionDebugControlles) {
                        Logging.Debug("Input equals to provider");
                    }
                    return;
                }

                if (!waiting.IsCompatibleWith(provider)) {
                    if (OptionDebugControlles) {
                        Logging.Debug("Not compatible with given input");
                    }
                    return;
                }

                var existing = Entries.OfType<BaseEntry<T>>().Where(x => x.Input == provider && (waiting.Layer & x.Layer) != 0).ToList();
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
                if (OptionDebugControlles) {
                    Logging.Debug($"Axle changed: {axle.Device}, {axle.DisplayName} (ID={axle.Id}; busy={_busy})");
                }

                AssignInput(axle).Forget();
            }
        }

        private void DeviceButtonEventHandler(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(DirectInputButton.Value) && sender is DirectInputButton button && button.Value) {
                AssignInput(button).Forget();
            }
        }

        private void DevicePovEventHandler(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(DirectInputPov.Value) && sender is DirectInputPov pov && pov.Value) {
                AssignInput<DirectInputButton>(pov).Forget();
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
            if (IsSaving || IsLoading) return;

            if (CurrentPresetName != null) {
                CurrentPresetChanged = true;
            }

            base.Save();
        }

        // For system settings, with some FFB stuff
        internal void OnSystemFfbSettingsChanged() {
            CurrentPresetChanged = true;
            Save();
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
                if (!InputMethods.ArrayContains(value)) value = InputMethods[0];
                if (Equals(value, _inputMethod)) return;
                _inputMethod = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Wheel
        private bool _wheelSteerScaleAutoAdjust;

        public bool WheelSteerScaleAutoAdjust {
            get => _wheelSteerScaleAutoAdjust;
            set => Apply(value, ref _wheelSteerScaleAutoAdjust);
        }

        private double _wheelSteerScale;

        public double WheelSteerScale {
            get => _wheelSteerScale;
            set => Apply(value, ref _wheelSteerScale);
        }

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

        public WheelAxleEntry SteerAxleEntry { get; } = new WheelAxleEntry("STEER", ToolsStrings.Controls_Steering, false, true);
        public WheelAxleEntry[] WheelAxleEntries { get; }

        private bool _combineWithKeyboardInput;

        public bool CombineWithKeyboardInput {
            get => _combineWithKeyboardInput;
            set => Apply(value, ref _combineWithKeyboardInput);
        }

        public WheelButtonCombined[] WheelGearsButtonEntries { get; } = {
            new WheelButtonCombined("GEARUP", ToolsStrings.Controls_NextGear),
            new WheelButtonCombined("GEARDN", ToolsStrings.Controls_PreviousGear)
        };

        private bool _wheelUseHShifter;

        public bool WheelUseHShifter {
            get => _wheelUseHShifter;
            set => Apply(value, ref _wheelUseHShifter);
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
            set => Apply(value, ref _wheelFfbInvert);
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
            set => Apply(value, ref _wheelFfbEnhancedUndersteer);
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
            set => Apply(value, ref _keyboardMouseSteering);
        }

        private bool _keyboardMouseButtons;

        public bool KeyboardMouseButtons {
            get => _keyboardMouseButtons;
            set => Apply(value, ref _keyboardMouseButtons);
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

        #region System shortcuts (Ctrl+…)
        public SystemButtonEntryCombined[] SystemRaceButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemCarButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemUiButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemReplayButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemOnlineButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemDiscordButtonEntries { get; }

        [NotNull]
        public IEnumerable<SystemButtonEntryCombined> SystemButtonEntries  => SystemRaceButtonEntries
                    .Concat(SystemCarButtonEntries)
                    .Concat(SystemUiButtonEntries)
                    .Concat(SystemReplayButtonEntries)
                    .Concat(SystemOnlineButtonEntries)
                    .Concat(SystemDiscordButtonEntries);

        [NotNull]
        public IEnumerable<string> SystemButtonKeys => SystemButtonEntries.Select(x => x.WheelButton.Id).NonNull();

        private bool _delaySpecificSystemCommands;

        public bool DelaySpecificSystemCommands {
            get => _delaySpecificSystemCommands;
            set => Apply(value, ref _delaySpecificSystemCommands);
        }

        private bool _showSystemDelays;

        public bool ShowSystemDelays {
            get => _showSystemDelays;
            set => Apply(value, ref _showSystemDelays);
        }

        private bool _systemIgnorePovInPits;

        public bool SystemIgnorePovInPits {
            get => _systemIgnorePovInPits;
            set => Apply(value, ref _systemIgnorePovInPits);
        }

        #region Overlay-availability-related options
        public class AppsForOverlayCollection : WrappedFilteredCollection<PythonAppObject, PythonAppObject> {
            public AppsForOverlayCollection() : base(PythonAppsManager.Instance.Enabled) { }

            protected override PythonAppObject Wrap(PythonAppObject source) {
                return source;
            }

            protected override bool Test(PythonAppObject source) {
                return Array.IndexOf(CmInGameAppHelper.OverlayAppIds, source.Id) != -1;
            }
        }

        public AppsForOverlayCollection AppsForOverlay { get; } = new AppsForOverlayCollection();

        private bool _isOverlayAvailable;

        public bool IsOverlayAvailable {
            get => _isOverlayAvailable;
            private set => Apply(value, ref _isOverlayAvailable);
        }

        private void RefreshOverlayAvailable() {
            IsOverlayAvailable = !AcSettingsHolder.Video.Fullscreen || AppsForOverlay.Any(x => AcSettingsHolder.Python.IsActivated(x.Id));
        }
        #endregion

        private bool _hardwareLock;

        public bool HardwareLock {
            get => _hardwareLock;
            set => Apply(value, ref _hardwareLock);
        }

        private bool _isHardwareLockSupported;

        public bool IsHardwareLockSupported {
            get => _isHardwareLockSupported;
            set => Apply(value, ref _isHardwareLockSupported);
        }

        private object _hardwareLockOptions;

        public object HardwareLockOptions {
            get => _hardwareLockOptions;
            set => Apply(value, ref _hardwareLockOptions);
        }

        public string DisplayHardwareLockSupported { get; } = WheelSteerLock.GetSupportedNames().OrderBy(x => x).JoinToReadableString();
        #endregion

        #region Main
        private const string HandbrakeId = "HANDBRAKE";

        [NotNull]
        private IEnumerable<IEntry> Entries
            => WheelAxleEntries
                    .Select(x => (IEntry)x)
                    .Union(WheelButtonEntries.Select(x => x.KeyboardButton))
                    .Union(WheelButtonEntries.Select(x => x.WheelButton))
                    .Union(WheelHShifterButtonEntries)
                    .Union(KeyboardSpecificButtonEntries)
                    .Union(SystemButtonEntries.Select(x => x.SystemButton).NonNull())
                    .Union(SystemButtonEntries.Select(x => x.WheelButton));

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
                entry.IsWaiting = false;
            }

            o.IsWaiting = !o.IsWaiting;
        }, o => o != null));

        [CanBeNull]
        public IEntry GetWaiting() {
            return Entries.FirstOrDefault(x => x.IsWaiting);
        }

        public bool StopWaiting() {
            var found = false;
            foreach (var entry in Entries.Where(x => x.IsWaiting)) {
                entry.IsWaiting = false;
                found = true;
            }

            return found;
        }

        public bool ClearWaiting() {
            var found = false;
            foreach (var entry in Entries.Where(x => x.IsWaiting)) {
                entry.Clear();
                found = true;
            }

            return found;
        }

        public bool AssignKey(Key key) {
            if (!key.IsInputAssignable()) {
                Logging.Debug("Not assignable: " + key);
                return false;
            }

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

        private bool _fixingMessedUpOrder;

        private async Task FixMessedOrderAsync() {
            if (_fixingMessedUpOrder || !SettingsHolder.Drive.CheckAndFixControlsOrder) return;
            _fixingMessedUpOrder = true;
            await Task.Yield();

            if (_fixingMessedUpOrder) {
                Save();
            }

            Toast.Show("Controls config fixed", "Controllers have changed in order, so Content Manager re-saved your configuration correctly");
        }

        protected override void LoadFromIni() {
            if (Devices.Count == 0) {
                // RescanDevices();
            }

            InputMethod = Ini["HEADER"].GetEntry("INPUT_METHOD", InputMethods);
            CombineWithKeyboardInput = Ini["ADVANCED"].GetBool("COMBINE_WITH_KEYBOARD_CONTROL", false);
            WheelUseHShifter = Ini["SHIFTER"].GetBool("ACTIVE", false);
            DebouncingInterval = Ini["STEER"].GetInt("DEBOUNCING_MS", 50);
            WheelSteerScale = Ini["STEER"].GetDouble("SCALE", 1d);

            foreach (var device in Devices.OfType<IDirectInputDevice>().Union(_placeholderDevices)) {
                device.OriginalIniIds.Clear();
            }

            var section = Ini["CONTROLLERS"];
            var orderChanged = false;
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
                    if (OptionDebugControlles) {
                        Logging.Debug($"{device.DisplayName}: actual index={device.Index}, config index={x.IniId}");
                    }

                    if (device.Index != x.IniId) {
                        orderChanged = true;
                    }

                    device.OriginalIniIds.Add(x.IniId.Value);
                    return device;
                }

                return (IDirectInputDevice)GetPlaceholderDevice(id, x.Name, x.IniId.Value);
            }).ToList();

            if (OptionDebugControlles) {
                if (orderChanged) {
                    Logging.Warning("Devices from config loaded: " + devices.Count + "; order changed!");
                } else {
                    Logging.Debug("Devices from config loaded: " + devices.Count);
                }
            }

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
            WheelSteerScaleAutoAdjust = section.GetBool("AUTO_ADJUST_SCALE", false);
            DelaySpecificSystemCommands = section.GetBool("DELAY_SPECIFIC_SYSTEM_COMMANDS", true);
            ShowSystemDelays = section.GetBool("SHOW_SYSTEM_DELAYS", true);
            SystemIgnorePovInPits = section.GetBool("SYSTEM_IGNORE_POV_IN_PITS", true);
            HardwareLock = section.GetBool("HARDWARE_LOCK", false);
            AcSettingsHolder.FfPostProcess.Import(section.GetNonEmpty("FF_POST_PROCESS").FromCutBase64()?.ToUtf8String());
            AcSettingsHolder.System.ImportFfb(section.GetNonEmpty("SYSTEM").FromCutBase64()?.ToUtf8String());

            section = Ini["__LAUNCHER_CM"];
            var name = section.GetNonEmpty("PRESET_NAME");
            CurrentPresetFilename = name == null ? null : Path.Combine(PresetsDirectory, name);
            CurrentPresetChanged = CurrentPresetName != null && section.GetBool("PRESET_CHANGED", true);

            if (orderChanged) {
                FixMessedOrderAsync().Forget();
            }
        }

        public void SaveControllers() {
            _fixingMessedUpOrder = false;
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
            Ini["STEER"].Set("SCALE", WheelSteerScale);

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
            section.Set("AUTO_ADJUST_SCALE", WheelSteerScaleAutoAdjust);
            section.Set("DELAY_SPECIFIC_SYSTEM_COMMANDS", DelaySpecificSystemCommands);
            section.Set("SHOW_SYSTEM_DELAYS", ShowSystemDelays);
            section.Set("SYSTEM_IGNORE_POV_IN_PITS", SystemIgnorePovInPits);
            section.Set("HARDWARE_LOCK", HardwareLock);

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

        public void FixControllersOrder() {
            if (Devices.Count == 0) {
                Logging.Warning("Devices are not yet scanned, scanning…");
                var fixTimeout = TimeSpan.FromSeconds(1);
                using (var timeout = new CancellationTokenSource(fixTimeout)) {
                    var list = DirectInputScanner.GetAsync(timeout.Token).Result;
                    Logging.Write("Scanned result: " + (list == null ? @"failed to scan" : $@"{list.Count} device(s)"));
                    if (list == null) return;

                    RescanDevices(list);
                }
            }

            if (_fixingMessedUpOrder) {
                Logging.Write("Controllers order is fixed");
                SaveImmediately();
            }
        }
    }
}
