using FirstFloor.ModernUI.Presentation;
using NAudio.CoreAudioApi;

namespace AcManager.Tools.Helpers {
    public sealed class AudioDevice : Displayable {
        public AudioDevice(string name, string deviceName, string iconPath, DeviceState state) {
            DisplayName = name;
            DeviceName = deviceName;
            State = state;
            IconPath = iconPath;
        }

        public string DeviceName { get; }
        public string IconPath { get; }
        public DeviceState State { get; }

        private bool _isDefault;

        public bool IsDefault {
            get => _isDefault;
            set => Apply(value, ref _isDefault);
        }
    }
}