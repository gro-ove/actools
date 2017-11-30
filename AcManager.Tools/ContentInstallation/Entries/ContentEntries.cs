using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.ContentInstallation.Installators;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation.Entries {
    public abstract class ContentEntryBase : NotifyPropertyChanged {
        [NotNull]
        public string Id { get; }

        public string DisplayId => string.IsNullOrEmpty(Id) ? "N/A" : Id;

        /// <summary>
        /// Empty if object’s in root.
        /// </summary>
        [NotNull]
        public string EntryPath { get; }

        [NotNull]
        public string DisplayPath => string.IsNullOrEmpty(EntryPath) ? "N/A" : Path.DirectorySeparatorChar + EntryPath;

        [NotNull]
        public string Name { get; }

        [CanBeNull]
        public string Version { get; private set; }

        [CanBeNull]
        public byte[] IconData { get; protected set; }

        [CanBeNull]
        public string Description { get; }

        private bool _singleEntry;

        public bool SingleEntry {
            get => _singleEntry;
            set {
                if (Equals(value, _singleEntry)) return;
                _singleEntry = value;
                OnPropertyChanged();
            }
        }

        private bool _installAsGenericMod;

        public bool InstallAsGenericMod {
            get => _installAsGenericMod;
            set {
                if (Equals(value, _installAsGenericMod)) return;
                _installAsGenericMod = value;
                OnPropertyChanged();
            }
        }

        public bool GenericModSupported => GenericModSupportedByDesign && _installationParams?.CupType.HasValue != true;
        protected abstract bool GenericModSupportedByDesign { get; }

        [CanBeNull]
        public abstract string GenericModTypeName { get; }
        public abstract string NewFormat { get; }
        public abstract string ExistingFormat { get; }

        protected ContentEntryBase([NotNull] string path, [NotNull] string id,
                string name = null, string version = null, byte[] iconData = null, string description = null) {
            EntryPath = path ?? throw new ArgumentNullException(nameof(path));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            Version = version;
            IconData = iconData;
            Description = description;
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
                var oldValue = _selectedOption;
                _updateOptions = GetUpdateOptions().ToArray();
                _selectedOption = GetDefaultUpdateOption(_updateOptions);
                OnSelectedOptionChanged(oldValue, _selectedOption);
            }
        }

        protected void ResetUpdateOptions() {
            var oldValue = _selectedOption;
            _updateOptions = GetUpdateOptions().ToArray();
            _selectedOption = GetDefaultUpdateOption(_updateOptions);
            OnSelectedOptionChanged(oldValue, _selectedOption);
            OnPropertyChanged(nameof(UpdateOptions));
            OnPropertyChanged(nameof(SelectedOption));
        }

        private ContentInstallationParams _installationParams;

        public void SetInstallationParams([NotNull] ContentInstallationParams installationParams) {
            _installationParams = installationParams;
            if (_installationParams.CupType.HasValue && _installationParams.Version != null) {
                Version = _installationParams.Version;
            }
        }

        protected virtual UpdateOption GetDefaultUpdateOption(UpdateOption[] list) {
            return _installationParams?.PreferCleanInstallation == true
                    ? (list.FirstOrDefault(x => x.RemoveExisting) ?? list.FirstOrDefault())
                    : list.FirstOrDefault();
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
                var oldValue = _selectedOption;
                _selectedOption = value;
                OnSelectedOptionChanged(oldValue, value);
                OnPropertyChanged();
            }
        }

        public string GetNew(string displayName) {
            return string.Format(NewFormat, displayName);
        }

        public string GetExisting(string displayName) {
            return string.Format(ExistingFormat, displayName);
        }

        private UpdateOption[] _updateOptions;
        public IReadOnlyList<UpdateOption> UpdateOptions {
            get {
                InitializeOptions();
                return _updateOptions;
            }
        }

        protected virtual void OnSelectedOptionChanged(UpdateOption oldValue, UpdateOption newValue) {}

        protected virtual IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst) { RemoveExisting = true }
            };
        }

        protected bool MoveEmptyDirectories = false;

        protected virtual ICopyCallback GetCopyCallback([NotNull] string destination) {
            var filter = SelectedOption?.Filter;
            var path = EntryPath;
            return new CopyCallback(info => {
                var filename = info.Key;
                if (path != string.Empty && !FileUtils.IsAffected(path, filename)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, path);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            }, MoveEmptyDirectories ? (info => {
                var filename = info.Key;
                if (path != string.Empty && !FileUtils.IsAffected(path, filename)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, path);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            }) : (Func<IDirectoryInfo, string>)null);
        }

        [ItemCanBeNull]
        public async Task<InstallationDetails> GetInstallationDetails(CancellationToken cancellation) {
            var destination = await GetDestination(cancellation);
            return destination != null ?
                    new InstallationDetails(GetCopyCallback(destination),
                            SelectedOption?.CleanUp?.Invoke(destination)?.ToArray(),
                            SelectedOption?.BeforeTask,
                            SelectedOption?.AfterTask) {
                                OriginalEntry = this
                            } :
                    null;
        }

        [ItemCanBeNull]
        protected abstract Task<string> GetDestination(CancellationToken cancellation);

        private BetterImage.BitmapEntry? _icon;
        public BetterImage.BitmapEntry? Icon => IconData == null ? null :
                _icon ?? (_icon = BetterImage.LoadBitmapSourceFromBytes(IconData, 32));

        #region From Wrapper
        private bool _active = true;

        public bool Active {
            get => _active;
            set {
                if (Equals(value, _active)) return;
                _active = value;
                OnPropertyChanged();
            }
        }

        private bool _noConflictMode;

        public bool NoConflictMode {
            get => _noConflictMode;
            set {
                if (value == _noConflictMode) return;
                _noConflictMode = value;
                OnPropertyChanged();
            }
        }

        public async Task CheckExistingAsync() {
            var tuple = await GetExistingNameAndVersionAsync();
            IsNew = tuple == null;
            ExistingName = tuple?.Item1;
            ExistingVersion = tuple?.Item2;
            IsNewer = Version.IsVersionNewerThan(ExistingVersion);
            IsOlder = Version.IsVersionOlderThan(ExistingVersion);
        }

        [NotNull, ItemCanBeNull]
        protected abstract Task<Tuple<string, string>> GetExistingNameAndVersionAsync();

        public bool IsNew { get; set; }

        [CanBeNull]
        private string _existingVersion;

        [CanBeNull]
        public string ExistingVersion {
            get => _existingVersion;
            set {
                if (value == _existingVersion) return;
                _existingVersion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        [CanBeNull]
        private string _existingName;

        [CanBeNull]
        public string ExistingName {
            get => _existingName;
            set {
                if (value == _existingName) return;
                _existingName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        private bool _isNewer;

        public bool IsNewer {
            get => _isNewer;
            set {
                if (value == _isNewer) return;
                _isNewer = value;
                OnPropertyChanged();
            }
        }

        private bool _isOlder;

        public bool IsOlder {
            get => _isOlder;
            set {
                if (value == _isOlder) return;
                _isOlder = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName => IsNew ? GetNew(Name) : GetExisting(ExistingName ?? Name);
        #endregion
    }

    public abstract class ContentEntryBase<T> : ContentEntryBase where T : AcCommonObject {
        protected ContentEntryBase([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        protected sealed override bool GenericModSupportedByDesign => true;

        public abstract FileAcManager<T> GetManager();

        private T _acObjectNew;

        [ItemCanBeNull]
        public async Task<T> GetExistingAcObjectAsync() {
            return _acObjectNew ?? (_acObjectNew = await GetManager().GetByIdAsync(Id));
        }

        protected T GetExistingAcObject() {
            return _acObjectNew ?? (_acObjectNew = GetManager().GetById(Id));
        }

        protected override async Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            var obj = await GetExistingAcObjectAsync();
            return obj == null ? null : Tuple.Create(obj.DisplayName, (obj as IAcObjectVersionInformation)?.Version);
        }

        protected override async Task<string> GetDestination(CancellationToken cancellation) {
            var manager = GetManager();
            if (manager == null) return null;

            var destination = await manager.PrepareForAdditionalContentAsync(Id,
                    SelectedOption != null && SelectedOption.RemoveExisting);
            return cancellation.IsCancellationRequested ? null : destination;
        }
    }
}