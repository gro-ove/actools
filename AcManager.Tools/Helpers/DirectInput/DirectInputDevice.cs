using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
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

        [CanBeNull]
        DirectInputPov GetPov(int id, DirectInputPovDirection direction);
    }

    public class DisplayInputParams {
        public readonly int Take;

        private readonly Func<int, bool> _test;
        private readonly Func<int, string> _name;

        public bool Test(int v) {
            return _test(v);
        }

        [CanBeNull]
        public string Name(int v) {
            return _name(v);
        }

        public DisplayInputParams(JToken token) {
            switch (token) {
                case JArray tokenArray:
                    Take = tokenArray.Count;
                    _test = x => true;
                    _name = x => x >= 0 && x < tokenArray.Count ? ContentUtils.Translate(tokenArray[x]?.ToString()) : null;
                    break;
                case JObject tokenObject:
                    Take = int.MaxValue;
                    _test = x => tokenObject[x.ToInvariantString()] != null;
                    _name = x => ContentUtils.Translate(tokenObject[x.ToInvariantString()]?.ToString());
                    break;
                default:
                    Take = token?.Type == JTokenType.Integer ? (int)token : int.MaxValue;
                    _test = x => true;
                    _name = x => null;
                    break;
            }
        }

        [ContractAnnotation(@"
                => displayName:null, axes:null, buttons:null, povs:null, false;
                => displayName:notnull, axes:notnull, buttons:notnull, povs:notnull, true")]
        public static bool Get([NotNull] string guid, out string displayName, out DisplayInputParams axes, out DisplayInputParams buttons, out DisplayInputParams povs) {
            var file = FilesStorage.Instance.GetContentFile("Controllers", $"{guid}.json");
            if (file.Exists) {
                Logging.Debug($"Description found: {guid.ToUpperInvariant()}");

                try {
                    var jData = JsonExtension.Parse(File.ReadAllText(file.Filename));
                    displayName = ContentUtils.Translate(jData.GetStringValueOnly("name"));
                    axes = new DisplayInputParams(jData["axes"] ?? jData["axles"]);
                    buttons = new DisplayInputParams(jData["buttons"]);
                    povs = new DisplayInputParams(jData["pov"] ?? jData["povs"] ?? jData["pointOfViews"]);
                    return true;
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            } else {
                Logging.Debug($"Unknown device: {guid}");
            }

            displayName = null;
            axes = null;
            buttons = null;
            povs = null;
            return false;
        }
    }

    public class PlaceholderInputDevice : IDirectInputDevice {
        [CanBeNull]
        private readonly DisplayInputParams _axesP, _buttonsP, _povsP;

        public PlaceholderInputDevice([CanBeNull] string id, string displayName, int index) {
            Id = id;
            Index = index;
            OriginalIniIds = new List<int> { index };

            if (id != null && DisplayInputParams.Get(id, out var gotDisplayName, out _axesP, out _buttonsP, out _povsP)) {
                DisplayName = gotDisplayName;
            } else {
                DisplayName = displayName;
            }
        }

        [CanBeNull]
        public string Id { get; }

        public bool IsVirtual => true;

        public string DisplayName { get; }

        public int Index { get; set; }

        public IList<int> OriginalIniIds { get; }

        private readonly Dictionary<int, DirectInputAxle> _axes = new Dictionary<int, DirectInputAxle>();

        private readonly Dictionary<int, DirectInputButton> _buttons = new Dictionary<int, DirectInputButton>();

        private readonly Dictionary<Tuple<int, DirectInputPovDirection>, DirectInputPov> _direction
                = new Dictionary<Tuple<int, DirectInputPovDirection>, DirectInputPov>();

        public bool Same(IDirectInputDevice other) {
            return other != null && (Id == other.Id || DisplayName == other.DisplayName);
        }

        public DirectInputAxle GetAxle(int id) {
            return id < 0 ? null : _axes.TryGetValue(id, out var result) ? result : (_axes[id] = new DirectInputAxle(this, id, _axesP?.Name(id)));
        }

        public DirectInputButton GetButton(int id) {
            return id < 0 ? null : _buttons.TryGetValue(id, out var result) ? result : (_buttons[id] = new DirectInputButton(this, id, _buttonsP?.Name(id)));
        }

        public DirectInputPov GetPov(int id, DirectInputPovDirection direction) {
            var key = Tuple.Create(id, direction);
            return id < 0 ? null : _direction.TryGetValue(key, out var result) ? result : (_direction[key] = new DirectInputPov(this, id, direction, _povsP?.Name(id)));
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

        public List<DirectInputAxle> VisibleAxis { get; }
        public List<DirectInputButton> VisibleButtons { get; }
        public List<DirectInputPov> VisiblePovs { get; }

        private Joystick _joystick;

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

            GetCapabilities(out var displayName, out var axes, out var buttons, out var povs);
            DisplayName = displayName ?? device.InstanceName;
            Axis = axes;
            Buttons = buttons;
            Povs = povs;
            VisibleAxis = Axis.Where(x => x.IsVisible).ToList();
            VisibleButtons = Buttons.Where(x => x.IsVisible).ToList();
            VisiblePovs = Povs.Where(x => x.IsVisible).ToList();
        }

        private void GetCapabilities([CanBeNull] out string displayName, out DirectInputAxle[] axes,out DirectInputButton[] buttons,  out DirectInputPov[] povs) {
            if (DisplayInputParams.Get(Device.ProductGuid.ToString(), out displayName, out var axesP, out var buttonsP, out var povsP)) {
                axes = Axles(axesP.Take, axesP.Test, axesP.Name);
                buttons = Buttons(buttonsP.Take, buttonsP.Test, buttonsP.Name);
                povs = Povs(povsP.Take, povsP.Test, povsP.Name);
            } else {
                axes = Axles();
                buttons = Buttons();
                povs = Povs();
            }

            DirectInputAxle[] Axles(int? take = null, Func<int, bool> test = null, Func<int, string> name = null) {
                return Enumerable.Range(0, 8).Take(take ?? int.MaxValue)
                                 .Select(x => new DirectInputAxle(this, x, name?.Invoke(x)) {
                                     IsVisible = test?.Invoke(x) != false
                                 })
                                 .ToArray();
            }

            DirectInputButton[] Buttons(int? take = null, Func<int, bool> test = null, Func<int, string> name = null) {
                return Enumerable.Range(0, _joystick.Capabilities.ButtonCount)
                                 .Take(take?? int.MaxValue)
                                 .Select(x => new DirectInputButton(this, x, name?.Invoke(x)) {
                                     IsVisible = test?.Invoke(x) != false
                                 }).ToArray();
            }

            DirectInputPov[] Povs(int? take = null, Func<int, bool> test = null, Func<int, string> name = null) {
                return Enumerable.Range(0, _joystick.Capabilities.PovCount)
                                 .Take(take ?? int.MaxValue)
                                 .SelectMany(x => Enumerable.Range(0, 4).Select(y => new {
                                     Id = x,
                                     Direction = (DirectInputPovDirection)y
                                 }))
                                 .Select(x => new DirectInputPov(this, x.Id, x.Direction,
                                         name?.Invoke(x.Id) ?? (_joystick.Capabilities.PovCount == 1 ? "POV" : null)) {
                                             IsVisible = test?.Invoke(x.Id) != false
                                         }).ToArray();
            }
        }

        [CanBeNull]
        public static DirectInputDevice Create([NotNull] SlimDX.DirectInput.DirectInput directInput, DeviceInstance device, int iniId) {
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
            DisposeHelper.Dispose(ref _joystick);
        }
    }
}