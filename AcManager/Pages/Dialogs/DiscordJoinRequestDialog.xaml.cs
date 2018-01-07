using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.DiscordRpc;
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

namespace AcManager.Pages.Dialogs {
    public partial class DiscordJoinRequestDialog : INotifyPropertyChanged {
        private DirectInput _directInput;
        private List<Joystick> _devices;

        private static Point GetWindowPosition(int width, int height) {
            var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var bounds = screen.Bounds;
            var x = bounds.Left + (bounds.Width - width) / 2;
            var y = bounds.Bottom - height - 20;
            return new Point(x, y);
        }

        public DiscordJoinRequestDialog(DiscordJoinRequest args, CancellationToken cancellation = default(CancellationToken)) {
            cancellation.Register(async () => {
                await Task.Delay(200);
                CloseWithResult(MessageBoxResult.Cancel);
            });

            Owner = null;
            DataContext = this;
            InitializeComponent();
            Buttons = new Control[0];

            Title.Text = $"User {args.UserName} wants to join. Allow?";

            if (!string.IsNullOrWhiteSpace(args.AvatarUrl)) {
                Image.Filename = $"{args.AvatarUrl}?size=128";
            } else {
                Image.Visibility = Visibility.Collapsed;
            }

            this.OnActualUnload(OnUnload);

            try {
                if (_directInput == null) {
                    _directInput = new DirectInput();
                }

                _devices = _directInput.GetDevices(DeviceClass.GameController,
                        DeviceEnumerationFlags.AttachedOnly).Select(x => new Joystick(_directInput, x.InstanceGuid)).ToList();
            } catch (Exception e) {
                Logging.Error(e);
            }

            CompositionTargetEx.Rendering += OnRendering;
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
            switch (MessageBoxResult) {
                case MessageBoxResult.No:
                    AlterValue(YesBar, false, null);
                    return;
                case MessageBoxResult.Yes:
                    AlterValue(NoBar, false, null);
                    return;
            }

            var anyLeftPressed = User32.IsKeyPressed(Keys.Left) || User32.IsKeyPressed(Keys.Enter);
            var anyRightPressed = User32.IsKeyPressed(Keys.Right) || User32.IsKeyPressed(Keys.Escape);

            for (var index = _devices.Count - 1; index >= 0; index--) {
                var joystick = _devices[index];

                try {
                    if (joystick.Acquire().IsFailure || joystick.Poll().IsFailure || Result.Last.IsFailure) {
                        continue;
                    }

                    var pointOfViews = joystick.GetCurrentState().GetPointOfViewControllers();
                    for (var i = 0; i < pointOfViews.Length; i++) {
                        var value = pointOfViews[i];
                        anyLeftPressed |= value >= 22500 && value <= 31500;
                        anyRightPressed |= value >= 4500 && value <= 13500;
                    }
                } catch (DirectInputException e) when (e.Message.Contains(@"DIERR_UNPLUGGED")) {
                    _devices.Remove(joystick);
                } catch (DirectInputException e) {
                    _devices.Remove(joystick);
                    Logging.Warning(e);
                }
            }

            AlterValue(NoBar, anyRightPressed, NoCommand);
            AlterValue(YesBar, !anyRightPressed && anyLeftPressed, YesCommand);
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
            CompositionTargetEx.Rendering -= OnRendering;
            DisposeHelper.Dispose(ref _devices);
            DisposeHelper.Dispose(ref _directInput);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
