using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX.DirectInput;

namespace AcManager.Tools.Helpers.DirectInput {
    public interface IDirectInputDevice : IWithId {
        string DisplayName { get; }

        int IniId { get; }
    }

    public sealed class DirectInputDevice : Displayable, IDirectInputDevice, IDisposable {
        public DeviceInstance Device { get; }

        public string Id { get; }

        public int IniId { get; }

        public DirectInputButton[] Buttons { get; } 

        public DirectInputAxle[] Axles { get; } 

        private Joystick _joystick;
        private readonly int _buttonsCount;

        private DirectInputDevice(SlimDX.DirectInput.DirectInput directInput, DeviceInstance device, int iniId) {
            Device = device;
            DisplayName = device.InstanceName;

            Id = device.ProductGuid.ToString().ToUpperInvariant();
            IniId = iniId;

            _joystick = new Joystick(directInput, Device.InstanceGuid);
            _joystick.SetCooperativeLevel(new WindowInteropHelper(Application.Current.MainWindow).Handle,
                    CooperativeLevel.Background | CooperativeLevel.Nonexclusive);

            var capabilities = _joystick.Capabilities;
            _buttonsCount = capabilities.ButtonCount;

            Buttons = Enumerable.Range(0, _buttonsCount).Select(x => new DirectInputButton(this, x)).ToArray();
            Axles = Enumerable.Range(0, 8).Select(x => new DirectInputAxle(this, x)).ToArray();
        }

        [CanBeNull]
        public static DirectInputDevice Create(SlimDX.DirectInput.DirectInput directInput, DeviceInstance device, int iniId) {
            try {
                return new DirectInputDevice(directInput, device, iniId);
            } catch (DirectInputException) {
                return null;
            }
        }

        public bool Error { get; private set; }

        public bool Unplugged { get; private set; }

        public void OnTick() {
            try {
                if (_joystick.Acquire().IsFailure || _joystick.Poll().IsFailure || SlimDX.Result.Last.IsFailure) {
                    return;
                }

                var state = _joystick.GetCurrentState();

                var i = 0;
                foreach (var source in state.GetButtons().Take(_buttonsCount)) {
                    Buttons[i++].Value = source;
                }

                i = 0;
                var sliders = state.GetSliders();
                foreach (var source in new [] {
                    state.X, state.Y, state.Z,
                    state.RotationX, state.RotationY, state.RotationZ,
                    sliders[0], sliders[1]
                }) {
                    Axles[i++].Value = source / 65535d;
                }
            } catch (DirectInputException e) {
                if (e.Message.Contains("DIERR_UNPLUGGED")) {
                    Unplugged = true;
                } else if (!Error){
                    Logging.Warning("[DirectInputDevice] Exception: " + e);
                    Error = true;
                }
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _joystick);
        }
    }
}