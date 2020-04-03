using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using NAudio.CoreAudioApi;

namespace AcManager.Pages.AcSettings {
    public partial class AcSettingsAudio : ILoadableContent {
        private readonly AudioDevices _audioDevices = new AudioDevices();
        private IReadOnlyList<AudioDevice> _audioDevicesList;

        public async Task LoadAsync(CancellationToken cancellationToken) {
            try {
                _audioDevicesList = await Task.Run(() => GetAudioDevicesList());
            } catch (Exception e) {
                NonfatalError.NotifyBackground(AppStrings.AcSettings_Audio_CantGetTheListOfAudioDevices, e);
            }
        }

        public void Load() {
            try {
                _audioDevicesList = GetAudioDevicesList();
            } catch (Exception e) {
                NonfatalError.NotifyBackground(AppStrings.AcSettings_Audio_CantGetTheListOfAudioDevices, e);
            }
        }

        public void Initialize() {
            InitializeComponent();

            this.OnActualUnload(_audioDevices);
            _audioDevices.EndpointsChanged += OnEndpointsChanged;
            DataContext = new ViewModel(_audioDevicesList);

            InputBindings.AddRange(new[] {
                new InputBinding(Model.ShareCommand, new KeyGesture(Key.PageUp, ModifierKeys.Control)),
                new InputBinding(UserPresetsControl.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control))
            });

            if (_audioDevices == null) {
                DevicesHeading.Visibility = Visibility.Collapsed;
                DevicesPanel.Visibility = Visibility.Collapsed;
            }

            this.OnActualUnload(Model);
        }

        private void OnEndpointsChanged(object sender, EventArgs eventArgs) {
            try {
                _audioDevicesList = GetAudioDevicesList();
                Model.AudioOutputDevices = _audioDevicesList;
            } catch (Exception e) {
                NonfatalError.NotifyBackground(AppStrings.AcSettings_Audio_CantGetTheListOfAudioDevices, e);
            }
        }

        [CanBeNull]
        private IReadOnlyList<AudioDevice> GetAudioDevicesList() {
            var result = _audioDevices?.GetOutputDevices().ToList();
            if (result == null) return null;

            var current = AcSettingsHolder.Audio.EndPointName;
            if (!string.IsNullOrWhiteSpace(current) && result.All(x => x.DisplayName != current)) {
                var deviceName = Regex.Match(current, @"\((.+)\)").Groups[1].Value;
                result.Insert(0, new AudioDevice(current, string.IsNullOrWhiteSpace(deviceName) ? AppStrings.AcSettings_Audio_Unknown : deviceName,
                        @"%windir%\system32\mmres.dll,-3004", DeviceState.NotPresent));
            }

            return result;
        }

        private ViewModel Model => (ViewModel)DataContext;

        private class ViewModel : NotifyPropertyChanged, IDisposable {
            internal ViewModel(IReadOnlyList<AudioDevice> audioDevicesList) {
                AudioOutputDevices = audioDevicesList;
                Audio.SubscribeWeak(OnAudioSettingsChanged);
            }

            private void OnAudioSettingsChanged(object sender, PropertyChangedEventArgs args) {
                if (args.PropertyName == nameof(Audio.EndPointName)) {
                    RefreshSelectedAudioDevice();
                }
            }

            private IReadOnlyCollection<AudioDevice> _audioOutputDevices;

            [CanBeNull]
            public IReadOnlyCollection<AudioDevice> AudioOutputDevices {
                get => _audioOutputDevices;
                set => Apply(value, ref _audioOutputDevices, RefreshSelectedAudioDevice);
            }

            private void RefreshSelectedAudioDevice() {
                _selectedAudioDevice = AudioOutputDevices?.FirstOrDefault(x => x.DisplayName == Audio.EndPointName)
                        ?? AudioOutputDevices?.FirstOrDefault(x => x.IsDefault) ?? AudioOutputDevices?.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedAudioDevice));
            }

            private AudioDevice _selectedAudioDevice;

            [CanBeNull]
            public AudioDevice SelectedAudioDevice {
                get => _selectedAudioDevice;
                set => Apply(value, ref _selectedAudioDevice, () => Audio.EndPointName = value?.DisplayName);
            }

            public AudioSettings Audio => AcSettingsHolder.Audio;

            public IUserPresetable Presets => AcSettingsHolder.AudioPresets;

            private ICommand _shareCommand;

            public ICommand ShareCommand => _shareCommand ?? (_shareCommand = new AsyncCommand(Share));

            private async Task Share() {
                var data = Presets.ExportToPresetData();
                if (data == null) return;
                await SharingUiHelper.ShareAsync(SharedEntryType.AudioSettingsPreset,
                        Path.GetFileNameWithoutExtension(UserPresetsControl.GetCurrentFilename(Presets.PresetableKey)), null,
                        data);
            }

            public void Dispose() {
                Audio.UnsubscribeWeak(OnAudioSettingsChanged);
            }
        }
    }
}