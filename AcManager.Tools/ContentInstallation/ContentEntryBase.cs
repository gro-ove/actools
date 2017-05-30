using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public abstract class ContentEntryBase : NotifyPropertyChanged {
        [NotNull]
        public string Id { get; }

        /// <summary>
        /// Empty if object’s in root.
        /// </summary>
        [NotNull]
        public string EntryPath { get; }

        [NotNull]
        public string DisplayPath => Path.DirectorySeparatorChar + EntryPath;

        [NotNull]
        public string Name { get; }

        [CanBeNull]
        public string Version { get; }

        [CanBeNull]
        public byte[] IconData { get; }

        public abstract string NewFormat { get; }

        public abstract string ExistingFormat { get; }

        protected ContentEntryBase([NotNull] string path, [NotNull] string id, string name = null, string version = null,
                byte[] iconData = null) {
            EntryPath = path ?? throw new ArgumentNullException(nameof(path));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            Version = version;
            IconData = iconData;
        }

        private bool _installEntry;

        public bool InstallEntry {
            get => _installEntry;
            set {
                if (Equals(value, _installEntry)) return;
                _installEntry = value;
                OnPropertyChanged();
            }
        }

        private void InitializeOptions() {
            if (_updateOptions == null) {
                _updateOptions = GetUpdateOptions().ToArray();
                _selectedOption = GetDefaultUpdateOption(_updateOptions);
            }
        }

        protected virtual UpdateOption GetDefaultUpdateOption(UpdateOption[] list) {
            return list.FirstOrDefault();
        }

        private UpdateOption _selectedOption;

        [CanBeNull]
        public UpdateOption SelectedOption {
            get {
                InitializeOptions();
                return _selectedOption;
            }
            set {
                if (Equals(value, _selectedOption)) return;
                _selectedOption = value;
                OnPropertyChanged();
            }
        }

        private UpdateOption[] _updateOptions;
        public IReadOnlyList<UpdateOption> UpdateOptions {
            get {
                InitializeOptions();
                return _updateOptions;
            }
        }

        public string GetNew(string displayName) {
            return string.Format(NewFormat, displayName);
        }

        public string GetExisting(string displayName) {
            return string.Format(ExistingFormat, displayName);
        }

        public abstract IFileAcManager GetManager();

        protected virtual IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst) { RemoveExisting = true }
            };
        }

        [ItemCanBeNull]
        protected virtual async Task<string> GetDestination(CancellationToken cancellation) {
            var manager = GetManager();
            if (manager == null) return null;

            var destination = await manager.PrepareForAdditionalContentAsync(Id,
                    SelectedOption != null && SelectedOption.RemoveExisting);
            return cancellation.IsCancellationRequested ? null : destination;
        }

        protected virtual CopyCallback GetCopyCallback([NotNull] string destination) {
            var filter = SelectedOption?.Filter;
            return fileInfo => {
                var filename = fileInfo.Key;
                if (EntryPath != string.Empty && !FileUtils.IsAffected(EntryPath, filename)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, EntryPath);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            };
        }

        [ItemCanBeNull]
        public async Task<InstallationDetails> GetInstallationDetails(CancellationToken cancellation) {
            var destination = await GetDestination(cancellation);
            return destination != null ?
                    new InstallationDetails(GetCopyCallback(destination),
                            SelectedOption?.CleanUp?.Invoke(destination)?.ToArray()) :
                    null;
        }
    }

    public class CarContentEntry : ContentEntryBase {
        public CarContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_CarNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_CarExisting;

        public override IFileAcManager GetManager() {
            return CarsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return x != @"ui\ui_car.json" && x != @"ui\brand.png" && x != @"logo.png" && (!x.StartsWith(@"skins\") || !x.EndsWith(@"\ui_skin.json"));
            }

            bool PreviewsFilter(string x) {
                return !x.StartsWith(@"skins\") || !x.EndsWith(@"\preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation) { Filter = UiFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepSkinsPreviews) { Filter = PreviewsFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformationAndSkinsPreviews) { Filter = x => UiFilter(x) && PreviewsFilter(x) }
            });
        }
    }

    public class TrackContentEntry : ContentEntryBase {
        public TrackContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_TrackNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_TrackExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return x != @"ui_skin.json";
            }

            bool PreviewFilter(string x) {
                return x != @"preview.jpg";
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation) { Filter = UiFilter },
                new UpdateOption(ToolsStrings.Installator_KeepSkinPreview) { Filter = PreviewFilter },
                new UpdateOption(ToolsStrings.Installator_KeepUiInformationAndSkinPreview) { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }

        public override IFileAcManager GetManager() {
            return TracksManager.Instance;
        }
    }

    public class ShowroomContentEntry : ContentEntryBase {
        public ShowroomContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_ShowroomNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_ShowroomExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return x != @"ui\ui_showroom.json";
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = UiFilter }
            });
        }

        public override IFileAcManager GetManager() {
            return ShowroomsManager.Instance;
        }
    }

    public class CarSkinContentEntry : ContentEntryBase {
        public CarSkinContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_CarSkinNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_CarSkinExisting;

        public override IFileAcManager GetManager() {
            throw new NotImplementedException();
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !x.StartsWith(@"ui\") || !x.EndsWith(@"\ui_track.json") && !x.EndsWith(@"\preview.png") && !x.EndsWith(@"\outline.png");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = UiFilter }
            });
        }
    }

    public class FontContentEntry : ContentEntryBase {
        public FontContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_FontNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_FontExisting;

        public override IFileAcManager GetManager() {
            return FontsManager.Instance;
        }

        protected override CopyCallback GetCopyCallback(string destination) {
            var bitmapExtension = Path.GetExtension(EntryPath);
            var mainEntry = EntryPath.ApartFromLast(bitmapExtension) + FontObject.FontExtension;

            return info => {
                if (FileUtils.ArePathsEqual(info.Key, mainEntry)) {
                    return destination;
                }

                if (FileUtils.ArePathsEqual(info.Key, EntryPath)) {
                    return destination.ApartFromLast(FontObject.FontExtension) + bitmapExtension;
                }

                return null;
            };
        }
    }

    public class WeatherContentEntry : ContentEntryBase {
        public WeatherContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_WeatherNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_WeatherExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool PreviewFilter(string x) {
                return x != @"preview.jpg";
            }

            IEnumerable<string> RemoveClouds(string location) {
                yield return Path.Combine(location, "clouds");
            }

            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst) { RemoveExisting = true },
                new UpdateOption("Update Everything, Remove Existing Clouds If Any"){ CleanUp = RemoveClouds },
                new UpdateOption("Keep Preview"){ Filter = PreviewFilter },
                new UpdateOption("Update Everything, Remove Existing Clouds If Any & Keep Preview"){ Filter = PreviewFilter, CleanUp = RemoveClouds },
            };
        }

        protected override UpdateOption GetDefaultUpdateOption(UpdateOption[] list) {
            return list.ElementAtOrDefault(2) ?? base.GetDefaultUpdateOption(list);
        }

        public override IFileAcManager GetManager() {
            return WeatherManager.Instance;
        }
    }

    public class PpFilterContentEntry : ContentEntryBase {
        public PpFilterContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => "New PP-filter {0}";
        public override string ExistingFormat => "New version for PP-filter {0}";

        public override IFileAcManager GetManager() {
            return PpFiltersManager.Instance;
        }
    }

    public class DriverModelContentEntry : ContentEntryBase {
        public DriverModelContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => "New driver model {0}";
        public override string ExistingFormat => "New version for driver model {0}";

        public override IFileAcManager GetManager() {
            return DriverModelsManager.Instance;
        }
    }
}
