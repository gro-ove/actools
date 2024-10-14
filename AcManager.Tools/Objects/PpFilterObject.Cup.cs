using System;
using System.Collections.Generic;
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

        private static readonly Dictionary<string, uint> KunosContent = new Dictionary<string, uint> {
            [@"b&w.ini"] = 0x2D15CDE5,
            [@"blue_steel.ini"] = 0xE7661BBC,
            [@"default.ini"] = 0x34A7F6E4,
            [@"default_bright.ini"] = 0x1A9A4B76,
            [@"default_dark.ini"] = 0xCA6FF8EF,
            [@"movie.ini"] = 0x886391E4,
            [@"natural.ini"] = 0xA05EB1B,
            [@"photographic.ini"] = 0xFBB4DB5,
            [@"sepia.ini"] = 0x6559CA3,
            [@"vintage.ini"] = 0x14C74CCE,
        };

        private static unsafe uint GetKey(string s) {
            var k = s.GetHashCode();
            return *(uint*)&k;
        }

        private void ApplyVersionInfo(string content) {
            if (string.IsNullOrEmpty(content)) return;

            if (KunosContent.TryGetValue(Id, out var key) && key == GetKey(content)) {
                _author = AuthorKunos;
                _version = null;
                _url = null;
                return;
            }

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
            PrepareForEditing();
            _versionInfoApplied = true;
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