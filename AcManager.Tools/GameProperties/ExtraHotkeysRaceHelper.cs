using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Internal;
using AcManager.Tools.GameProperties.InGameApp;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.DirectInput;
using AcManager.Tools.SharedMemory;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using AcTools.Windows.Input;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.DirectInput;
using Keyboard = System.Windows.Input.Keyboard;
using Keys = System.Windows.Forms.Keys;
using AcCommand = AcManager.Internal.InternalUtils.AcControlPointCommand;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using Cursor = System.Windows.Forms.Cursor;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyboardEventArgs = AcTools.Windows.Input.KeyboardEventArgs;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace AcManager.Tools.GameProperties {
    public class ExtraHotkeysRaceHelper : Game.GameHandler {
        public static TimeSpan OptionSmallInterval = TimeSpan.FromMilliseconds(150);
        public static TimeSpan OptionLargeInterval = TimeSpan.FromMilliseconds(450);

        public abstract class JoyCommandBase {
            public bool ShowDelay { get; set; } = true;
            public ModifierKeys Modifiers { get; set; } = ModifierKeys.Control;
            public TimeSpan MinInterval { get; set; } = OptionLargeInterval;

            [CanBeNull]
            public Func<bool> IsAvailableTest { get; set; }

            private string _delayedName;

            public string DelayedName {
                get => _delayedName;
                set {
                    if (Equals(value, _delayedName)) return;
                    _delayedName = value;
                    _applied = true;
                    _lastApplied = Stopwatch.StartNew();
                }
            }

            private Stopwatch _lastApplied;
            private bool _isPressed, _applied;

            private void SetPressed(bool isPressed) {
                if (_isPressed == isPressed) return;

                var wasPressed = _isPressed;
                _isPressed = isPressed;

                if (DelayedName != null) {
                    if (!wasPressed) {
                        ShowHoldDialog();
                        _lastApplied = Stopwatch.StartNew();
                        _applied = false;
                    } else if (!_applied) {
                        if (_lastApplied.Elapsed > MinInterval) {
                            ApplySafe();
                            HideHoldDialogSmoothly();
                        } else {
                            HideHoldDialog();
                        }
                    }
                }
            }

            private bool _joyPressed, _keyboardPressed;

            public void SetJoyPressed(bool isPressed) {
                _joyPressed = isPressed;
                SetPressed(_joyPressed || _keyboardPressed);
            }

            public void SetKeyboardPressed(bool isPressed) {
                _keyboardPressed = isPressed;
                SetPressed(_joyPressed || _keyboardPressed);
            }

            public double Progress => _lastApplied?.Elapsed.TotalSeconds / MinInterval.TotalSeconds ?? 0d;
            public bool IsDelayed => DelayedName != null && _isPressed && !_applied;

            public void Update() {
                if (DelayedName == null) {
                    if (_isPressed && !(_lastApplied?.Elapsed < MinInterval)) {
                        ApplySafe();
                        (_lastApplied = _lastApplied ?? Stopwatch.StartNew()).Restart();
                    }
                } else if (_isPressed && !_applied && _lastApplied?.Elapsed > MinInterval) {
                    ApplySafe();
                    HideHoldDialogSmoothly();
                }
            }

            private void ApplySafe() {
                _applied = true;
                try {
                    Apply();
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            public virtual void ConsiderControlsIni(IniFile iniFile) { }

            protected abstract void Apply();

            #region UI
            [CanBeNull]
            private DpiAwareWindow _dialog;

            [CanBeNull]
            private ProgressBar _progressBar;

            [CanBeNull]
            private TextBlock _textBlock;

            private void ShowHoldDialog() {
                if (!ShowDelay) return;

                Application.Current.Dispatcher.InvokeAsync(() => {
                    _textBlock = new TextBlock {
                        Text = $"Hold to {DelayedName.ToSentenceMember()}…",
                        Style = (Style)Application.Current.Resources["Title"],
                        Foreground = new SolidColorBrush(Colors.White),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontWeight = FontWeights.Light,
                        FontSize = 16
                    };

                    _progressBar = new ProgressBar {
                        Background = new SolidColorBrush(Colors.Black),
                        Foreground = new SolidColorBrush(AppearanceManager.Instance.AccentColor),
                        Maximum = 1d,
                        Opacity = 0.4
                    };

                    RenderOptions.SetClearTypeHint(_textBlock, ClearTypeHint.Auto);
                    TextOptions.SetTextHintingMode(_textBlock, TextHintingMode.Animated);
                    TextOptions.SetTextRenderingMode(_textBlock, TextRenderingMode.Grayscale);
                    TextOptions.SetTextFormattingMode(_textBlock, TextFormattingMode.Display);

                    var dialog = new ModernDialog {
                        MinWidth = 300,
                        MinHeight = 40,
                        Width = 300,
                        Height = 40,
                        MaxWidth = 300,
                        MaxHeight = 40,
                        ConsiderPreferredFullscreen = false,
                        SizeToContent = SizeToContent.Manual,
                        WindowStyle = WindowStyle.None,
                        BlurBackground = true,
                        PreventActivation = true,
                        ShowActivated = false,
                        Topmost = true,
                        ShowTopBlob = false,
                        ShowTitle = false,
                        Owner = null,
                        BorderThickness = new Thickness(0),
                        Background = new SolidColorBrush(Colors.Transparent),
                        Buttons = new Control[0],
                        Padding = new Thickness(),
                        Content = new Cell {
                            Width = 300,
                            Height = 40,
                            Children = { _progressBar, _textBlock }
                        },
                        ContentMargin = new Thickness(0, 0, 0, -24),
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        OpacityMask = new VisualBrush {
                            Stretch = Stretch.None,
                            Visual = new Border {
                                Background = new SolidColorBrush(Colors.Black),
                                Width = 300,
                                Height = 40,
                                CornerRadius = new CornerRadius(4)
                            }
                        }
                    };

                    dialog.Loaded += OnDialogLoaded;
                    dialog.Show();

                    CompositionTargetEx.Rendering -= OnRendering;
                    CompositionTargetEx.Rendering += OnRendering;

                    if (_dialog != null) {
                        if (_dialog.IsLoaded) {
                            _dialog.Close();
                        } else {
                            _dialog.Loaded += (s, e) => ((ModernDialog)s).Close();
                        }
                    }

                    _dialog = dialog;
                });
            }

            private void OnRendering(object sender, RenderingEventArgs renderingEventArgs) {
                if (_progressBar == null) return;
                _progressBar.Value = Progress;
            }

            private static Point GetWindowPosition(int width, int height) {
                var screen = Screen.FromPoint(Cursor.Position);
                var bounds = screen.Bounds;
                var x = bounds.Left + (bounds.Width - width) / 2;
                var y = bounds.Bottom - height - 60;
                return new Point(x, y);
            }

            private void OnDialogLoaded(object sender, RoutedEventArgs routedEventArgs) {
                var dialog = (DpiAwareWindow)sender;
                if (!_isPressed) {
                    try {
                        dialog.Close();
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                    return;
                }

                var point = GetWindowPosition(dialog.ActualWidth.RoundToInt(), dialog.ActualHeight.RoundToInt());
                dialog.Left = point.X;
                dialog.Top = point.Y;
            }

            private void HideHoldDialogSmoothly() {
                var dialog = _dialog;
                var progress = _progressBar;
                var text = _textBlock;
                if (dialog == null || progress == null || text == null) {
                    HideHoldDialog();
                    return;
                }

                _dialog = null;
                _progressBar = null;
                _textBlock = null;
                CompositionTargetEx.Rendering -= OnRendering;

                Application.Current.Dispatcher.InvokeAsync(async () => {
                    var d = new Duration(TimeSpan.FromSeconds(0.12));
                    progress.Value = 1d;
                    progress.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((Color)Application.Current.Resources["GoColor"], d));
                    progress.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1d, d));
                    TextBlock.GetForeground(text).BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Black, d));
                    await Task.Delay(500);
                    try {
                        dialog.Close();
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                });
            }

            private void HideHoldDialog() {
                var dialog = _dialog;
                if (dialog == null) return;

                _dialog = null;
                _progressBar = null;
                _textBlock = null;
                CompositionTargetEx.Rendering -= OnRendering;

                Application.Current.Dispatcher.InvokeAsync(() => {
                    try {
                        dialog.Close();
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                });
            }
            #endregion
        }

        public class HotkeyJoyCommand : JoyCommandBase {
            private readonly Keys[] _keys;

            public HotkeyJoyCommand(params Keys[] keys) {
                _keys = keys;
            }

            protected override void Apply() {
                Press(_keys);
            }

            public override string ToString() {
                return $"(Keys={_keys.JoinToString('+')})";
            }
        }

        public class CallbackJoyCommand : JoyCommandBase {
            private readonly Action _apply;

            public CallbackJoyCommand(Action apply) {
                _apply = apply;
            }

            protected override void Apply() {
                _apply?.Invoke();
            }

            public override string ToString() {
                return $"(Callback={_apply})";
            }
        }

        public class ReverseHotkeyJoyCommand : JoyCommandBase {
            private readonly string _baseId;
            private Keys? _key;

            public ReverseHotkeyJoyCommand(string baseId) {
                _baseId = baseId;
            }

            protected override void Apply() {
                if (_key.HasValue) {
                    Press(Keys.RControlKey, Keys.RShiftKey, _key.Value);
                }
            }

            public override void ConsiderControlsIni(IniFile iniFile) {
                _key = (Keys)iniFile[_baseId].GetInt("KEY", -1);
            }

            public override string ToString() {
                return $"(RevHotkey={_baseId})";
            }
        }

        public class AcJoyCommand : JoyCommandBase {
            private readonly AcCommand _command;

            public AcJoyCommand(AcCommand command) {
                _command = command;
            }

            protected override void Apply() {
                InternalUtils.AcControlPointExecute(_command);
            }

            public override string ToString() {
                return $"(AcCommand={_command})";
            }
        }

        private static void Press(params Keys[] values) {
            for (var i = 0; i < values.Length; i++) {
                User32.SendInput(new User32.KeyboardInput {
                    VirtualKeyCode = (ushort)values[i],
                    Flags = User32.IsExtendedKey(values[i]) ? User32.KeyboardFlag.ExtendedKey : User32.KeyboardFlag.None
                });
                Thread.Sleep(10);
            }
            for (var i = values.Length - 1; i >= 0; i--) {
                User32.SendInput(new User32.KeyboardInput {
                    VirtualKeyCode = (ushort)values[i],
                    Flags = User32.IsExtendedKey(values[i]) ? User32.KeyboardFlag.ExtendedKey | User32.KeyboardFlag.KeyUp : User32.KeyboardFlag.KeyUp
                });
                Thread.Sleep(10);
            }
        }

        private class MemoryListener : IDisposable {
            private static readonly string[] ShortenDelays = {
                "ABS", "__CM_ABS_DECREASE",
                "TRACTION_CONTROL", "__CM_TRACTION_CONTROL_DECREASE",
            };

            private static readonly Dictionary<string, JoyCommandBase> ExtraCommands;
            private static bool _isInPits, _isDriving;
            private static bool _delaysAvailable;

            static MemoryListener() {
                var startStopSession =
                        new CallbackJoyCommand(
                                () => { InternalUtils.AcControlPointExecute(_isDriving ? AcCommand.TeleportToPitsWithConfig : AcCommand.StartGame); });

                ExtraCommands = new Dictionary<string, JoyCommandBase> {
                    ["__CM_START_SESSION"] = new AcJoyCommand(AcCommand.StartGame),
                    ["__CM_RESET_SESSION"] = new AcJoyCommand(AcCommand.RestartSession),
                    ["__CM_TO_PITS"] = new AcJoyCommand(AcCommand.TeleportToPits),
                    ["__CM_SETUP_CAR"] = new AcJoyCommand(AcCommand.TeleportToPitsWithConfig),
                    ["__CM_EXIT"] = new AcJoyCommand(AcCommand.Shutdown),
                    ["__CM_PAUSE"] = new CallbackJoyCommand(ContinueRace),
                    ["__CM_START_STOP_SESSION"] = startStopSession,
                    ["__CM_ABS_DECREASE"] = new ReverseHotkeyJoyCommand("ABS"),
                    ["__CM_TRACTION_CONTROL_DECREASE"] = new ReverseHotkeyJoyCommand("TRACTION_CONTROL"),
                    ["__CM_NEXT_APPS_DESKTOP"] = new HotkeyJoyCommand(Keys.RControlKey, Keys.U) { MinInterval = OptionSmallInterval },
                    ["__CM_ONLINE_POLL_YES"] = new HotkeyJoyCommand(Keys.Y) { IsAvailableTest = IsOnlineRace },
                    ["__CM_ONLINE_POLL_NO"] = new HotkeyJoyCommand(Keys.N) { IsAvailableTest = IsOnlineRace },
                };

                AcSharedMemory.Instance.Updated += (sender, args) => {
                    _isInPits = args.Current?.Graphics.IsInPits == true;
                    _isDriving = IsDriving(args.Current);
                    startStopSession.DelayedName = _isDriving && _delaysAvailable ? "Setup car in pits" : null;

                    bool IsDriving(AcShared shared) {
                        if (shared == null) return false;
                        var p = shared.Physics;
                        if (p.SteerAngle != 0f || p.Brake != 1f || p.Clutch != 0f
                                || p.TurboBoost > 0.01f || p.Gas > 0.01f || p.SpeedKmh > 1f) return true;
                        for (var i = 0; i < 5; i++) {
                            if (p.CarDamage[i] != 0f) {
                                return true;
                            }
                        }
                        return false;
                    }
                };
            }

            private static void ContinueRace() {
                if (!ContinueRaceHelper.ContinueRace()) {
                    Press(Keys.Escape);
                }
            }

            private static bool IsOnlineRace() {
                return new IniFile(AcPaths.GetRaceIniFilename())["REMOTE"].GetBool("ACTIVE", false);
            }

            private IKeyboardListener _keyboard;

            private struct JoyKey {
                public JoyKey(int joy, int button) {
                    Joy = joy;
                    Button = button;
                    Pov = null;
                    Direction = DirectInputPovDirection.Top;
                }

                public JoyKey(int joy, int pov, DirectInputPovDirection direction) {
                    Joy = joy;
                    Button = null;
                    Pov = pov;
                    Direction = direction;
                }

                public readonly int Joy;
                public readonly int? Button;
                public readonly int? Pov;
                public readonly DirectInputPovDirection Direction;

                public bool Equals(JoyKey other) {
                    return Joy == other.Joy && Button == other.Button && Pov == other.Pov && Direction == other.Direction;
                }

                public override bool Equals(object obj) {
                    return !ReferenceEquals(null, obj) && obj is JoyKey key && Equals(key);
                }

                public override int GetHashCode() {
                    unchecked {
                        var hashCode = Joy;
                        hashCode = (hashCode * 397) ^ Button.GetHashCode();
                        hashCode = (hashCode * 397) ^ Pov.GetHashCode();
                        hashCode = (hashCode * 397) ^ (int)Direction;
                        return hashCode;
                    }
                }

                public override string ToString() {
                    return $"(Joy={Joy}, Button={Button ?? -1}, Pov={Pov ?? -1})";
                }
            }

            [CanBeNull]
            private readonly KeyValuePair<JoyKey, JoyCommandBase>[] _joyToCommand;

            [CanBeNull]
            private readonly KeyValuePair<Keys, JoyCommandBase>[] _keyToCommand;

            private readonly bool _ignorePovInPits;

            [CanBeNull]
            private readonly Thread _pollThread;

            [CanBeNull]
            private readonly CmInGameAppHelper _inGameApp;

            public MemoryListener() {
                var ini = new IniFile(AcPaths.GetCfgControlsFilename());
                var isAcFullscreen = new IniFile(AcPaths.GetCfgVideoFilename())["VIDEO"].GetBool("FULLSCREEN", false);
                var isCmAppInstalled = CmInGameAppHelper.IsAvailable();
                var delayEnabled = ini["__EXTRA_CM"].GetBool("DELAY_SPECIFIC_SYSTEM_COMMANDS", true);
                var showDelay = delayEnabled && ini["__EXTRA_CM"].GetBool("SHOW_SYSTEM_DELAYS", true);
                _ignorePovInPits = ini["__EXTRA_CM"].GetBool("SYSTEM_IGNORE_POV_IN_PITS", true);

                var keyToCommand = new Dictionary<Keys, JoyCommandBase>();
                var joyToCommand = new Dictionary<JoyKey, JoyCommandBase>();
                var delaysAvailable = delayEnabled && (!isAcFullscreen || isCmAppInstalled);
                _delaysAvailable = delaysAvailable;

                foreach (var n in AcSettingsHolder.Controls.SystemButtonKeys) {
                    var section = ini[n];
                    var delay = ShortenDelays.ArrayContains(n) ? OptionSmallInterval : OptionLargeInterval;

                    var isExtra = ExtraCommands.TryGetValue(n, out var c);
                    if (isExtra && c.IsAvailableTest?.Invoke() == false) continue;

                    var joy = section.GetInt("JOY", -1);
                    var button = section.GetInt("BUTTON", -1);
                    var pov = section.GetInt("__CM_POV", -1);
                    var povDirection = section.GetIntEnum("__CM_POV_DIR", DirectInputPovDirection.Top);
                    var key = section.GetInt("KEY", -1);

                    var delayedName = delaysAvailable ? AcSettingsHolder.Controls.SystemRaceButtonEntries.FirstOrDefault(
                            x => x.Delayed && x.WheelButton.Id == n)?.WheelButton.DisplayName : null;

                    if (isExtra && key != -1) {
                        c.MinInterval = delay;
                        c.DelayedName = delayedName;
                        c.ShowDelay &= showDelay && !isAcFullscreen;
                        keyToCommand[(Keys)key] = c;
                    }

                    if (joy == -1 || button == -1 && pov == -1) {
                        continue;
                    }

                    var joyKey = button != -1 ? new JoyKey(joy, button) : new JoyKey(joy, pov, povDirection);
                    if (isExtra) {
                        c.ConsiderControlsIni(ini);
                    } else if (key != -1) {
                        c = new HotkeyJoyCommand(Keys.RControlKey, (Keys)key);
                    } else {
                        continue;
                    }

                    c.MinInterval = delay;
                    c.DelayedName = delayedName;
                    c.ShowDelay &= showDelay && !isAcFullscreen;
                    joyToCommand[joyKey] = c;
                }

                Logging.Write("Extra key bindings: " + keyToCommand.Count);
                Logging.Write("Extra joystick bindings: " + joyToCommand.Count);

                if (keyToCommand.Count > 0) {
                    _keyboard = KeyboardListenerFactory.Get();
                    _keyboard.WatchFor(keyToCommand.Select(x => x.Key));
                    _keyboard.KeyDown += OnKeyDown;
                    _keyboard.KeyUp += OnKeyUp;
                }

                if (joyToCommand.Count > 0) {
                    try {
                        _pollThread = new Thread(Start) {
                            Name = "CM Controllers Polling",
                            IsBackground = true
                        };
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                }

                _joyToCommand = joyToCommand.ToArray();
                _keyToCommand = keyToCommand.ToArray();
                _pollThread?.Start();

                if (showDelay && isAcFullscreen
                        && (joyToCommand.Any(x => x.Value.DelayedName != null) || keyToCommand.Any(x => x.Value.DelayedName != null))) {
                    _inGameApp = CmInGameAppHelper.GetInstance();
                }
            }

            private bool? _running;

            private async void Start() {
                if (_running == null) {
                    _running = true;
                }

                var devices = await DirectInputScanner.GetAsync();
                var joysticks = DirectInputScanner.DirectInput == null ? null
                        : devices?.Select(x => new Joystick(DirectInputScanner.DirectInput, x.InstanceGuid)).ToList();
                var joystickCommands = joysticks == null ? null : _joyToCommand?.GroupBy(x => x.Key.Joy).Select(x =>
                        Tuple.Create(joysticks.ElementAtOrDefault(x.Key), x.ToArray())).Where(x => x.Item1 != null).ToList();

#if DEBUG
                var s = new Stopwatch();
                var iterations = 0;
#endif

                try {
                    while (_running == true) {
#if DEBUG
                        s.Start();
                        OnTick(joystickCommands);
                        s.Stop();
#else
                        OnTick(joystickCommands);
#endif
                        Thread.Sleep(8);

#if DEBUG
                        if (++iterations >= 300) {
                            Logging.Debug($"Time per tick: {s.Elapsed.TotalMilliseconds / iterations:F2} ms");
                            iterations = 0;
                            s.Restart();
                        }
#endif
                    }
                } finally {
                    DisposeHelper.Dispose(ref joysticks);
                }
            }

            private void OnTick(List<Tuple<Joystick, KeyValuePair<JoyKey, JoyCommandBase>[]>> devices) {
                if (devices == null) return;

                var joyToCommand = _joyToCommand;
                var keyToCommand = _keyToCommand;

                var povAvailable = !(_ignorePovInPits && _isInPits);
                for (var index = devices.Count - 1; index >= 0; index--) {
                    try {
                        var item = devices[index];
                        var joystick = item.Item1;
                        if (joystick.Acquire().IsFailure || joystick.Poll().IsFailure) continue;

                        var state = joystick.GetCurrentState();
                        if (Result.Last.IsFailure) continue;

                        var buttons = state.GetButtons();
                        var povs = state.GetPointOfViewControllers();
                        var list = item.Item2;
                        for (var i = list.Length - 1; i >= 0; i--) {
                            var command = list[i];
                            var key = command.Key;
                            command.Value.SetJoyPressed(key.Button.HasValue
                                    ? buttons.ArrayElementAtOrDefault(key.Button.Value)
                                    : (povAvailable || key.Pov != 0) && key.Direction.IsInRange(povs.ArrayElementAtOrDefault(key.Pov ?? 0)));
                        }
                    } catch (DirectInputException e) when (e.Message.Contains(@"DIERR_UNPLUGGED")) {
                        devices.RemoveAt(index);
                    } catch (DirectInputException e) {
                        devices.RemoveAt(index);
                        Logging.Warning(e);
                    }
                }

                string delayedName = null;
                var delayedProgress = 0d;

                for (var i = joyToCommand.Length - 1; i >= 0; i--) {
                    var v = joyToCommand[i].Value;
                    v.Update();
                    if (v.IsDelayed) {
                        delayedName = v.DelayedName;
                        delayedProgress = v.Progress;
                    }
                }

                for (var i = keyToCommand.Length - 1; i >= 0; i--) {
                    var v = keyToCommand[i].Value;
                    v.Update();
                    if (v.IsDelayed) {
                        delayedName = v.DelayedName;
                        delayedProgress = v.Progress;
                    }
                }

                var inGameApp = _inGameApp;
                if (inGameApp != null) {
                    if (delayedName != null) {
                        inGameApp.Update(new CmInGameAppDelayedInputParams(delayedName.ToSentenceMember()) { Progress = delayedProgress });
                    } else {
                        inGameApp.HideApp();
                    }
                }
            }

            private JoyCommandBase GetCommand(Keys key) {
                if (_keyToCommand == null) return null;
                for (var i = _keyToCommand.Length - 1; i >= 0; i--) {
                    var x = _keyToCommand[i];
                    if (x.Key == key) {
                        return x.Value;
                    }
                }
                return null;
            }

            private void OnKeyDown(object sender, KeyboardEventArgs e) {
                var c = GetCommand(e.Key);
                if (c != null && Keyboard.Modifiers == c.Modifiers) {
                    c.SetKeyboardPressed(true);
                }
            }

            private void OnKeyUp(object sender, KeyboardEventArgs e) {
                GetCommand(e.Key)?.SetKeyboardPressed(false);
            }

            public void Dispose() {
                _running = false;

                try {
                    DisposeHelper.Dispose(ref _keyboard);
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t remove events hook", e);
                }

                _inGameApp?.Dispose();
            }
        }

        public override IDisposable Set(Process process) {
            return new MemoryListener();
        }
    }
}