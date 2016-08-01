using System;
using System.Collections.Generic;
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

        int IniId { get; set; }

        [CanBeNull]
        DirectInputAxle GetAxle(int id);

        [CanBeNull]
        DirectInputButton GetButton(int id);
    }

    public class PlaceholderInputDevice : IDirectInputDevice {
        public PlaceholderInputDevice(string id, string displayName, int iniId) {
            Id = id;
            DisplayName = displayName + " (!)";
            IniId = iniId;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public int IniId { get; set; }

        private readonly Dictionary<int, DirectInputAxle> _axles = new Dictionary<int, DirectInputAxle>(); 
        private readonly Dictionary<int, DirectInputButton> _buttons = new Dictionary<int, DirectInputButton>(); 

        public DirectInputAxle GetAxle(int id) {
            DirectInputAxle result;
            if (_axles.TryGetValue(id, out result)) return result;
            result = new DirectInputAxle(this, id);
            return _axles[id] = result;
        }

        public DirectInputButton GetButton(int id) {
            DirectInputButton result;
            if (_buttons.TryGetValue(id, out result)) return result;
            result = new DirectInputButton(this, id);
            return _buttons[id] = result;
        }
    }

    public sealed class DirectInputDevice : Displayable, IDirectInputDevice, IDisposable {
        public DeviceInstance Device { get; }

        public string Id { get; }

        public int IniId { get; set; }

        public DirectInputAxle GetAxle(int id) {
            return Axles.ElementAtOrDefault(id);
        }

        public DirectInputButton GetButton(int id) {
            return Buttons.ElementAtOrDefault(id);
        }

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
            if (Application.Current.MainWindow != null) {
                try {
                    _joystick.SetCooperativeLevel(new WindowInteropHelper(Application.Current.MainWindow).Handle,
                            CooperativeLevel.Background | CooperativeLevel.Nonexclusive);
                } catch (Exception e) {
                    Logging.Warning("[DirectInputDevice] Can’t set cooperative level: " + e);
                }
            }

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