using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using SlimDX.DirectInput;
using Key = System.Windows.Input.Key;
using MenuItem = System.Windows.Controls.MenuItem;

namespace AcManager.Tools.Helpers {
    public enum AcControlsConflictSolution {
        Cancel,
        KeepEverything,
        Flip,
        ClearPrevious
    }

    public interface IAcControlsConflictResolver {
        AcControlsConflictSolution Resolve(string inputDisplayName, IEnumerable<string> existingAssignments);
    }

    public partial class AcSettingsHolder {
        public class ControlsSettings : IniSettings, IDisposable {
            public IAcControlsConflictResolver ConflictResolver { get; set; }

            internal ControlsSettings() : base("controls", false) {
                try {
                    KeyboardButtonEntries = WheelButtonEntries.Select(x => x.KeyboardButton).ToArray();

                    _directInput = new SlimDX.DirectInput.DirectInput();
                    _keyboardInput = new Dictionary<int, KeyboardInputButton>();
                    _presetsDirectory = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "controllers");
                    ReloadPresets();

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
                    _used = value;
                    OnPropertyChanged(false);
                    UpdateInputs();
                }
            }

            public event EventHandler<DirectInputAxleEventArgs> AxleEventHandler;
            public event EventHandler<DirectInputButtonEventArgs> ButtonEventHandler;

            private void UpdateInputs() {
                if (Used == 0 && AxleEventHandler == null && ButtonEventHandler == null) return;

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
                                                        .Select((x, i) => new DirectInputDevice(_directInput, x, i)));

                foreach (var device in Devices) {
                    foreach (var button in device.Buttons) {
                        button.PropertyChanged += DeviceButtonEventHandler;
                    }

                    foreach (var axle in device.Axles) {
                        axle.PropertyChanged += DeviceAxleEventHandler;
                    }
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
            private readonly string _presetsDirectory;

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

            private DirectInputDevice _wheelHShifterDevice;

            public DirectInputDevice WheelHShifterDevice {
                get { return _wheelHShifterDevice; }
                set {
                    if (Equals(value, _wheelHShifterDevice)) return;
                    _wheelHShifterDevice = value;
                    OnPropertyChanged();
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
                new WheelButtonCombined("BALANCEUP", "Brake balance to front"),
                new WheelButtonCombined("BALANCEDN", "Brake balance to rear"),
                new WheelButtonCombined("KERS", "KERS activation"),
                new WheelButtonCombined("DRS", "DRS activation"),
                new WheelButtonCombined("HANDBRAKE", "Handbrake"),
                new WheelButtonCombined("ACTION_HEADLIGHTS", "Headlights"),
                new WheelButtonCombined("ACTION_HORN", "Horn")
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

            private IEnumerable<WheelButtonCombined> WheelButtonEntries
                => WheelGearsButtonEntries
                        .Union(WheelCarButtonEntries)
                        .Union(WheelViewButtonEntries)
                        .Union(WheelGesturesButtonEntries);
            #endregion

            #region Keyboard
            public KeyboardButtonEntry[] KeyboardButtonEntries { get; }
            #endregion

            private IEnumerable<IEntry> Entries
                => WheelAxleEntries
                        .Select(x => (IEntry)x)
                        .Union(WheelButtonEntries.Select(x => x.KeyboardButton))
                        .Union(WheelButtonEntries.Select(x => x.WheelButton))
                        .Union(WheelHShifterButtonEntries);

            private void EntryPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(WheelHShifterButtonEntry.Input) && sender is WheelHShifterButtonEntry) {
                    UpdateWheelHShifterDevice();
                }

                Save();
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

            protected override void LoadFromIni() {
                if (Devices.Count == 0) {
                    RescanDevices();
                }

                InputMethod = Ini["HEADER"].GetEntry("INPUT_METHOD", InputMethods);
                CombineWithKeyboardInput = Ini["ADVANCED"].GetBool("COMBINE_WITH_KEYBOARD_CONTROL", false);
                WheelUseHShifter = Ini["SHIFTER"].GetBool("ACTIVE", false);

                var section = Ini["CONTROLLERS"];
                var devices = LinqExtension.RangeFrom().Select(x => section.Get($"PGUID{x}")).TakeWhile(x => x != null).Select(x => {
                    var device = Devices.GetByIdOrDefault(x);
                    if (device == null) {
                        Logging.Warning("DEVICE NOT FOUND: " + x);
                    }

                    return device;
                }).ToList();

                foreach (var entry in Entries) {
                    entry.Load(Ini, devices);
                }
            }

            protected override void SetToIni() {
                Ini["HEADER"].Set("INPUT_METHOD", InputMethod);
                Ini["ADVANCED"].Set("COMBINE_WITH_KEYBOARD_CONTROL", CombineWithKeyboardInput);
                Ini["SHIFTER"].Set("ACTIVE", WheelUseHShifter);
                Ini["SHIFTER"].Set("JOY", WheelHShifterButtonEntries.FirstOrDefault(x => x.Input != null)?.Input?.Device.IniId);

                Ini["CONTROLLERS"] = Devices.Aggregate(new IniFileSection(), (s, d, i) => {
                    s.Set("CON" + i, d.DisplayName);
                    s.Set("PGUID" + i, d.Id);
                    return s;
                });

                foreach (var entry in Entries) {
                    entry.Save(Ini);
                }
            }
        }

        public static ControlsSettings Controls = new ControlsSettings();

        static AcSettingsHolder() {
            Controls.FinishInitialization();
        }
    }
}
