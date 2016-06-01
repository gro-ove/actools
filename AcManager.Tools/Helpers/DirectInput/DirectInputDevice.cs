using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using SlimDX.DirectInput;

namespace AcManager.Tools.Helpers.DirectInput {
    public sealed class DirectInputDevice : Displayable, IWithId, IDisposable {
        public DeviceInstance Device { get; }

        public string Id { get; }

        public int IniId { get; }

        public DirectInputButton[] Buttons { get; } 

        public DirectInputAxle[] Axles { get; } 

        private Joystick _joystick;
        private readonly int _buttonsCount;

        public DirectInputDevice(SlimDX.DirectInput.DirectInput directInput, DeviceInstance device, int iniId) {
            Device = device;
            DisplayName = device.InstanceName;

            Id = device.ProductGuid.ToString().ToUpperInvariant();
            IniId = iniId;

            _joystick = new Joystick(directInput, Device.InstanceGuid);

            var capabilities = _joystick.Capabilities;
            _buttonsCount = capabilities.ButtonCount;

            Buttons = Enumerable.Range(0, _buttonsCount).Select(x => new DirectInputButton(this, x)).ToArray();
            Axles = Enumerable.Range(0, 8).Select(x => new DirectInputAxle(this, x)).ToArray();
        }

        public void OnTick() {
            try {
                // TODO
                if (_joystick.Acquire().IsFailure || _joystick.Poll().IsFailure || SlimDX.Result.Last.IsFailure) {
                    // TODO
                    _joystick.SetCooperativeLevel(new WindowInteropHelper(Application.Current.MainWindow).Handle,
                            CooperativeLevel.Background | CooperativeLevel.Nonexclusive);

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
                // TODO
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _joystick);
        }
    }
}