using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Windows;
using System.Windows.Interop;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.DirectInput;

namespace AcManager.Tools.Helpers.DirectInput {
    public interface IDirectInputDevice : IWithId {
        bool IsVirtual { get; }

        string DisplayName { get; }

        int Index { get; }

        IList<int> OriginalIniIds { get; }

        bool Same(IDirectInputDevice other);

        [CanBeNull]
        DirectInputAxle GetAxle(int id);

        [CanBeNull]
        DirectInputButton GetButton(int id);
    }

    public class PlaceholderInputDevice : IDirectInputDevice {
        public PlaceholderInputDevice([CanBeNull] string id, string displayName, int index) {
            Id = id;
            DisplayName = displayName;
            Index = index;
            OriginalIniIds = new List<int> { index };
        }

        [CanBeNull]
        public string Id { get; }

        public bool IsVirtual => true;

        public string DisplayName { get; }

        public int Index { get; set; }

        public IList<int> OriginalIniIds { get; }

        private readonly Dictionary<int, DirectInputAxle> _axles = new Dictionary<int, DirectInputAxle>();
        private readonly Dictionary<int, DirectInputButton> _buttons = new Dictionary<int, DirectInputButton>();

        public bool Same(IDirectInputDevice other) {
            return other != null && (Id == other.Id || DisplayName == other.DisplayName);
        }

        public DirectInputAxle GetAxle(int id) {
            return _axles.TryGetValue(id, out var result) ? result : (_axles[id] = new DirectInputAxle(this, id));
        }

        public DirectInputButton GetButton(int id) {
            return _buttons.TryGetValue(id, out var result) ? result : (_buttons[id] = new DirectInputButton(this, id));
        }

        public override string ToString() {
            return $"PlaceholderInputDevice({Id}:{DisplayName}, Ini={Index})";
        }
    }

    public sealed class DirectInputDevice : Displayable, IDirectInputDevice, IDisposable {
        [NotNull]
        public DeviceInstance Device { get; }

        public string Id { get; }

        public bool IsVirtual => false;

        public int Index { get; }

        public IList<int> OriginalIniIds { get; }

        public bool Same(IDirectInputDevice other) {
            return other != null && (Id == other.Id || DisplayName == other.DisplayName);
        }

        public bool Same(DeviceInstance other) {
            return other != null && (Id == GuidToString(other.ProductGuid) || DisplayName == other.InstanceName);
        }

        public DirectInputAxle GetAxle(int id) {
            return Axis.ElementAtOrDefault(id);
        }

        public DirectInputButton GetButton(int id) {
            return Buttons.ElementAtOrDefault(id);
        }

        public DirectInputButton[] Buttons { get; }

        public DirectInputAxle[] Axis { get; }

        private Joystick _joystick;
        private readonly int _buttonsCount;

        public override string ToString() {
            return $"DirectInputDevice({Id}:{DisplayName}, Ini={Index})";
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

        private DirectInputDevice([NotNull] SlimDX.DirectInput.DirectInput directInput, [NotNull] DeviceInstance device, int index) {
            Device = device;
            DisplayName = device.InstanceName;

            Id = GuidToString(device.ProductGuid);
            Index = index;
            OriginalIniIds = new List<int>();

            _joystick = new Joystick(directInput, Device.InstanceGuid);

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

            var capabilities = _joystick.Capabilities;
            _buttonsCount = capabilities.ButtonCount;

            Buttons = Enumerable.Range(0, _buttonsCount).Select(x => new DirectInputButton(this, x)).ToArray();
            Axis = Enumerable.Range(0, 8).Select(x => new DirectInputAxle(this, x)).ToArray();
        }

        [CanBeNull]
        public static DirectInputDevice Create(SlimDX.DirectInput.DirectInput directInput, DeviceInstance device, int iniId) {
            try {
                return new DirectInputDevice(directInput, device, iniId);
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
                if (_joystick.Acquire().IsFailure || _joystick.Poll().IsFailure || Result.Last.IsFailure) {
                    return;
                }

                var state = _joystick.GetCurrentState();

                var buttons = state.GetButtons();
                for (var i = 0; i < _buttonsCount; i++) {
                    Buttons[i].Value = i < buttons.Length && buttons[i];
                }

                Axis[0].Value = state.X / 65535d;
                Axis[1].Value = state.Y / 65535d;
                Axis[2].Value = state.Z / 65535d;
                Axis[3].Value = state.RotationX / 65535d;
                Axis[4].Value = state.RotationY / 65535d;
                Axis[5].Value = state.RotationZ / 65535d;

                var sliders = state.GetSliders();
                Axis[6].Value = sliders.Length > 0 ? sliders[0] / 65535d : 0d;
                Axis[7].Value = sliders.Length > 1 ? sliders[1] / 65535d : 0d;
            } catch (DirectInputException e) when(e.Message.Contains(@"DIERR_UNPLUGGED")) {
                Unplugged = true;
            } catch (DirectInputException e) {
                if (!Error){
                    Logging.Warning("Exception: " + e);
                    Error = true;
                }
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _joystick);
        }
    }
}