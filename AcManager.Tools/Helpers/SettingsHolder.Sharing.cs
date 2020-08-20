using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class SharingSettings : NotifyPropertyChanged {
            internal SharingSettings() { }

            private bool? _customIds;

            public bool CustomIds {
                get => _customIds ?? (_customIds = ValuesStorage.Get("Settings.SharingSettings.CustomIds", false)).Value;
                set {
                    if (Equals(value, _customIds)) return;
                    _customIds = value;
                    ValuesStorage.Set("Settings.SharingSettings.CustomIds", value);
                    OnPropertyChanged();
                }
            }

            private bool? _verifyBeforeSharing;

            public bool VerifyBeforeSharing {
                get => _verifyBeforeSharing ?? (_verifyBeforeSharing = ValuesStorage.Get("Settings.SharingSettings.VerifyBeforeSharing", true)).Value;
                set {
                    if (Equals(value, _verifyBeforeSharing)) return;
                    _verifyBeforeSharing = value;
                    ValuesStorage.Set("Settings.SharingSettings.VerifyBeforeSharing", value);
                    OnPropertyChanged();
                }
            }

            private bool? _showSharedDialog;

            public bool ShowSharedDialog {
                get => _showSharedDialog ?? (_showSharedDialog = ValuesStorage.Get("Settings.SharingSettings.ShowSharedDialog", true)).Value;
                set {
                    if (Equals(value, _showSharedDialog)) return;
                    _showSharedDialog = value;
                    ValuesStorage.Set("Settings.SharingSettings.ShowSharedDialog", value);
                    OnPropertyChanged();
                }
            }

            private bool? _copyLinkToClipboard;

            public bool CopyLinkToClipboard {
                get => _copyLinkToClipboard ?? (_copyLinkToClipboard = ValuesStorage.Get("Settings.SharingSettings.CopyLinkToClipboard", true)).Value;
                set {
                    if (Equals(value, _copyLinkToClipboard)) return;
                    _copyLinkToClipboard = value;
                    ValuesStorage.Set("Settings.SharingSettings.CopyLinkToClipboard", value);
                    OnPropertyChanged();
                }
            }

            private bool? _shareAnonymously;

            public bool ShareAnonymously {
                get => _shareAnonymously ?? (_shareAnonymously = ValuesStorage.Get("Settings.SharingSettings.ShareAnonymously", false)).Value;
                set {
                    if (Equals(value, _shareAnonymously)) return;
                    _shareAnonymously = value;
                    ValuesStorage.Set("Settings.SharingSettings.ShareAnonymously", value);
                    OnPropertyChanged();
                }
            }

            private bool? _shareWithoutName;

            public bool ShareWithoutName {
                get => _shareWithoutName ?? (_shareWithoutName = ValuesStorage.Get("Settings.SharingSettings.ShareWithoutName", false)).Value;
                set {
                    if (Equals(value, _shareWithoutName)) return;
                    _shareWithoutName = value;
                    ValuesStorage.Set("Settings.SharingSettings.ShareWithoutName", value);
                    OnPropertyChanged();
                }
            }

            private string _sharingName;

            [CanBeNull]
            public string SharingName {
                get => _sharingName ?? (_sharingName = ValuesStorage.Get<string>("Settings.SharingSettings.SharingName") ?? Drive.PlayerNameOnline);
                set {
                    value = value?.Trim();

                    if (value?.Length > 60) {
                        value = value.Substring(0, 60);
                    }

                    if (Equals(value, _sharingName)) return;
                    _sharingName = value;
                    ValuesStorage.Set("Settings.SharingSettings.SharingName", value);
                    OnPropertyChanged();
                }
            }
        }

        private static SharingSettings _sharing;
        public static SharingSettings Sharing => _sharing ?? (_sharing = new SharingSettings());
    }
}