using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.DiscordRpc;
using AcManager.Tools.GameProperties.InGameApp;
using AcManager.Tools.Helpers.DirectInput;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.DirectInput;
using Keys = System.Windows.Forms.Keys;
using Path = System.Windows.Shapes.Path;

namespace AcManager.Pages.Dialogs {
    public partial class DiscordJoinRequestDialog {
        [CanBeNull]
        private Joystick[] _devices;

        [CanBeNull]
        private JoystickState[] _states;

        private readonly CmInGameAppHelper _inGameApp = CmInGameAppHelper.GetInstance();
        private readonly CmInGameAppJoinRequestParams _inGameAppParams;

        private static Point GetWindowPosition(int width, int height) {
            var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var bounds = screen.Bounds;
            var x = bounds.Left + (bounds.Width - width) / 2;
            var y = bounds.Bottom - height - 20;
            return new Point(x, y);
        }

        private class ControlsInput {
            private int _joy, _button, _pov;
            private Keys _key;
            private DirectInputPovDirection _povDirection;

            public ControlsInput(IniFileSection section, Keys key) {
                _joy = section.GetInt("JOY", -1);
                _button = section.GetInt("BUTTON", -1);
                _pov = section.GetInt("__CM_POV", -1);
                _povDirection = section.GetIntEnum("__CM_POV_DIR", DirectInputPovDirection.Up);

                var k = section.GetInt("KEY", -1);
                _key = k == -1 ? key : (Keys)k;
            }

            [CanBeNull]
            private string GetIconKey() {
                if (_joy != -1 && _pov != -1) {
                    switch (_povDirection) {
                        case DirectInputPovDirection.Left:
                            return "JoystickPovLeftIconData";
                        case DirectInputPovDirection.Up:
                            return "JoystickPovUpIconData";
                        case DirectInputPovDirection.Right:
                            return "JoystickPovRightIconData";
                        case DirectInputPovDirection.Down:
                            return "JoystickPovDownIconData";
                    }
                }

                return null;
            }

            public void SetIcon(Path p, FrameworkElement resources) {
                p.Data = resources.TryFindResource(GetIconKey() ?? "") as Geometry;
                if (p.Data != null) {
                    p.Visibility = Visibility.Visible;
                }
            }

            public bool IsPressed(JoystickState[] states) {
                if (User32.IsAsyncKeyPressed(_key)) return true;

                var state = states.ArrayElementAtOrDefault(_joy);
                if (state == null) return false;

                return state.GetButtons().ArrayElementAtOrDefault(_button)
                        || _pov >= 0 && _pov < state.GetPointOfViewControllers().Length
                                && _povDirection.IsInRange(state.GetPointOfViewControllers().ArrayElementAtOrDefault(_pov));
            }
        }

        private readonly ControlsInput _yes, _no;

        public DiscordJoinRequestDialog(DiscordJoinRequest args, CancellationToken cancellation = default(CancellationToken)) {
            cancellation.Register(async () => {
                await Task.Delay(200);
                CloseWithResult(MessageBoxResult.Cancel);
            });

            Owner = null;
            DataContext = this;
            InitializeComponent();
            Buttons = new Control[0];

            TitleBlock.Text = $"User {args.UserName} wants to join. Allow?";

            if (!string.IsNullOrWhiteSpace(args.AvatarUrl)) {
                Image.Filename = $"{args.AvatarUrl}?size=128";
            } else {
                Image.Visibility = Visibility.Collapsed;
            }

            this.OnActualUnload(OnUnload);

            var config = new IniFile(AcPaths.GetCfgControlsFilename());
            _yes = new ControlsInput(config["__CM_DISCORD_REQUEST_ACCEPT"], Keys.Enter);
            _no = new ControlsInput(config["__CM_DISCORD_REQUEST_DENY"], Keys.Back);

            try {
                SetDevices().Ignore();
                _yes.SetIcon(YesIcon, this);
                _no.SetIcon(NoIcon, this);
            } catch (Exception e) {
                Logging.Error(e);
            }

            _inGameAppParams = new CmInGameAppJoinRequestParams(args.UserName, args.UserId, args.AvatarUrl,
                    b => (b ? YesCommand : NoCommand).ExecuteAsync().Ignore());
            CompositionTargetEx.Rendering += OnRendering;
        }

        private async Task SetDevices() {
            var devices = await DirectInputScanner.GetAsync();
            _devices = devices?.ToArray();
            _states = _devices == null ? null : new JoystickState[_devices.Length];
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            var point = GetWindowPosition(ActualWidth.RoundToInt(), ActualHeight.RoundToInt());
            Left = point.X;
            Top = point.Y;
        }

        private static void AlterValue([NotNull] RangeBase slider, bool value, [CanBeNull] ICommand run) {
            if (value) {
                slider.Value += 0.04 + (1 - slider.Value) * 0.03;
                if (slider.Value >= 1d) {
                    run?.Execute(null);
                }
            } else {
                slider.Value *= 0.8 - 0.01;
            }
        }

        private void OnRendering(object sender, RenderingEventArgs args) {
            try {
                if (_devices == null || _states == null) return;

                switch (MessageBoxResult) {
                    case MessageBoxResult.No:
                        AlterValue(YesBar, false, null);
                        return;
                    case MessageBoxResult.Yes:
                        AlterValue(NoBar, false, null);
                        return;
                }

                for (var index = _devices.Length - 1; index >= 0; index--) {
                    var joystick = _devices[index];
                    if (joystick == null || joystick.Disposed) continue;

                    try {
                        if (!joystick.Acquire().IsFailure && !joystick.Poll().IsFailure && !Result.Last.IsFailure) {
                            _states[index] = joystick.GetCurrentState();
                        }
                    } catch (DirectInputException e) when (e.Message.Contains(@"DIERR_UNPLUGGED")) {
                        _devices[index] = null;
                    } catch (DirectInputException e) {
                        _devices[index] = null;
                        Logging.Warning(e);
                    }
                }

                var yes = _yes.IsPressed(_states);
                var no = _no.IsPressed(_states);
                AlterValue(NoBar, no, NoCommand);
                AlterValue(YesBar, !no && yes, YesCommand);
            } finally {
                _inGameAppParams.YesProgress = YesBar.Value;
                _inGameAppParams.NoProgress = NoBar.Value;
                _inGameApp.Update(_inGameAppParams);
            }
        }

        private AsyncCommand _yesCommand;

        public AsyncCommand YesCommand => _yesCommand ?? (_yesCommand = new AsyncCommand(async () => {
            MessageBoxResult = MessageBoxResult.Yes;
            YesBar.Value = 1d;
            YesBar.Foreground = new SolidColorBrush { Color = ((SolidColorBrush)YesBar.Foreground).Color };
            YesBar.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((Color)FindResource("GoColor"),
                    new Duration(TimeSpan.FromSeconds(0.12))));
            TextBlock.SetForeground(YesText, new SolidColorBrush { Color = ((SolidColorBrush)TextBlock.GetForeground(YesText)).Color });
            TextBlock.GetForeground(YesText).BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Black,
                    new Duration(TimeSpan.FromSeconds(0.12))));
            await Task.Delay(500);
            CloseWithResult(MessageBoxResult.Yes);
        }, () => MessageBoxResult == MessageBoxResult.None));

        private AsyncCommand _noCommand;

        public AsyncCommand NoCommand => _noCommand ?? (_noCommand = new AsyncCommand(async () => {
            MessageBoxResult = MessageBoxResult.No;
            NoBar.Value = 1d;
            NoBar.Foreground = new SolidColorBrush { Color = ((SolidColorBrush)NoBar.Foreground).Color };
            NoBar.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((Color)FindResource("AccentColor"),
                    new Duration(TimeSpan.FromSeconds(0.12))));
            TextBlock.SetForeground(NoText, new SolidColorBrush { Color = ((SolidColorBrush)TextBlock.GetForeground(NoText)).Color });
            TextBlock.GetForeground(NoText).BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.White,
                    new Duration(TimeSpan.FromSeconds(0.12))));
            await Task.Delay(500);
            CloseWithResult(MessageBoxResult.No);
        }, () => MessageBoxResult == MessageBoxResult.None));

        private void OnUnload() {
            _inGameApp.Dispose();
            CompositionTargetEx.Rendering -= OnRendering;
        }
    }
}
