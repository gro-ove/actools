using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Helpers.AcSettings;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using NAudio.CoreAudioApi;

namespace AcManager.Tools.Helpers {
    public class AudioDevices : IDisposable, IMMNotificationClient {
        // MMDevAPI doesn’t keep registered callbacks alive. If one gets collected before being unregistered (for example,
        // when settings search creates audio settings page just to look through it and drops it without disposing), the
        // next device notification calls into a dead COM wrapper and crashes the whole app. So, callback is registered
        // only when somebody actually listens, and kept referenced here until it’s unregistered.
        private static readonly HashSet<AudioDevices> Registered = new HashSet<AudioDevices>();

        private MMDeviceEnumerator _mm;
        private EventHandler _endpointsChanged;

        public AudioDevices() {
            _mm = new MMDeviceEnumerator();
        }

        private void SetRegistered(bool value) {
            lock (Registered) {
                if (_mm == null || Registered.Contains(this) == value) return;
                if (value) {
                    _mm.RegisterEndpointNotificationCallback(this);
                    Registered.Add(this);
                } else {
                    _mm.UnregisterEndpointNotificationCallback(this);
                    Registered.Remove(this);
                }
            }
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
                      .OrderBy(x => (int)x.State).ThenBy(x => x.DisplayName).Prepend(defaultDevice)
                      .Prepend(new AudioDevice(AudioSettings.DefaultDeviceName, "Default Audio Device",
                              "%windir%\\system32\\mmres.dll,0", DeviceState.Active));
        }

        public event EventHandler EndpointsChanged {
            add {
                _endpointsChanged += value;
                SetRegistered(true);
            }
            remove {
                _endpointsChanged -= value;
                if (_endpointsChanged == null) SetRegistered(false);
            }
        }

        private readonly Busy _reloadDevicesBusy = new Busy(true);

        private void ReloadDevices() {
            _reloadDevicesBusy.Yield(() => _endpointsChanged?.Invoke(this, EventArgs.Empty));
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
            SetRegistered(false);
            DisposeHelper.Dispose(ref _mm);
        }
    }
}