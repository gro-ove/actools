using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.DirectInput;

namespace AcManager.Tools.Helpers.DirectInput {
    public sealed class DirectInputDevice : Displayable, IDirectInputDevice, IDisposable {
        [NotNull]
        public DeviceInstance Device { get; }

        public string InstanceId { get; }

        public string ProductId { get; }

        public bool IsVirtual => false;

        public bool IsController { get; }

        public int Index { get; }

        public IList<int> OriginalIniIds { get; }

        public bool Same(IDirectInputDevice other) {
            return other != null && (this.IsSameAs(other) || IsController && other.InstanceId == @"0");
        }

        public bool Same(DeviceInstance other, int index) {
            return other != null && InstanceId == GuidToString(other.InstanceGuid);
        }

        public DirectInputAxle GetAxle(int id) {
            return id < 0 ? null : Axis.GetByIdOrDefault(id);
        }

        public DirectInputButton GetButton(int id) {
            return id < 0 ? null : Buttons.GetByIdOrDefault(id);
        }

        public DirectInputPov GetPov(int id, DirectInputPovDirection direction) {
            return id < 0 ? null : Povs.FirstOrDefault(x => x.Id == id && x.Direction == direction);
        }

        public DirectInputAxle[] Axis { get; }
        public DirectInputButton[] Buttons { get; }
        public DirectInputPov[] Povs { get; }

        public IReadOnlyList<DirectInputAxle> VisibleAxis { get; set; }
        public IReadOnlyList<DirectInputButton> VisibleButtons { get; set; }
        public IReadOnlyList<DirectInputPov> VisiblePovs { get; set; }

        private readonly Joystick _joystick;

        public override string ToString() {
            return $"DirectInputDevice({InstanceId}:{DisplayName}, Ini={Index})";
        }

        public void RunControlPanel() {
            _joystick.RunControlPanel();
        }

        private static string GuidToString(Guid guid) {
            return guid.ToString().ToUpperInvariant();
        }

        private static bool Acquire(Device newdevice) {
            var result = newdevice.Acquire();
            if (result.IsSuccess) {
                newdevice.Poll();
                return true;
            }

            Logging.Debug($"Can’t acquire {newdevice.Information.ProductName}: {result.Description}");
            return false;
        }

        private DirectInputDevice([NotNull] Joystick device, int index) {
            Device = device.Information;

            InstanceId = GuidToString(device.Information.InstanceGuid);
            ProductId = GuidToString(device.Information.ProductGuid);
            Index = index;
            IsController = DirectInputDeviceUtils.IsController(device.Information.InstanceName);
            OriginalIniIds = new List<int>();

            _joystick = device;

            var window = Application.Current?.MainWindow;
            if (window != null) {
                try {
                    _joystick.SetCooperativeLevel(new WindowInteropHelper(window).Handle, CooperativeLevel.Background | CooperativeLevel.Nonexclusive);
                    _joystick.Properties.AxisMode = DeviceAxisMode.Absolute;

                    if (!Acquire(_joystick)) {
                        _joystick.SetCooperativeLevel(new WindowInteropHelper(window).Handle, CooperativeLevel.Background | CooperativeLevel.Nonexclusive);
                        Acquire(_joystick);
                    }
                } catch (Exception e) {
                    Logging.Warning("Can’t set cooperative level: " + e);
                }
            }

            Axis = Enumerable.Range(0, 8).Select(x => new DirectInputAxle(this, x)).ToArray();
            Buttons = Enumerable.Range(0, _joystick.Capabilities.ButtonCount).Select(x => new DirectInputButton(this, x)).ToArray();
            Povs = Enumerable.Range(0, _joystick.Capabilities.PovCount)
                             .SelectMany(x => Enumerable.Range(0, 4).Select(y => new { Id = x, Direction = (DirectInputPovDirection)y }))
                             .Select(x => new DirectInputPov(this, x.Id, x.Direction)).ToArray();
            RefreshDescription();
            FilesStorage.Instance.Watcher(ContentCategory.Controllers).Update += OnDefinitionsUpdate;
        }

        public static string FixDisplayName(string deviceName) {
            var match = Regex.Match(deviceName, @"^Controller \((.+)\)$");
            if (match.Success) return match.Groups[1].Value;
            var trimmed = Regex.Replace(Regex.Replace(deviceName, @"\b(?:USB|Racing Wheel)\b", ""), @"\s+", " ").Trim();
            return string.IsNullOrEmpty(trimmed) ? deviceName : trimmed;
        }

        private void OnDefinitionsUpdate(object sender, EventArgs eventArgs) {
            RefreshDescription();
        }

        public void RefreshDescription() {
            if (!DisplayInputParams.Get(Device.ProductGuid.ToString(), out var displayName, out var axisP, out var buttonsP, out var povsP)
                    && IsController) {
                DisplayInputParams.Get(DirectInputDeviceUtils.GetXboxControllerGuid(), out _, out axisP, out buttonsP, out povsP);
            }

            DisplayName = displayName ?? FixDisplayName(Device.InstanceName);
            Proc(Axis, axisP);
            Proc(Buttons, buttonsP);
            Proc(Povs, povsP);

            void Proc(IEnumerable<IInputProvider> items, DisplayInputParams p) {
                foreach (var t in items) {
                    t.SetDisplayParams(p?.Name(t.Id), p?.Test(t.Id) ?? true);
                }
            }

            VisibleAxis = Axis.Where(x => x.IsVisible).ToList();
            VisibleButtons = Buttons.Where(x => x.IsVisible).ToList();
            VisiblePovs = Povs.Where(x => x.IsVisible).ToList();
            OnPropertyChanged(nameof(VisibleAxis));
            OnPropertyChanged(nameof(VisibleButtons));
            OnPropertyChanged(nameof(VisiblePovs));
        }

        [CanBeNull]
        public static DirectInputDevice Create(Joystick device, int iniId) {
            try {
                return new DirectInputDevice(device, iniId);
            } catch (DirectInputNotFoundException e) {
                Logging.Warning(e);
                return null;
            } catch (DirectInputException e) {
                Logging.Error(e);
                return null;
            }
        }

        public bool Error { get; private set; }
        public bool Unplugged { get; private set; }

        public void OnTick() {
            try {
                if (_joystick.Disposed || _joystick.Acquire().IsFailure || _joystick.Poll().IsFailure || Result.Last.IsFailure) {
                    return;
                }

                var state = _joystick.GetCurrentState();

                for (var i = 0; i < Axis.Length; i++) {
                    var a = Axis[i];
                    a.Value = GetAxisValue(a.Id, state);
                }

                var buttons = state.GetButtons();
                for (var i = 0; i < Buttons.Length; i++) {
                    var b = Buttons[i];
                    b.Value = b.Id < buttons.Length && buttons[b.Id];
                }

                var povs = state.GetPointOfViewControllers();
                for (var i = 0; i < Povs.Length; i++) {
                    var b = Povs[i];
                    b.Value = b.Direction.IsInRange(b.Id < povs.Length ? povs[b.Id] : -1);
                }
            } catch (DirectInputException e) when (e.Message.Contains(@"DIERR_UNPLUGGED")) {
                Unplugged = true;
            } catch (DirectInputException e) {
                if (!Error) {
                    Logging.Warning("Exception: " + e);
                    Error = true;
                }
            }

            double GetAxisValue(int id, JoystickState state) {
                switch (id) {
                    case 0:
                        return state.X / 65535d;
                    case 1:
                        return state.Y / 65535d;
                    case 2:
                        return state.Z / 65535d;
                    case 3:
                        return state.RotationX / 65535d;
                    case 4:
                        return state.RotationY / 65535d;
                    case 5:
                        return state.RotationZ / 65535d;
                    case 6:
                        return state.GetSliders().Length > 0 ? state.GetSliders()[0] / 65535d : 0d;
                    case 7:
                        return state.GetSliders().Length > 1 ? state.GetSliders()[1] / 65535d : 0d;
                    default:
                        return 0;
                }
            }
        }

        public void Dispose() {
            FilesStorage.Instance.Watcher(ContentCategory.Controllers).Update -= OnDefinitionsUpdate;
        }
    }
}