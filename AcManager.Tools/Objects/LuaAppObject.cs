using System.Collections;
using System.Collections.Generic;
using System.IO;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Tools.Objects {
    public class LuaAppObject : AcIniObject, IAcObjectFullAuthorshipInformation, ICupSupportedObject, IDraggable {
        private string _appIcon;

        public string AppIcon {
            get => _appIcon;
            set => Apply(value, ref _appIcon);
        }

        public LuaAppObject(IFileAcManager manager, string id, bool enabled) : base(manager, id, enabled) { }

        public override string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        protected override void InitializeLocations() {
            base.InitializeLocations();
            IniFilename = Path.Combine(Location, "manifest.ini");
            AppIcon = Path.Combine(Location, "icon.png");
        }

        private string _author;

        public string Author {
            get => _author;
            set => Apply(string.IsNullOrWhiteSpace(value) ? null : value, ref _author, () => {
                if (Loaded) {
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    Changed = true;
                }
            });
        }

        private string _version;

        public string Version {
            get => _version;
            set => Apply(string.IsNullOrWhiteSpace(value) ? null : value, ref _version, () => {
                if (Loaded) {
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    Changed = true;
                }
            });
        }

        private string _url;

        public string Url {
            get => _url;
            set => Apply(string.IsNullOrWhiteSpace(value) ? null : value, ref _url, () => {
                if (Loaded) {
                    OnPropertyChanged(nameof(VersionInfoDisplay));
                    Changed = true;
                }
            });
        }

        private string _description;

        public string Description {
            get => _description;
            set => Apply(string.IsNullOrWhiteSpace(value) ? null : value, ref _description, () => {
                if (Loaded) {
                    Changed = true;
                }
            });
        }

        public virtual string VersionInfoDisplay => this.GetVersionInfoDisplay();

        protected override void LoadData(IniFile ini) {
            Name = ini["ABOUT"].GetPossiblyEmpty("NAME");
            Author = ini["ABOUT"].GetPossiblyEmpty("AUTHOR");
            Version = ini["ABOUT"].GetPossiblyEmpty("VERSION");
            Url = ini["ABOUT"].GetPossiblyEmpty("URL");
            Description = ini["ABOUT"].GetPossiblyEmpty("DESCRIPTION");
        }

        protected override void SaveData(IniFile ini) {
            ini["ABOUT"].Set("NAME", Name);
            ini["ABOUT"].Set("AUTHOR", Author);
            ini["ABOUT"].Set("VERSION", Version);
            ini["ABOUT"].Set("URL", Url);
            ini["ABOUT"].Set("DESCRIPTION", Description);
        }

        protected override void ResetData() {
            base.ResetData();
            Version = null;
            Author = null;
            Url = null;
            Description = null;
        }

        public CupContentType CupContentType => CupContentType.LuaApp;

        public bool IsCupUpdateAvailable => CupClient.Instance?.ContainsAnUpdate(CupContentType, Id.ToLowerInvariant(), Version) ?? false;

        public CupClient.CupInformation CupUpdateInformation => CupClient.Instance?.GetInformation(CupContentType, Id.ToLowerInvariant());

        void ICupSupportedObject.OnCupUpdateAvailableChanged() {
            OnPropertyChanged(nameof(IsCupUpdateAvailable));
            OnPropertyChanged(nameof(CupUpdateInformation));
        }

        void ICupSupportedObject.SetValues(string author, string informationUrl, string version) {
            Author = author;
            Url = informationUrl;
            Version = version;
            SaveAsync();
        }

        public const string DraggableFormat = "Data-LuaAppObject";
        string IDraggable.DraggableFormat => DraggableFormat;

        #region Packing
        public override bool CanBePacked() {
            return true;
        }

        public class LuaAppPackerParams : AcCommonObjectPackerParams { }

        private class LuaAppPacker : AcCommonObjectPacker<LuaAppObject, LuaAppPackerParams> {
            protected override string GetBasePath(LuaAppObject t) {
                return $"apps/lua/{t.Id}";
            }

            protected override IEnumerable PackOverride(LuaAppObject t) {
                yield return Add("*");
            }

            protected override PackedDescription GetDescriptionOverride(LuaAppObject t) {
                return new PackedDescription(t.Id, t.Name,
                        new Dictionary<string, string> {
                            ["Version"] = t.Version,
                            ["Made by"] = t.Author,
                            ["Webpage"] = t.Url,
                        }, LuaAppsManager.Instance.Directories.GetMainDirectory(), true);
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new LuaAppPacker();
        }
        #endregion
    }
}