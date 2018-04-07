using System.Collections.Generic;
using System.Windows.Media;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class OnlineSourceInformation : NotifyPropertyChanged {
        private string _label;
        private Color? _color;
        private bool _hidden;
        private bool _excluded;

        public OnlineSourceInformation() { }

        public OnlineSourceInformation(string id) {
            switch (id) {
                case FileBasedOnlineSources.HiddenKey:
                    SetHiddenInner(true);
                    SetExcludedInner(true);
                    break;
            }
        }

        public string Label {
            get => _label;
            set => SetLabelInner(value);
        }

        private void SetLabelInner(string value) {
            if (string.IsNullOrWhiteSpace(value)) value = null;
            if (Equals(value, _label)) return;
            _label = value;
            OnPropertyChanged(nameof(Label));
            FileBasedOnlineSources.Instance.RaiseLabelUpdated();
        }

        public Color? Color {
            get => _color;
            set => SetColorInner(value);
        }

        private void SetColorInner(Color? value) {
            if (Equals(value, _color)) return;
            _color = value;
            OnPropertyChanged(nameof(Color));
            FileBasedOnlineSources.Instance.RaiseLabelUpdated();
        }

        public bool Hidden {
            get => _hidden;
            set {
                SetHiddenInner(value);
                FileBasedOnlineSources.Instance.RaiseUpdated();
            }
        }

        private void SetHiddenInner(bool value) {
            if (Equals(value, _hidden)) return;
            _hidden = value;
            OnPropertyChanged(nameof(Hidden));
        }

        public bool Excluded {
            get => _excluded;
            set => SetExcludedInner(value);
        }

        private void SetExcludedInner(bool value) {
            if (Equals(value, _excluded)) return;
            _excluded = value;
            OnPropertyChanged(nameof(Excluded));
        }

        internal void Export(IList<string> list) {
            if (!string.IsNullOrWhiteSpace(Label)) {
                list.Add($@"# label: {Label}");
            }

            if (Color.HasValue) {
                list.Add($@"# color: {Color.Value.ToHexString()}");
            }

            if (Hidden || Excluded) {
                list.Add($@"# hidden: {(Hidden ? @"yes" : @"no")}");
                list.Add($@"# excluded: {(Excluded ? @"yes" : @"no")}");
            }
        }

        internal void Assign([CanBeNull] IEnumerable<KeyValuePair<string, string>> values) {
            string label = null;
            Color? color = null;
            var hidden = false;
            var excluded = false;

            if (values != null) {
                foreach (var pair in values) {
                    switch (pair.Key) {
                        case "label":
                            label = pair.Value;
                            break;

                        case "color":
                            color = pair.Value?.ToColor();
                            break;

                        case "hidden":
                            hidden = FlexibleParser.ParseBool(pair.Value, false);
                            break;

                        case "excluded":
                            excluded = FlexibleParser.ParseBool(pair.Value, false);
                            break;
                    }
                }
            }

            SetLabelInner(label);
            SetColorInner(color);
            SetHiddenInner(hidden);
            SetExcludedInner(excluded);
        }
    }
}