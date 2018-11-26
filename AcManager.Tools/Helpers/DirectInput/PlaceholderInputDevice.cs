using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.DirectInput {
    public class PlaceholderInputDevice : IDirectInputDevice {
        [CanBeNull]
        private readonly DisplayInputParams _axesP, _buttonsP, _povsP;

        public PlaceholderInputDevice([CanBeNull] string id, string displayName, int index) {
            Id = id;
            Index = index;
            IsController = DirectInputDeviceUtils.IsController(displayName);
            OriginalIniIds = new List<int> { index };

            if (id != null && DisplayInputParams.Get(id, out var gotDisplayName, out _axesP, out _buttonsP, out _povsP)) {
                DisplayName = gotDisplayName;
            } else {
                DisplayInputParams.Get(DirectInputDeviceUtils.GetXboxControllerGuid(), out _, out _axesP, out _buttonsP, out _povsP);
                DisplayName = displayName;
            }
        }

        [CanBeNull]
        public string Id { get; }

        public bool IsVirtual => true;

        public bool IsController { get; }

        public string DisplayName { get; }

        public int Index { get; set; }

        public IList<int> OriginalIniIds { get; }

        private readonly Dictionary<int, DirectInputAxle> _axes = new Dictionary<int, DirectInputAxle>();

        private readonly Dictionary<int, DirectInputButton> _buttons = new Dictionary<int, DirectInputButton>();

        private readonly Dictionary<Tuple<int, DirectInputPovDirection>, DirectInputPov> _povs
                = new Dictionary<Tuple<int, DirectInputPovDirection>, DirectInputPov>();

        public bool Same(IDirectInputDevice other) {
            return other != null && (Id == other.Id || DisplayName == other.DisplayName || Id == @"0" && other.IsController);
        }

        public DirectInputAxle GetAxle(int id) {
            if (id < 0) return null;
            if (_axes.TryGetValue(id, out var result)) return result;
            var axle = new DirectInputAxle(this, id);
            axle.SetDisplayParams(_axesP?.Name(id), true);
            return _axes[id] = axle;
        }

        public DirectInputButton GetButton(int id) {
            if (id < 0) return null;
            if (_buttons.TryGetValue(id, out var result)) return result;
            var axle = new DirectInputButton(this, id);
            axle.SetDisplayParams(_buttonsP?.Name(id), true);
            return _buttons[id] = axle;
        }

        public DirectInputPov GetPov(int id, DirectInputPovDirection direction) {
            if (id < 0) return null;
            var key = Tuple.Create(id, direction);
            if (_povs.TryGetValue(key, out var result)) return result;
            var axle = new DirectInputPov(this, id, direction);
            axle.SetDisplayParams(_povsP?.Name(id), true);
            return _povs[key] = axle;
        }

        public override string ToString() {
            return $"PlaceholderInputDevice({Id}:{DisplayName}, Ini={Index})";
        }
    }
}