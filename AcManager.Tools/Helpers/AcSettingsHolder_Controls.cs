using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using AcManager.Tools.Helpers.AcSettingsControls;
using AcManager.Tools.Helpers.DirectInput;
using AcManager.Tools.Lists;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using SlimDX.DirectInput;
using MenuItem = System.Windows.Controls.MenuItem;

namespace AcManager.Tools.Helpers {
    public partial class AcSettingsHolder {
        public class ControlsSettings : IniSettings, IDisposable {
            internal ControlsSettings() : base("controls", false) {
                KeyboardButtonEntries = WheelButtonEntries.Select(x => x.KeyboardButton).ToArray();

                _directInput = new SlimDX.DirectInput.DirectInput();
                _keyboardInput = new Dictionary<int, KeyboardInputButton>();
                _presetsDirectory = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "controllers");
                ReloadPresets();

                _timer = new DispatcherTimer {
                    Interval = TimeSpan.FromMilliseconds(20),
                    IsEnabled = true
                };

                _timer.Tick += OnTick;
                Reload();
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
                    _used = value;
                    OnPropertyChanged(false);
                    UpdateInputs();
                }
            }

            private void UpdateInputs() {
                if (Used == 0) return;

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

            private void RescanDevices() {
                Devices.DisposeEverything();
                Devices.ReplaceEverythingBy(_directInput.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly)
                                                        .Select(x => new DirectInputDevice(_directInput, x)));
            }

            public BetterObservableCollection<DirectInputDevice> Devices { get; } = new BetterObservableCollection<DirectInputDevice>();

            [CanBeNull]
            public KeyboardInputButton GetKeyboardInputButton(int keyCode) {
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
            private string _presetsDirectory;

            protected override void OnRenamed(object sender, RenamedEventArgs e) {
                if (FileUtils.IsAffected(e.OldFullPath, _presetsDirectory) || FileUtils.IsAffected(e.FullPath, _presetsDirectory)) {
                    ReloadPresetsLater();
                }

                base.OnRenamed(sender, e);
            }

            protected override void OnChanged(object sender, FileSystemEventArgs e) {
                if (FileUtils.IsAffected(e.FullPath, _presetsDirectory)) {
                    ReloadPresetsLater();
                }

                base.OnChanged(sender, e);
            }

            private bool _reloading;
            private bool _loading;
            private bool _saving;
            private DateTime _lastSaved;

            private IEnumerable<SettingEntry> GetUserPresets() {
                return Directory.GetFiles(Path.Combine(_presetsDirectory, "savedsetups"), "*.ini").Select(x => new SettingEntry(x, Path.GetFileNameWithoutExtension(x)));
            }

            private IEnumerable<SettingEntry> GetBuiltInPresets() {
                return Directory.GetFiles(Path.Combine(_presetsDirectory, "presets"), "*.ini").Select(x => new SettingEntry(x, Path.GetFileNameWithoutExtension(x)));
            }

            private MenuItem CreateMenuItem(SettingEntry x) {
                var result = new MenuItem {
                    Header = x.DisplayName,
                    Tag = x
                };

                result.Click += MenuItemClick;
                return result;
            }

            private void MenuItemClick(object sender, RoutedEventArgs e) {
                var entry = ((MenuItem)sender).Tag as SettingEntry;
                if (entry == null ||
                        ModernDialog.ShowMessage($"Load {entry.DisplayName}? Current values will be replaced (but later could be restored from Recycle Bin).",
                                "Are you sure?", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }

                FileUtils.Recycle(Filename);
                File.Copy(entry.Value, Filename);
            }

            private void ReloadPresets() {
                var userPresets = new MenuItem { Header = "User Presets" };
                foreach (var x in GetUserPresets()) {
                    userPresets.Items.Add(CreateMenuItem(x));
                }

                var builtInPresets = new MenuItem { Header = "Built-in Presets" };
                foreach (var x in GetBuiltInPresets()) {
                    builtInPresets.Items.Add(CreateMenuItem(x));
                }

                Presets.ReplaceEverythingBy(new[] {
                    builtInPresets, userPresets
                });
            }

            private async void ReloadPresetsLater() {
                if (_reloading || _saving || DateTime.Now - _lastSaved < TimeSpan.FromSeconds(1)) return;

                _reloading = true;
                await Task.Delay(200);

                try {
                    ReloadPresets();
                } finally {
                    _reloading = false;
                }
            }

            public BetterObservableCollection<MenuItem> Presets { get; } = new BetterObservableCollection<MenuItem>();

            private SettingEntry _currentPreset;

            public SettingEntry CurrentPreset {
                get { return _currentPreset; }
                set {
                    if (Equals(value, _currentPreset)) return;
                    _currentPreset = value;
                    OnPropertyChanged();
                }
            }

            private bool _currentPresetChanged = true;

            public bool CurrentPresetChanged {
                get { return _currentPresetChanged; }
                set {
                    if (Equals(value, _currentPresetChanged)) return;
                    _currentPresetChanged = value;
                    OnPropertyChanged();
                }
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
                    if (Equals(value, _inputMethod)) return;
                    _inputMethod = value;
                    OnPropertyChanged();
                }
            }
            #endregion

            #region Wheel
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

            public class WheelButtonCombined {
                public WheelButtonCombined(string id, string displayName) {
                    WheelButton = new WheelButtonEntry(id, displayName);
                    KeyboardButton = new KeyboardButtonEntry(id, displayName);
                }

                public WheelButtonEntry WheelButton { get; }

                public KeyboardButtonEntry KeyboardButton { get; }
            }

            public WheelButtonCombined[] WheelButtonGearsEntries { get; } = {
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

            public WheelHShifterButtonEntry[] WheelButtonHShifterEntries { get; } = {
                new WheelHShifterButtonEntry("GEAR_1", "1"),
                new WheelHShifterButtonEntry("GEAR_2", "2"),
                new WheelHShifterButtonEntry("GEAR_3", "3"),
                new WheelHShifterButtonEntry("GEAR_4", "4"),
                new WheelHShifterButtonEntry("GEAR_5", "5"),
                new WheelHShifterButtonEntry("GEAR_6", "6"),
                new WheelHShifterButtonEntry("GEAR_7", "7"),
                new WheelHShifterButtonEntry("GEAR_R", "R")
            };

            public WheelButtonCombined[] WheelButtonCarEntries { get; } = {
                new WheelButtonCombined("BALANCEUP", "Brake balance to front"),
                new WheelButtonCombined("BALANCEDN", "Brake balance to rear"),
                new WheelButtonCombined("KERS", "KERS activation"),
                new WheelButtonCombined("DRS", "DRS activation"),
                new WheelButtonCombined("HANDBRAKE", "Handbrake"),
                new WheelButtonCombined("ACTION_HEADLIGHTS", "Headlights"),
                new WheelButtonCombined("ACTION_HORN", "Horn")
            };

            public WheelButtonCombined[] WheelButtonViewEntries { get; } = {
                new WheelButtonCombined("GLANCELEFT", "Glance left"),
                new WheelButtonCombined("GLANCERIGHT", "Glance right"),
                new WheelButtonCombined("GLANCEBACK", "Glance back"),
                new WheelButtonCombined("ACTION_CHANGE_CAMERA", "Change camera")
            };

            public WheelButtonCombined[] WheelButtonGesturesEntries { get; } = {
                new WheelButtonCombined("ACTION_CELEBRATE", "Celebrate"),
                new WheelButtonCombined("ACTION_CLAIM", "Complain")
            };

            private IEnumerable<WheelButtonCombined> WheelButtonEntries
                => WheelButtonGearsEntries
                        .Union(WheelButtonCarEntries)
                        .Union(WheelButtonViewEntries)
                        .Union(WheelButtonGesturesEntries);
            #endregion

            #region Keyboard
            public KeyboardButtonEntry[] KeyboardButtonEntries { get; }
            #endregion

            private IEnumerable<IEntry> Entries
                => WheelAxleEntries
                        .Select(x => (IEntry)x)
                        .Union(WheelButtonEntries.Select(x => x.KeyboardButton))
                        .Union(WheelButtonEntries.Select(x => x.WheelButton))
                        .Union(WheelButtonHShifterEntries);

            private RelayCommand _toggleWaitingCommand;

            public RelayCommand ToggleWaitingCommand => _toggleWaitingCommand ?? (_toggleWaitingCommand = new RelayCommand(o => {
                var b = o as IEntry;
                if (b == null) return;

                foreach (var entry in Entries.ApartFrom(b)) {
                    entry.WaitingFor = WaitingFor.None;
                }

                b.WaitingFor = b.WaitingFor != WaitingFor.Wheel ? WaitingFor.Wheel : WaitingFor.None;
            }, o => o is IEntry));

            private RelayCommand _toggleWaitingKeyboardCommand;

            public RelayCommand ToggleWaitingKeyboardCommand => _toggleWaitingKeyboardCommand ?? (_toggleWaitingKeyboardCommand = new RelayCommand(o => {
                var b = o as BaseEntry;
                if (b == null) return;

                foreach (var entry in Entries.ApartFrom(b)) {
                    entry.WaitingFor = WaitingFor.None;
                }

                b.WaitingFor = b.WaitingFor != WaitingFor.Keyboard ? WaitingFor.Keyboard : WaitingFor.None;
            }, o => o is BaseEntry));

            public BaseEntry GetWaiting() {
                return Entries.FirstOrDefault(x => x.Waiting);
            }

            public BaseEntry GetWaiting(WaitingFor t) {
                return Entries.FirstOrDefault(x => x.WaitingFor == t);
            }

            public void StopWaiting() {
                foreach (var entry in Entries) {
                    entry.WaitingFor = WaitingFor.None;
                }
            }

            protected override void LoadFromIni() {
                if (Devices.Count == 0) {
                    RescanDevices();
                }

                InputMethod = Ini["HEADER"].GetEntry("INPUT_METHOD", InputMethods);

                var section = Ini["CONTROLLERS"];
                var devices = LinqExtension.RangeFrom().Select(x => section.Get($"PGUID{x}")).TakeWhile(x => x != null).Select(x => {
                    var device = Devices.GetByIdOrDefault(x);
                    if (device == null) {
                        Logging.Warning("DEVICE NOT FOUND: " + x);
                    }

                    return device;
                }).ToList();

                foreach (var entry in WheelAxleEntries) {
                    section = Ini[entry.Id];

                    var device = devices.ElementAtOrDefault(section.GetInt("JOY", -1));
                    var axle = device?.Axles.ElementAtOrDefault(section.GetInt("AXLE", -1));
                    entry.LoadFromIni(Ini, axle);
                }

                CombineWithKeyboardInput = Ini["ADVANCED"].GetBool("COMBINE_WITH_KEYBOARD_CONTROL", false);

                foreach (var entry in WheelButtonEntries) {
                    section = Ini[entry.Id];

                    var device = devices.ElementAtOrDefault(section.GetInt("JOY", -1));
                    var button = device?.Buttons.ElementAtOrDefault(section.GetInt("BUTTON", -1));
                    var keyCode = section.GetInt("KEY", -1);
                    entry.LoadFromIni(Ini, button, GetKeyboardInputButton(keyCode));
                }

                var hShifter = Ini["SHIFTER"];
                WheelUseHShifter = hShifter.GetBool("ACTIVE", false);
            }

            protected override void SetToIni() {
                Ini["HEADER"].Set("INPUT_METHOD", InputMethod);
            }
        }

        public static ControlsSettings Controls = new ControlsSettings();
    }
}
