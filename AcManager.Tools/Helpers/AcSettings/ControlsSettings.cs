using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.Data;
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
using FirstFloor.ModernUI.Presentation;
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

        private static string CapitalizeFirst(string s) {
            if (s == string.Empty) return string.Empty;
            if (s.Length == 1) return s.ToUpperInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static bool ParseKey(string v, out Keys k) {
            if (string.Equals(v, "ctrl", StringComparison.OrdinalIgnoreCase)) {
                k = Keys.Control;
                return true;
            }
            return Enum.TryParse(v, true, out k);
        }

        internal ControlsSettings() : base("controls", false) {
            SetCanSave(false);
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

            KeyboardPatchButtonEntries = new[] {
                new KeyboardSpecificButtonEntry("KEY_MODIFICATOR", "Forcing modifier", "__EXT_KEYBOARD_GAS_RAW"),
                new KeyboardSpecificButtonEntry("KEY", "Forced throttle", "__EXT_KEYBOARD_GAS_RAW")
            }.ToArray();

            #region Joystick entires
            ControllerCarExtraButtonEntries = new[] {
                new ControllerButtonCombined("KERS", KeyboardButtonProvider, ToolsStrings.Controls_Kers),
                new ControllerButtonCombined("DRS", KeyboardButtonProvider, ToolsStrings.Controls_Drs),
                new ControllerButtonCombined("ACTION_HEADLIGHTS_FLASH", KeyboardButtonProvider, ToolsStrings.Controls_FlashHeadlights),
                new ControllerButtonCombined("ACTION_HORN", KeyboardButtonProvider, ToolsStrings.Controls_Horn),
            };
            ControllerCarBrakeButtonEntries = new[] {
                new ControllerButtonCombined("BALANCEUP", KeyboardButtonProvider, ToolsStrings.Controls_MoveToFront),
                new ControllerButtonCombined("BALANCEDN", KeyboardButtonProvider, ToolsStrings.Controls_MoveToRear),
            };
            ControllerCarTurboButtonEntries = new[] {
                new ControllerButtonCombined("TURBOUP", KeyboardButtonProvider, ToolsStrings.Controls_Increase),
                new ControllerButtonCombined("TURBODN", KeyboardButtonProvider, ToolsStrings.Controls_Decrease),
            };
            ControllerCarTractionControlButtonEntries = new[] {
                new ControllerButtonCombined("TCUP", KeyboardButtonProvider, ToolsStrings.Controls_Increase),
                new ControllerButtonCombined("TCDN", KeyboardButtonProvider, ToolsStrings.Controls_Decrease),
            };
            ControllerCarAbsButtonEntries = new[] {
                new ControllerButtonCombined("ABSUP", KeyboardButtonProvider, ToolsStrings.Controls_Increase),
                new ControllerButtonCombined("ABSDN", KeyboardButtonProvider, ToolsStrings.Controls_Decrease),
            };
            ControllerCarEngineBrakeButtonEntries = new[] {
                new ControllerButtonCombined("ENGINE_BRAKE_UP", KeyboardButtonProvider, ToolsStrings.Controls_Increase),
                new ControllerButtonCombined("ENGINE_BRAKE_DN", KeyboardButtonProvider, ToolsStrings.Controls_Decrease),
            };
            ControllerCarMgukButtonEntries = new[] {
                new ControllerButtonCombined("MGUK_DELIVERY_UP", KeyboardButtonProvider, ToolsStrings.Controls_Mguk_IncreaseDelivery),
                new ControllerButtonCombined("MGUK_DELIVERY_DN", KeyboardButtonProvider, ToolsStrings.Controls_Mguk_DecreaseDelivery),
                new ControllerButtonCombined("MGUK_RECOVERY_UP", KeyboardButtonProvider, ToolsStrings.Controls_Mguk_IncreaseRecovery),
                new ControllerButtonCombined("MGUK_RECOVERY_DN", KeyboardButtonProvider, ToolsStrings.Controls_Mguk_DecreaseRecovery),
                new ControllerButtonCombined("MGUH_MODE", KeyboardButtonProvider, ToolsStrings.Controls_Mguh_Mode)
            };
            ControllerViewButtonEntries = new[] {
                new ControllerButtonCombined("GLANCELEFT", KeyboardButtonProvider, ToolsStrings.Controls_GlanceLeft),
                new ControllerButtonCombined("GLANCERIGHT", KeyboardButtonProvider, ToolsStrings.Controls_GlanceRight),
                new ControllerButtonCombined("GLANCEBACK", KeyboardButtonProvider, ToolsStrings.Controls_GlanceBack),
                new ControllerButtonCombined("ACTION_CHANGE_CAMERA", KeyboardButtonProvider, ToolsStrings.Controls_ChangeCamera)
            };
            ControllerCarButtonEntries = new[] {
                new ControllerButtonCombined("GEARUP", KeyboardButtonProvider, ToolsStrings.Controls_NextGear),
                new ControllerButtonCombined("GEARDN", KeyboardButtonProvider, ToolsStrings.Controls_PreviousGear),
                new ControllerButtonCombined(HandbrakeId, KeyboardButtonProvider, ToolsStrings.Controls_Handbrake),
                new ControllerButtonCombined("ACTION_HEADLIGHTS", KeyboardButtonProvider, ToolsStrings.Controls_Headlights),
            };
            #endregion

            #region Constructing custom patch entries
            var manifest = PatchHelper.GetManifest();
            var sectionsList = new List<CustomButtonEntrySection>();
            for (var i = 0; i < 999; i++) {
                var k = manifest[$@"INPUT_GROUP_{i}"];
                var n = k.GetNonEmpty("GROUP_NAME");
                if (string.IsNullOrWhiteSpace(n)) continue;

                var list = new List<CustomButtonEntryCombined>();
                foreach (var p in k) {
                    if (!p.Key.StartsWith("__") || p.Key.EndsWith("_")) continue;

                    var value = p.Value.WrapQuoted(out var unwrap)
                            .Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                    if (value.Count < 1 || value.Count > 2) continue;

                    var keys = new List<Keys>();
                    var modifiers = new List<Keys>();
                    if (value.Count > 1) {
                        var broken = false;
                        foreach (var v in unwrap(value[1]).Split('+')) {
                            if (ParseKey(v, out Keys key)) {
                                keys.Add(key);
                            } else {
                                broken = true;
                            }
                        }
                        for (var j = 0; j < keys.Count - 1; j++) {
                            if (keys[j].IsInputModifier(out var normalized)) {
                                modifiers.Add(normalized);
                            } else {
                                broken = true;
                            }
                        }
                        if (broken) continue;
                    }

                    string description = null;
                    var name = unwrap(value[0]);
                    var index = name.IndexOf('(');
                    if (index != -1 && name.EndsWith(")")) {
                        description = CapitalizeFirst(name.Substring(index + 1, name.Length - index - 2)).TrimEnd('.');
                        name = name.Substring(0, index).TrimStart();
                    }

                    var flags = k.GetStrings(p.Key + "_FLAGS_");
                    var canBeHeld = flags.ArrayContains("canBeHeld"); // TODO

                    list.Add(new CustomButtonEntryCombined(p.Key, name, description,
                            keys.Count > 0 ? keys.LastOrDefault() : (Keys?)null, modifiers));
                }

                sectionsList.Add(new CustomButtonEntrySection { DisplayName = n, Entries = list.ToArray() });
            }
            CustomButtonEntrySections = sectionsList.ToArray();
            #endregion

            #region Constructing system entries
            SystemRaceButtonEntries = new[] {
                new SystemButtonEntryCombined("__CM_PAUSE", "Pause race", fixedValueCallback: x => new[] { Keys.Escape }),
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
                new SystemButtonEntryCombined("HIDE_DAMAGE", "Toggle damage display", defaultKey: Keys.Q),
                new SystemButtonEntryCombined("SHOW_DAMAGE", "Show damage display", defaultKey: Keys.J),
                new SystemButtonEntryCombined("DRIVER_NAMES", "Driver names", defaultKey: Keys.L),
                new SystemButtonEntryCombined("IDEAL_LINE", "Ideal line", defaultKey: Keys.I),
                new SystemButtonEntryCombined("__CM_RESET_CAMERA_VR", "Reset camera in VR", fixedValueCallback: x => new[] { Keys.Control, Keys.Space }),
            };

            SystemReplayButtonEntries = new[] {
                new SystemButtonEntryCombined("START_REPLAY", "Start replay", defaultKey: Keys.R),
                new SystemButtonEntryCombined("PAUSE_REPLAY", "Pause replay", defaultKey: Keys.Space),
                new SystemButtonEntryCombined("NEXT_LAP", "Next lap", defaultKey: Keys.N),
                new SystemButtonEntryCombined("PREVIOUS_LAP", "Previous lap", defaultKey: Keys.P),
                new SystemButtonEntryCombined("NEXT_CAR", "Next car", defaultKey: Keys.NumPad3),
                new SystemButtonEntryCombined("PLAYER_CAR", "Your car", defaultKey: Keys.NumPad2),
                new SystemButtonEntryCombined("PREVIOUS_CAR", "Previous car", defaultKey: Keys.NumPad1),
                new SystemButtonEntryCombined("SLOWMO", "Slow motion", defaultKey: Keys.S),
                new SystemButtonEntryCombined("FFWD", "Fast-forward", defaultKey: Keys.F),
                new SystemButtonEntryCombined("REV", "Rewind", defaultKey: Keys.D),
            };

            SystemOnlineButtonEntries = new[] {
                new SystemButtonEntryCombined("__CM_ONLINE_POLL_YES", "Poll: vote Yes", fixedValueCallback: x => new[] { Keys.Y }, delayed: true),
                new SystemButtonEntryCombined("__CM_ONLINE_POLL_NO", "Poll: vote No", fixedValueCallback: x => new[] { Keys.N }, delayed: true)
            };

            SystemDiscordButtonEntries = new[] {
                new SystemButtonEntryCombined("__CM_DISCORD_REQUEST_ACCEPT", "Accept join request", fixedValueCallback: x => new[] { Keys.Enter }, delayed: true),
                new SystemButtonEntryCombined("__CM_DISCORD_REQUEST_DENY", "Deny join request", fixedValueCallback: x => new[] { Keys.Back }, delayed: true)
            };

            var systemAbs = new SystemButtonEntryCombined("ABS", "ABS", true, defaultKey: Keys.A);
            var systemTractionControl = new SystemButtonEntryCombined("TRACTION_CONTROL", "Traction control", true, defaultKey: Keys.T);
            var mguk1 = new SystemButtonEntryCombined("__CM_MGU_1", "MGU-K recovery", true, defaultKey: Keys.D1);
            var mguk2 = new SystemButtonEntryCombined("__CM_MGU_2", "MGU-K delivery", true, defaultKey: Keys.D2);
            var engineBrake = new SystemButtonEntryCombined("__CM_ENGINE_BRAKE", "Engine brake", true, defaultKey: Keys.D4);

            SystemCarButtonEntries = new[] {
                new SystemButtonEntryCombined("MOUSE_STEERING", "Mouse steering", defaultKey: Keys.M),
                new SystemButtonEntryCombined("ACTIVATE_AI", "Toggle AI", defaultKey: Keys.C),
                new SystemButtonEntryCombined("AUTO_SHIFTER", "Auto shifter", defaultKey: Keys.G),
                mguk1,
                new SystemButtonEntryCombined("__CM_MGU_1_DECREASE", "MGU-K recovery (decrease)", fixedValueCallback: key => key.HasValue ? new[] {
                    Keys.Control, Keys.Shift, key.Value
                } : null, buttonReference: mguk1.SystemButton),
                mguk2,
                new SystemButtonEntryCombined("__CM_MGU_2_DECREASE", "MGU-K delivery (decrease)", fixedValueCallback: key => key.HasValue ? new[] {
                    Keys.Control, Keys.Shift, key.Value
                } : null, buttonReference: mguk2.SystemButton),
                new SystemButtonEntryCombined("__CM_MGU_3", "MGU-H mode", false, defaultKey: Keys.D3),
                engineBrake,
                new SystemButtonEntryCombined("__CM_ENGINE_BRAKE_DECREASE", "Engine brake (decrease)", fixedValueCallback: key => key.HasValue ? new[] {
                    Keys.Control, Keys.Shift, key.Value
                } : null, buttonReference: engineBrake.SystemButton),
                systemAbs,
                new SystemButtonEntryCombined("__CM_ABS_DECREASE", "ABS (decrease)", fixedValueCallback: key => key.HasValue ? new[] {
                    Keys.Control, Keys.Shift, key.Value
                } : null, buttonReference: systemAbs.SystemButton),
                systemTractionControl,
                new SystemButtonEntryCombined("__CM_TRACTION_CONTROL_DECREASE", "Traction c. (decrease)", fixedValueCallback: key => key.HasValue ? new[] {
                    Keys.Control, Keys.Shift, key.Value
                } : null, buttonReference: systemTractionControl.SystemButton),
                // new SystemButtonEntryCombined("KERS", "KERS", defaultKey: Keys.K),
            };
            #endregion

            AcSettingsHolder.Python.AppActiveStateChanged += OnAppActiveStateChanged;
            AcSettingsHolder.Video.PropertyChanged += OnVideoPropertyChanged;
            RefreshOverlayAvailable();
        }

        private KeyboardButtonEntry KeyboardButtonProvider(string arg) {
            return WheelButtonEntries.Select(x => x.KeyboardButton)
                    .Select(x => (IEntry)x)
                    .Union(WheelButtonEntries.OfType<WheelButtonCombinedAlt>().Select(x => x.WheelButtonAlt))
                    .Union(WheelHShifterButtonEntries)
                    .Union(KeyboardSpecificButtonEntries)
                    .Union(KeyboardPatchButtonEntries)
                    .OfType<KeyboardButtonEntry>().First(x => x.Id == arg);
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
                OnPropertyChanged(false);
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
                    UpdateHardwareLockAvailability();

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
            set {
                if (Equals(value, _isScanningInProgress)) return;
                _isScanningInProgress = value;
                OnPropertyChanged(false);
            }
        }

        private void UpdateHardwareLockAvailability() {
            object options = null;
            IsHardwareLockSupported = Devices.Any(x => {
                if (!WheelSteerLock.IsSupported(x.ProductId, out var o)) return false;
                if (o != null) options = o;
                return true;
            });
            HardwareLockOptions = options;
        }

        private void OnDevicesUpdate(object sender, EventArgs eventArgs) {
            IsScanningInProgress = false;
            var watcher = (DirectInputScanner.Watcher)sender;
            RescanDevices(watcher.Get());

            if (DevicesScan?.ScanTime.TotalSeconds > 1 && !ValuesStorage.Contains("Settings.DriveSettings.ScanControllersAutomatically")) {
                SettingsHolder.Drive.ScanControllersAutomatically = true;
            }

            UpdateHardwareLockAvailability();
            Save();
        }

        private DelegateCommand _runControlPanelCommand;

        public DelegateCommand RunControlPanelCommand
            => _runControlPanelCommand ?? (_runControlPanelCommand = new DelegateCommand(() => Devices.FirstOrDefault()?.RunControlPanel()));

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

        private PlaceholderInputDevice GetPlaceholderDevice([CanBeNull] string instanceId, [CanBeNull] string productId, string displayName, int iniId) {
            var placeholder = _placeholderDevices.FirstOrDefault(y => y.DisplayName == displayName)
                    ?? (instanceId != null ? _placeholderDevices.FirstOrDefault(x => x.InstanceId == instanceId) : null);
            if (placeholder == null) {
                placeholder = new PlaceholderInputDevice(instanceId, productId, displayName, iniId);
                _placeholderDevices.Add(placeholder);
            } else if (!placeholder.OriginalIniIds.Contains(iniId)) {
                placeholder.OriginalIniIds.Add(iniId);
            }

            return placeholder;
        }

        private PlaceholderInputDevice GetPlaceholderDevice(DirectInputDevice device) {
            return GetPlaceholderDevice(device.InstanceId, device.ProductId, device.DisplayName, device.Index);
        }

        private void UpdatePlaceholders() {
            foreach (var source in _placeholderDevices.Where(x =>
                    Entries.OfType<IDirectInputEntry>().All(y => y.Device != x)).ToList()) {
                _placeholderDevices.Remove(source);
            }

            var next = Devices.Count;
            foreach (var placeholder in _placeholderDevices) {
                if (Devices.Any(x => x.Index == placeholder.Index)) {
                    placeholder.Index = next++;
                }
            }
        }

        private void RescanDevices([CanBeNull] IList<Joystick> devices) {
            _skip = true;
            SetCanSave(true);

            try {
                var directInput = DirectInputScanner.DirectInput;
                var checkedPlaceholders = new List<PlaceholderInputDevice>();
                var newDevices = new List<DirectInputDevice>();
                if (devices != null && directInput != null) {
                    for (var i = 0; i < devices.Count; i++) {
                        var device = devices[i];
                        if (device == null) continue;

                        if (device.Information.ProductName.Contains(@"FANATEC CSL Elite")) {
                            if (SettingsHolder.Drive.SameControllersKeepFirst) {
                                newDevices.RemoveAll(y => y.Same(device.Information, i));
                            } else if (newDevices.Any(y => y.Same(device.Information, i))) {
                                continue;
                            }
                        }

                        newDevices.Add(Devices.FirstOrDefault(y => y.Same(device.Information, i)) ?? DirectInputDevice.Create(device, i));
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
                    } else if (current is DirectInputDevice device && !newDevices.Contains(current)) {
                        entry.Input = GetPlaceholderDevice(device).GetAxle(entry.Input.Id);
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
                    } else if (current is DirectInputDevice device && !newDevices.Contains(current)) {
                        GetActualInput(entry, GetPlaceholderDevice(device));
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

                Logging.Debug("New devices:" + newDevices.Select(x => $"\n{x.Index}: {x.DisplayName}, PGUID={x.ProductId}, IGUID={x.InstanceId}")
                        .JoinToString("").Or(" none"));

                Devices.ReplaceEverythingBy(newDevices);
                UpdatePlaceholders();

                foreach (var device in newDevices) {
                    ProductGuids[device.DisplayName] = device.ProductId;

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

                void EnsureIndicesMatch(PlaceholderInputDevice placeholder, DirectInputDevice actualDevice) {
                    if (checkedPlaceholders.Contains(placeholder)) return;
                    checkedPlaceholders.Add(placeholder);

                    if (!placeholder.OriginalIniIds.Contains(actualDevice.Index)) {
                        Logging.Warning(
                                $"{placeholder.DisplayName} index changed: saved as {placeholder.OriginalIniIds.JoinToString("+")}, actual: {actualDevice.Index}");
                        FixMessedOrderAsync().Forget();
                    }
                }

                void GetActualInput(BaseEntry<DirectInputButton> entry, IDirectInputDevice replacement) {
                    if (entry.Input is DirectInputPov pov) {
                        entry.Input = replacement.GetPov(entry.Input.Id, pov.Direction);
                    } else if (entry.Input != null) {
                        entry.Input = replacement.GetButton(entry.Input.Id);
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

                if (waiting is CustomButtonEntry c
                        && provider is KeyboardInputButton k
                        && k.Key.IsInputModifier(out var modifier)) {
                    c.AddModifier(modifier);
                    return;
                }

                if (waiting.Input == provider) {
                    waiting.OnInputArrived();
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

                if (waiting.Layer != EntryLayer.NoIntersection) {
                    var existing = Entries.OfType<BaseEntry<T>>().Where(x => x.Input == provider && waiting.Layer == x.Layer).ToList();
                    if (existing.Any()) {
                        var providerName = new[] {
                            (waiting as WheelButtonEntry)?.ModifierButton?.Input?.DisplayName, provider.DisplayName
                        }.NonNull().JoinToString(@"+");
                        var solution = ConflictResolver == null ? AcControlsConflictSolution.ClearPrevious :
                                await ConflictResolver.Resolve(providerName, existing.Select(x => x.DisplayName));
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
            if (FileUtils.IsAffectedBy(filename, PresetsDirectory) || FileUtils.IsAffectedBy(PresetsDirectory, filename)) {
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
                OnPropertyChanged(false);

                CurrentPresetName = value == null ? null : GetCurrentPresetName(value);
                OnPropertyChanged(false, nameof(CurrentPresetName));
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
            set => Apply(value.Clamp(0, 500), ref _debouncingInterval);
        }

        public WheelAxleEntry SteerAxleEntry { get; } = new WheelAxleEntry("STEER", ToolsStrings.Controls_Steering, false, true);
        public WheelAxleEntry[] WheelAxleEntries { get; }

        private bool _combineWithKeyboardInput;

        public bool CombineWithKeyboardInput {
            get => _combineWithKeyboardInput;
            set => Apply(value, ref _combineWithKeyboardInput);
        }

        public WheelButtonCombinedAlt[] WheelGearsButtonEntries { get; } = {
            new WheelButtonCombinedAlt("GEARUP", ToolsStrings.Controls_NextGear),
            new WheelButtonCombinedAlt("GEARDN", ToolsStrings.Controls_PreviousGear)
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

        /*public WheelButtonCombined[] WheelGesturesButtonEntries { get; } = {
            new WheelButtonCombined("ACTION_CELEBRATE", ToolsStrings.Controls_Celebrate),
            new WheelButtonCombined("ACTION_CLAIM", ToolsStrings.Controls_Complain)
        };*/

        private WheelButtonCombined[] _wheelButtonEntries;

        public IEnumerable<WheelButtonCombined> WheelButtonEntries => _wheelButtonEntries ?? (_wheelButtonEntries = WheelGearsButtonEntries
                .Union(WheelCarButtonEntries)
                .Union(WheelCarBrakeButtonEntries)
                .Union(WheelCarTurboButtonEntries)
                .Union(WheelCarTractionControlButtonEntries)
                .Union(WheelCarAbsButtonEntries)
                .Union(WheelCarEngineBrakeButtonEntries)
                .Union(WheelCarMgukButtonEntries)
                .Union(WheelViewButtonEntries)
                // .Union(WheelGesturesButtonEntries)
                .ToArray());

        [NotNull]
        public IEnumerable<string> WheelButtonKeys => WheelButtonEntries.Select(x => x.WheelButton.Id).NonNull();
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
            set => Apply(value.Clamp(0, 300), ref _wheelFfbGain);
        }

        private int _wheelFfbFilter;

        public int WheelFfbFilter {
            get => _wheelFfbFilter;
            set => Apply(value.Clamp(0, 99), ref _wheelFfbFilter);
        }

        private double _wheelFfbMinForce;

        public double WheelFfbMinForce {
            get => _wheelFfbMinForce;
            set => Apply(value.Clamp(0, 50), ref _wheelFfbMinForce);
        }

        private double _wheelFfbWheelCenterBoostGain;

        public double WheelFfbWheelCenterBoostGain {
            get => _wheelFfbWheelCenterBoostGain;
            set => Apply(value.Clamp(0, 2000).Round(0.1), ref _wheelFfbWheelCenterBoostGain);
        }

        private double _wheelFfbWheelCenterBoostRange;

        public double WheelFfbWheelCenterBoostRange {
            get => _wheelFfbWheelCenterBoostRange;
            set => Apply(value.Clamp(0, 2000).Round(0.1), ref _wheelFfbWheelCenterBoostRange);
        }

        private int _wheelFfbKerbEffect;

        public int WheelFfbKerbEffect {
            get => _wheelFfbKerbEffect;
            set => Apply(value.Clamp(0, 2000), ref _wheelFfbKerbEffect);
        }

        private int _wheelFfbRoadEffect;

        public int WheelFfbRoadEffect {
            get => _wheelFfbRoadEffect;
            set => Apply(value.Clamp(0, 2000), ref _wheelFfbRoadEffect);
        }

        private int _wheelFfbSlipEffect;

        public int WheelFfbSlipEffect {
            get => _wheelFfbSlipEffect;
            set => Apply(value.Clamp(0, 2000), ref _wheelFfbSlipEffect);
        }

        private int _wheelFfbAbsEffect;

        public int WheelFfbAbsEffect {
            get => _wheelFfbAbsEffect;
            set => Apply(value.Clamp(0, 2000), ref _wheelFfbAbsEffect);
        }

        private bool _wheelFfbEnhancedUndersteer;

        public bool WheelFfbEnhancedUndersteer {
            get => _wheelFfbEnhancedUndersteer;
            set => Apply(value, ref _wheelFfbEnhancedUndersteer);
        }

        private int _wheelFfbSkipSteps;

        public int WheelFfbSkipSteps {
            get => _wheelFfbSkipSteps;
            set => Apply(value.Clamp(0, 1000), ref _wheelFfbSkipSteps);
        }
        #endregion

        #region Controller
        public ControllerButtonCombined[] ControllerCarButtonEntries { get; }
        public ControllerButtonCombined[] ControllerCarExtraButtonEntries { get; }
        public ControllerButtonCombined[] ControllerCarBrakeButtonEntries { get; }
        public ControllerButtonCombined[] ControllerCarTurboButtonEntries { get; }
        public ControllerButtonCombined[] ControllerCarTractionControlButtonEntries { get; }
        public ControllerButtonCombined[] ControllerCarAbsButtonEntries { get; }
        public ControllerButtonCombined[] ControllerCarEngineBrakeButtonEntries { get; }
        public ControllerButtonCombined[] ControllerCarMgukButtonEntries { get; }
        public ControllerButtonCombined[] ControllerViewButtonEntries { get; }

        private ControllerButtonCombined[] _controllerButtonEntries;

        public IEnumerable<ControllerButtonCombined> ControllerButtonEntries => _controllerButtonEntries ?? (_controllerButtonEntries
                = ControllerCarButtonEntries
                        .Union(ControllerCarExtraButtonEntries)
                        .Union(ControllerCarBrakeButtonEntries)
                        .Union(ControllerCarTurboButtonEntries)
                        .Union(ControllerCarTractionControlButtonEntries)
                        .Union(ControllerCarAbsButtonEntries)
                        .Union(ControllerCarEngineBrakeButtonEntries)
                        .Union(ControllerCarMgukButtonEntries)
                        .Union(ControllerViewButtonEntries)
                        // .Union(WheelGesturesButtonEntries)
                        .ToArray());
        #endregion

        public static readonly SettingEntry[] ControllerSticks = {
            new SettingEntry("LEFT", "Left thumb"),
            new SettingEntry("RIGHT", "Right thumb"),
        };

        private SettingEntry _controllerSteeringStick = ControllerSticks[0];

        [CanBeNull]
        public SettingEntry ControllerSteeringStick {
            get => _controllerSteeringStick;
            set => Apply(value.EnsureFrom(ControllerSticks), ref _controllerSteeringStick);
        }

        private double _controllerSteeringGamma;

        public double ControllerSteeringGamma {
            get => _controllerSteeringGamma;
            set => Apply(value.Clamp(1.0, 4.0), ref _controllerSteeringGamma);
        }

        private double _controllerSteeringFilter;

        public double ControllerSteeringFilter {
            get => _controllerSteeringFilter;
            set => Apply(value.Clamp(0.0, 1.0), ref _controllerSteeringFilter);
        }

        private double _controllerSpeedSensitivity;

        public double ControllerSpeedSensitivity {
            get => _controllerSpeedSensitivity;
            set => Apply(value.Clamp(0.0, 1.0), ref _controllerSpeedSensitivity);
        }

        private double _controllerSteeringDeadzone;

        public double ControllerSteeringDeadzone {
            get => _controllerSteeringDeadzone;
            set => Apply(value.Clamp(0.0, 0.45), ref _controllerSteeringDeadzone);
        }

        private double _controllerSteeringSpeed;

        public double ControllerSteeringSpeed {
            get => _controllerSteeringSpeed;
            set => Apply(value.Clamp(0.01, 1.0), ref _controllerSteeringSpeed);
        }

        private double _controllerRumbleIntensity;

        public double ControllerRumbleIntensity {
            get => _controllerRumbleIntensity;
            set => Apply(value.Clamp(0.0, 1.0), ref _controllerRumbleIntensity);
        }

        #region Keyboard
        private double _keyboardSteeringSpeed;

        public double KeyboardSteeringSpeed {
            get => _keyboardSteeringSpeed;
            set => Apply(value.Clamp(0.4, 3.0), ref _keyboardSteeringSpeed);
        }

        private double _keyboardOppositeLockSpeed;

        public double KeyboardOppositeLockSpeed {
            get => _keyboardOppositeLockSpeed;
            set => Apply(value.Clamp(1.0, 5.0), ref _keyboardOppositeLockSpeed);
        }

        private double _keyboardReturnRate;

        public double KeyboardReturnRate {
            get => _keyboardReturnRate;
            set => Apply(value.Clamp(1.0, 5.0), ref _keyboardReturnRate);
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
            set => Apply(value.Clamp(0, 1.0), ref _keyboardMouseSteeringSpeed);
        }

        public KeyboardButtonEntry[] KeyboardSpecificButtonEntries { get; }

        private bool _keyboardPatchThrottleOverride;

        public bool KeyboardPatchThrottleOverride {
            get => _keyboardPatchThrottleOverride;
            set => Apply(value, ref _keyboardPatchThrottleOverride);
        }

        private double _keyboardPatchThrottleLagUp;

        public double KeyboardPatchThrottleLagUp {
            get => _keyboardPatchThrottleLagUp;
            set => Apply(value.Saturate(), ref _keyboardPatchThrottleLagUp);
        }

        private double _keyboardPatchThrottleLagDown;

        public double KeyboardPatchThrottleLagDown {
            get => _keyboardPatchThrottleLagDown;
            set => Apply(value.Saturate(), ref _keyboardPatchThrottleLagDown);
        }

        public KeyboardButtonEntry[] KeyboardPatchButtonEntries { get; }
        #endregion

        #region Shaders Patch keys
        public class CustomButtonEntrySection : Displayable {
            public CustomButtonEntryCombined[] Entries { get; set; }
        }

        public CustomButtonEntrySection[] CustomButtonEntrySections { get; }

        [NotNull]
        public IEnumerable<CustomButtonEntryCombined> CustomButtonEntries => CustomButtonEntrySections.SelectMany(x => x.Entries);

        private List<SettingEntry> _customGasRawMouseValues;

        public List<SettingEntry> CustomGasRawMouseValues => _customGasRawMouseValues ?? (_customGasRawMouseValues = new List<SettingEntry> {
            new SettingEntry(0, "Disabled"),
            new SettingEntry(1, "Left mouse button"),
            new SettingEntry(2, "Right mouse button"),
            new SettingEntry(3, "Middle mouse button"),
            new SettingEntry(4, "Fourth mouse button"),
            new SettingEntry(5, "Fifth mouse button"),
        });

        private SettingEntry _customGasRawMouseModifier;

        public SettingEntry CustomGasRawMouseModifier {
            get => _customGasRawMouseModifier;
            set => Apply(value.EnsureFrom(CustomGasRawMouseValues), ref _customGasRawMouseModifier);
        }

        private SettingEntry _customGasRawMouse;

        public SettingEntry CustomGasRawMouse {
            get => _customGasRawMouse;
            set => Apply(value.EnsureFrom(CustomGasRawMouseValues), ref _customGasRawMouse);
        }
        #endregion

        #region System shortcuts (Ctrl+…)
        public SystemButtonEntryCombined[] SystemRaceButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemCarButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemUiButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemReplayButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemOnlineButtonEntries { get; }
        public SystemButtonEntryCombined[] SystemDiscordButtonEntries { get; }

        [NotNull]
        public IEnumerable<SystemButtonEntryCombined> SystemButtonEntries => SystemRaceButtonEntries
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
            private set {
                if (Equals(value, _isOverlayAvailable)) return;
                _isOverlayAvailable = value;
                OnPropertyChanged(false);
            }
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
            set {
                if (Equals(value, _isHardwareLockSupported)) return;
                _isHardwareLockSupported = value;
                OnPropertyChanged(false);
            }
        }

        private object _hardwareLockOptions;

        public object HardwareLockOptions {
            get => _hardwareLockOptions;
            set {
                if (Equals(value, _hardwareLockOptions)) return;
                _hardwareLockOptions = value;
                OnPropertyChanged(false);
            }
        }

        public string DisplayHardwareLockSupported { get; } = WheelSteerLock.GetSupportedNames().OrderBy(x => x).JoinToReadableString();
        #endregion

        #region Main
        private const string HandbrakeId = "HANDBRAKE";

        [NotNull]
        private IEnumerable<IEntry> Entries => WheelAxleEntries
                .Select(x => (IEntry)x)
                .Union(WheelButtonEntries.Select(x => x.KeyboardButton))
                .Union(WheelButtonEntries.Select(x => x.WheelButton))
                .Union(WheelButtonEntries.OfType<WheelButtonCombinedAlt>().Select(x => x.WheelButtonAlt))
                .Union(WheelHShifterButtonEntries)
                .Union(ControllerButtonEntries.Select(x => x.KeyboardButton))
                .Union(ControllerButtonEntries.Select(x => x.ControllerButton))
                .Union(KeyboardSpecificButtonEntries)
                .Union(KeyboardPatchButtonEntries)
                .Union(SystemButtonEntries.Select(x => x.SystemButton).NonNull())
                .Union(SystemButtonEntries.Select(x => x.WheelButton))
                .Union(SystemButtonEntries.Select(x => x.WheelButtonModifier))
                .Union(CustomButtonEntries.Select(x => x.Button).NonNull())
                .Union(CustomButtonEntries.Select(x => x.WheelButton))
                .Union(CustomButtonEntries.Select(x => x.WheelButtonModifier));

        private bool _skip;

        private void EntryPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (_skip) return;

            switch (e.PropertyName) {
                case nameof(BaseEntry<IInputProvider>.Input):
                    var senderEntry = sender as IDirectInputEntry;
                    if (senderEntry?.Id == HandbrakeId) {
                        try {
                            _skip = true;
                            foreach (var entry in Entries.OfType<IDirectInputEntry>().Where(x => x.Id == HandbrakeId
                                    && x.Device?.Same(senderEntry.Device) != true
                                    && !(x is ControllerButtonEntry))) {
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

            Logging.Debug("Devices:" + Devices.Select(x => $"\n{x.Index}: {x.DisplayName}, PGUID={x.ProductId}, IGUID={x.InstanceId}")
                    .JoinToString("").Or(" none"));

            var orderChanged = false;
            var devices = Ini["CONTROLLERS"].Keys.Where(x => x.StartsWith(@"CON")).Select(x => new {
                IniId = FlexibleParser.TryParseInt(x.Substring(3)),
                Name = Ini["CONTROLLERS"].GetNonEmpty(x)
            }).TakeWhile(x => x.Name != null && x.IniId.HasValue).Select(x => {
                // Would never happen, but just in case, to get rid of warnings
                var iniId = x.IniId;
                if (!iniId.HasValue) return null;

                var instanceId = Ini["CONTROLLERS"].GetNonEmpty($"__IGUID{x.IniId}");
                var productId = Ini["CONTROLLERS"].GetNonEmpty($"PGUID{x.IniId}");

                // Filter out fake placeholder just in case
                if (productId == @"0") return null;

                if (productId != null) {
                    ProductGuids[x.Name] = productId;
                }

                var device = instanceId != null ? Devices.FirstOrDefault(y => y.InstanceId == instanceId)
                        : Devices.FirstOrDefault(y => y.Device.InstanceName == x.Name)
                                ?? Devices.FirstOrDefault(y => y.ProductId == productId);
                if (device != null) {
                    Logging.Debug($"{device.DisplayName}: actual index={device.Index}, config index={x.IniId}");
                    if (device.Index != x.IniId) {
                        orderChanged = true;
                    }

                    device.OriginalIniIds.Add(iniId.Value);
                    return device;
                }

                Logging.Debug($"Device {x.Name} is missing, using placeholder (index: {iniId.Value})");
                return (IDirectInputDevice)GetPlaceholderDevice(instanceId, productId, x.Name, iniId.Value);
            }).NonNull().ToList();

            if (devices.All(x => !x.IsController)) {
                devices.Add((IDirectInputDevice)Devices.FirstOrDefault(x => x.IsController)
                        ?? GetPlaceholderDevice(null, @"0", @"Controller (Placeholder)", -1));
            }

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

            var section = Ini["KEYBOARD"];
            KeyboardSteeringSpeed = section.GetDouble("STEERING_SPEED", 1.75);
            KeyboardOppositeLockSpeed = section.GetDouble("STEERING_OPPOSITE_DIRECTION_SPEED", 2.5);
            KeyboardReturnRate = section.GetDouble("STEER_RESET_SPEED", 1.8);
            KeyboardMouseSteering = section.GetBool("MOUSE_STEER", false);
            KeyboardMouseButtons = section.GetBool("MOUSE_ACCELERATOR_BRAKE", false);
            KeyboardMouseSteeringSpeed = section.GetDouble("MOUSE_SPEED", 0.1);

            section = Ini["__EXT_KEYBOARD_GAS_RAW"];
            KeyboardPatchThrottleOverride = section.GetBool("OVERRIDE", false);
            KeyboardPatchThrottleLagUp = section.GetDouble("LAG_UP", 0.5);
            KeyboardPatchThrottleLagDown = section.GetDouble("LAG_DOWN", 0.2);
            CustomGasRawMouse = CustomGasRawMouseValues.GetByIdOrDefault(section.GetInt("MOUSE", 0));
            CustomGasRawMouseModifier = CustomGasRawMouseValues.GetByIdOrDefault(section.GetInt("MOUSE_MODIFICATOR", 0));

            section = Ini["X360"];
            ControllerSteeringStick = ControllerSticks.GetByIdOrDefault(section.GetNonEmpty("STEER_THUMB", ControllerSticks[0].Id));
            ControllerSteeringGamma = section.GetDouble("STEER_GAMMA", 2.0);
            ControllerSteeringFilter = section.GetDouble("STEER_FILTER", 0.7);
            ControllerSpeedSensitivity = section.GetDouble("SPEED_SENSITIVITY", 0.2);
            ControllerSteeringDeadzone = section.GetDouble("STEER_DEADZONE", 0.0);
            ControllerSteeringSpeed = section.GetDouble("STEER_SPEED", 0.2);
            ControllerRumbleIntensity = section.GetDouble("RUMBLE_INTENSITY", 0.8);

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
                        ["NAME"] = device.Device.InstanceName,
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
                    .Union(_placeholderDevices.Where(x => x.ProductId != "0"))
                    .Aggregate(new IniFileSection(null), (s, d, i) => {
                        s.Set("CON" + d.Index, d.DisplayName);
                        s.SetOrRemove("__IGUID" + d.Index, d.InstanceId);
                        s.Set("PGUID" + d.Index, d.ProductId ?? ProductGuids.GetValueOrDefault(d.DisplayName));
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

            section = Ini["__EXT_KEYBOARD_GAS_RAW"];
            section.Set("OVERRIDE", KeyboardPatchThrottleOverride);
            section.Set("LAG_UP", KeyboardPatchThrottleLagUp);
            section.Set("LAG_DOWN", KeyboardPatchThrottleLagDown);
            section.Set("MOUSE", CustomGasRawMouse?.Id);
            section.Set("MOUSE_MODIFICATOR", CustomGasRawMouseModifier?.Id);

            section = Ini["X360"];
            section.Set("STEER_THUMB", ControllerSteeringStick?.Id ?? ControllerSticks[0].Id);
            section.Set("STEER_GAMMA", ControllerSteeringGamma);
            section.Set("STEER_FILTER", ControllerSteeringFilter);
            section.Set("SPEED_SENSITIVITY", ControllerSpeedSensitivity);
            section.Set("STEER_DEADZONE", ControllerSteeringDeadzone);
            section.Set("STEER_SPEED", ControllerSteeringSpeed);
            section.Set("RUMBLE_INTENSITY", ControllerRumbleIntensity);

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

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void FixControllersOrder() {
            if (Devices.Count == 0) {
                Logging.Warning("Devices are not yet scanned, scanning…");
                var fixTimeout = TimeSpan.FromSeconds(2);
                try {
                    using (var timeout = new CancellationTokenSource(fixTimeout)) {
                        var list = DirectInputScanner.GetAsync(timeout.Token).Result;
                        Logging.Write("Scanned result: " + (list == null ? @"failed to scan" : $@"{list.Count} device(s)"));
                        if (list == null) return;

                        RescanDevices(list);
                    }
                } catch (Exception e) when (e.IsCancelled()) {
                    Logging.Warning("Failed to scan the devices in given time");
                }
            }

            if (_fixingMessedUpOrder) {
                Logging.Write("Controllers order is fixed");
                SaveImmediately();
            }
        }
    }
}