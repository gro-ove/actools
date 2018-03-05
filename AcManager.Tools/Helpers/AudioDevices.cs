using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using NAudio.CoreAudioApi;

namespace AcManager.Tools.Helpers {
    public class AudioDevices : IDisposable, IMMNotificationClient {
        private MMDeviceEnumerator _mm;

        public AudioDevices() {
            _mm = new MMDeviceEnumerator();
            _mm.RegisterEndpointNotificationCallback(this);
        }

        [CanBeNull]
        private static AudioDevice ToAudioDevice(MMDevice endpoint) {
            if (endpoint == null) return null;
            return new AudioDevice(endpoint.FriendlyName, endpoint.DeviceFriendlyName, endpoint.IconPath, endpoint.State);
        }

        public IEnumerable<AudioDevice> GetOutputDevices() {
            if (_mm == null) return new AudioDevice[0];
            var defaultDevice = ToAudioDevice(_mm.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia));
            if (defaultDevice != null) {
                defaultDevice.IsDefault = true;
            }
            return _mm.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active | DeviceState.Unplugged | DeviceState.Disabled)
                      .Select(ToAudioDevice).Where(x => x != null && x.DisplayName != defaultDevice?.DisplayName)
                      .OrderBy(x => (int)x.State).ThenBy(x => x.DisplayName).Prepend(defaultDevice);
        }

        public event EventHandler EndpointsChanged;

        private readonly Busy _reloadDevicesBusy = new Busy(true);

        private void ReloadDevices() {
            _reloadDevicesBusy.Yield(() => EndpointsChanged?.Invoke(this, EventArgs.Empty));
        }

        void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState) {
            ReloadDevices();
        }

        void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId) {
            ReloadDevices();
        }

        void IMMNotificationClient.OnDeviceRemoved(string deviceId) {
            ReloadDevices();
        }

        void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) {
            ReloadDevices();
        }

        void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) {
            ReloadDevices();
        }

        public void Dispose() {
            _mm?.UnregisterEndpointNotificationCallback(this);
            DisposeHelper.Dispose(ref _mm);
        }
    }
}