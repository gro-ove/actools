using System;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.Objects {
    public partial class PpFilterObject : IAcObjectFullAuthorshipInformation, ICupSupportedObject {
        public CupContentType CupContentType => CupContentType.Filter;
        public bool IsCupUpdateAvailable => CupClient.Instance?.ContainsAnUpdate(CupContentType, Id, Version) ?? false;
        public CupClient.CupInformation CupUpdateInformation => CupClient.Instance?.GetInformation(CupContentType, Id);

        private void OnVersionChanged() {
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        void ICupSupportedObject.OnCupUpdateAvailableChanged() {
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        private bool _versionInfoApplied;

        private void ApplyVersionField(ref string target, string newValue, string propertyName) {
            newValue = newValue?.Trim().Or(null);
            if (target == newValue) return;
            target = newValue;
            OnPropertyChanged(propertyName);
        }

        private void ApplyVersionInfo(string content) {
            if (string.IsNullOrEmpty(content)) return;
            var i = content.IndexOf("[ABOUT]", StringComparison.Ordinal);
            var j = content.IndexOf("[", i + 8, StringComparison.Ordinal);
            if (i == -1) {
                ApplyVersionField(ref _version, null, nameof(Version));
                ApplyVersionField(ref _author, null, nameof(Author));
                ApplyVersionField(ref _url, null, nameof(Url));
            } else {
                var piece = IniFile.Parse(content.Substring(i, (j == -1 ? content.Length : j) - i))["ABOUT"];
                ApplyVersionField(ref _version, piece.GetNonEmpty("VERSION"), nameof(Version));
                ApplyVersionField(ref _author, piece.GetNonEmpty("AUTHOR"), nameof(Author));
                ApplyVersionField(ref _url, piece.GetNonEmpty("URL"), nameof(Url));
            }
        }

        private void ApplyVersionInfoToContent(string field, string value) {
            var content = Content;
            var i = content.IndexOf("[ABOUT]", StringComparison.Ordinal);
            var j = content.IndexOf("[", i + 8, StringComparison.Ordinal);
            if (i == -1) {
                Content = $"[ABOUT]\n{field}={value}\n\n{content}";
            } else {
                var piece = IniFile.Parse(content.Substring(i, (j == -1 ? content.Length : j) - i));
                piece["ABOUT"].Set(field, value);
                Content = content.Substring(0, i) + piece.Stringify() 
                        + (j == -1 ? string.Empty : "\n\n" + content.Substring(j));
            }
        }

        private void LoadVersionInfo() {
            if (_versionInfoApplied) return;
            _versionInfoApplied = true;
            PrepareForEditing();
            ApplyVersionInfo(Content);
        }

        private string _version;

        public string Version {
            get {
                LoadVersionInfo();
                return _version;
            }
            set => Apply(value?.Trim().Or(null), ref _version, () => {
                if (Loaded) {
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    ApplyVersionInfoToContent("VERSION", _version);
                }
                OnVersionChanged();
            });
        }

        private string _url;

        public string Url {
            get {
                LoadVersionInfo();
                return _url;
            }
            set => Apply(value, ref _url, () => {
                if (Loaded) {
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    ApplyVersionInfoToContent("URL", _version);
                    Changed = true;
                }
            });
        }

        private string _author;

        public string Author {
            get {
                LoadVersionInfo();
                return _author;
            }
            set => Apply(value, ref _author, () => {
                if (Loaded) {
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    ApplyVersionInfoToContent("AUTHOR", _version);
                    Changed = true;
                }
            });
        }

        public virtual string VersionInfoDisplay => this.GetVersionInfoDisplay();

        void ICupSupportedObject.SetValues(string author, string informationUrl, string version) {
            Author = author;
            Url = informationUrl;
            Version = version;
            SaveAsync().Ignore();
        }
    }
}