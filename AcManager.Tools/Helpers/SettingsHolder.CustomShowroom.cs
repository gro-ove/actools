using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static partial class SettingsHolder {
        public class CustomShowroomSettings : NotifyPropertyChanged {
            internal CustomShowroomSettings() { }

            private bool? _useOldLiteShowroom;

            public bool UseOldLiteShowroom {
                get => _useOldLiteShowroom ?? (_useOldLiteShowroom = ValuesStorage.Get("Settings.CustomShowroomSettings.UseOldLiteShowroom", false)).Value;
                set {
                    if (Equals(value, _useOldLiteShowroom)) return;
                    _useOldLiteShowroom = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.UseOldLiteShowroom", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseFxaa;

            public bool LiteUseFxaa {
                get => _liteUseFxaa ?? (_liteUseFxaa = ValuesStorage.Get("Settings.CustomShowroomSettings.LiteUseFxaa", true)).Value;
                set {
                    if (Equals(value, _liteUseFxaa)) return;
                    _liteUseFxaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseFxaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseMsaa;

            public bool LiteUseMsaa {
                get => _liteUseMsaa ?? (_liteUseMsaa = ValuesStorage.Get("Settings.CustomShowroomSettings.LiteUseMsaa", false)).Value;
                set {
                    if (Equals(value, _liteUseMsaa)) return;
                    _liteUseMsaa = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseMsaa", value);
                    OnPropertyChanged();
                }
            }

            private bool? _liteUseBloom;

            public bool LiteUseBloom {
                get => _liteUseBloom ?? (_liteUseBloom = ValuesStorage.Get("Settings.CustomShowroomSettings.LiteUseBloom", true)).Value;
                set {
                    if (Equals(value, _liteUseBloom)) return;
                    _liteUseBloom = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.LiteUseBloom", value);
                    OnPropertyChanged();
                }
            }

            private string _showroomId;

            [CanBeNull]
            public string ShowroomId {
                get => _showroomId ?? (_showroomId = ValuesStorage.Get("Settings.CustomShowroomSettings.ShowroomId", @"showroom"));
                set {
                    value = value?.Trim();
                    if (Equals(value, _showroomId)) return;
                    _showroomId = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.ShowroomId", value);
                    OnPropertyChanged();
                }
            }

            private bool? _customShowroomInstead;

            public bool CustomShowroomInstead {
                get => _customShowroomInstead ??
                        (_customShowroomInstead = ValuesStorage.Get("Settings.CustomShowroomSettings.CustomShowroomInstead", true)).Value;
                set {
                    if (Equals(value, _customShowroomInstead)) return;
                    _customShowroomInstead = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.CustomShowroomInstead", value);
                    OnPropertyChanged();
                }
            }

            private bool? _customShowroomPreviews;

            public bool CustomShowroomPreviews {
                get => _customShowroomPreviews ??
                        (_customShowroomPreviews = ValuesStorage.Get("Settings.CustomShowroomSettings.CustomShowroomPreviews", true)).Value;
                set {
                    if (Equals(value, _customShowroomPreviews)) return;
                    _customShowroomPreviews = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.CustomShowroomPreviews", value);
                    OnPropertyChanged();
                }
            }

            private bool? _detailedExifForPreviews;

            public bool DetailedExifForPreviews {
                get => _detailedExifForPreviews ??
                        (_detailedExifForPreviews = ValuesStorage.Get("Settings.CustomShowroomSettings.DetailedExifForPreviews", true)).Value;
                set {
                    if (Equals(value, _detailedExifForPreviews)) return;
                    _detailedExifForPreviews = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.DetailedExifForPreviews", value);
                    OnPropertyChanged();
                }
            }

            private bool? _previewsRecycleOld;

            public bool PreviewsRecycleOld {
                get => _previewsRecycleOld ?? (_previewsRecycleOld = ValuesStorage.Get("Settings.CustomShowroomSettings.PreviewsRecycleOld", true)).Value;
                set {
                    if (Equals(value, _previewsRecycleOld)) return;
                    _previewsRecycleOld = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.PreviewsRecycleOld", value);
                    OnPropertyChanged();
                }
            }

            private bool? _smartCameraPivot;

            public bool SmartCameraPivot {
                get => _smartCameraPivot ?? (_smartCameraPivot = ValuesStorage.Get("Settings.CustomShowroomSettings.SmartCameraPivot", true)).Value;
                set {
                    if (Equals(value, _smartCameraPivot)) return;
                    _smartCameraPivot = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.SmartCameraPivot", value);
                    OnPropertyChanged();
                }
            }

            private bool? _alternativeControlScheme;

            public bool AlternativeControlScheme {
                get => _alternativeControlScheme ??
                        (_alternativeControlScheme = ValuesStorage.Get("Settings.CustomShowroomSettings.AlternativeControlScheme", false)).Value;
                set {
                    if (Equals(value, _alternativeControlScheme)) return;
                    _alternativeControlScheme = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.AlternativeControlScheme", value);
                    OnPropertyChanged();
                }
            }

            /*private string[] _paintShopSources;

            public string[] PaintShopSources {
                get => _paintShopSources ?? (_paintShopSources = ValuesStorage.GetStringList("Settings.CustomShowroomSettings.PaintShopSources", new[] {
                    "https://github.com/MadMat13/CM-Paint-Shop/"
                }).ToArray());
                set {
                    value = value.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                    if (Equals(value, _paintShopSources)) return;
                    _paintShopSources = value;
                    ValuesStorage.Set("Settings.CustomShowroomSettings.PaintShopSources", value);
                    OnPropertyChanged();
                }
            }*/
        }

        private static CustomShowroomSettings _customShowroom;
        public static CustomShowroomSettings CustomShowroom => _customShowroom ?? (_customShowroom = new CustomShowroomSettings());
    }
}