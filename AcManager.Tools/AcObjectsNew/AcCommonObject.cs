using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject : AcObjectNew {
        public static readonly string AuthorKunos = "Kunos";

        public readonly IFileAcManager FileAcManager;

        public virtual bool NeedsMargin => false;

        // ReSharper disable once NotNullMemberIsNotInitialized
        protected AcCommonObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            FileAcManager = manager;
        }

        [NotNull]
        public string Location { get; protected set; }

        protected virtual string GetLocation() {
            return FileAcManager.Directories.GetLocation(Id, Enabled);
        }

        private bool _locationsInitialized;

        protected void InitializeLocationsOnce() {
            if (_locationsInitialized) return;
            InitializeLocations();
            _locationsInitialized = true;
        }

        protected virtual void InitializeLocations() {
            Location = GetLocation();
        }

        public override void Reload() {
            ClearErrors();
            LoadOrThrow();
            Changed = false;
        }

        protected bool Loaded { get; private set; }

        protected abstract void LoadOrThrow();

        protected virtual DateTime GetCreationDateTime() {
            return File.GetCreationTime(Location);
        }

        public sealed override void Load() {
            InitializeLocationsOnce();

            ClearErrors();
            Changed = false;

            CreationDateTime = GetCreationDateTime();
            CheckIfNew();

            try {
                LoadOrThrow();
            } catch (AcErrorException e) {
                AddError(e.AcError);
#if !DEBUG
            } catch (Exception e) {
                AddError(AcErrorType.Load_Base, e);
#endif
            } finally {
                Loaded = true;
            }
        }

        public void SortAffectingValueChanged() {
            if (Loaded) {
                Manager.UpdateList(false);
            }
        }

        public abstract bool HasData { get; }

        public override string Name {
            get => base.Name;
            protected set {
                value = value?.Trim();

                if (Equals(value, base.Name)) return;
                base.Name = value;

                ErrorIf(string.IsNullOrEmpty(value) && HasData, AcErrorType.Data_ObjectNameIsMissing);

                if (Loaded) {
                    SortAffectingValueChanged();
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(NameEditable));
                    OnPropertyChanged(nameof(DisplayName));
                    Changed = true;
                }
            }
        }

        [CanBeNull]
        public virtual string NameEditable {
            get => Name ?? Id;
            set => Name = value;
        }

        private bool _ignoreChanges;
        private bool _changed;

        public virtual bool Changed {
            get => _changed;
            protected set {
                if (value == _changed || !Loaded || _ignoreChanges) return;
                _changed = value;
                OnPropertyChanged(nameof(Changed));
                _saveCommand?.RaiseCanExecuteChanged();
            }
        }

        protected override void OnPropertyChanged(string propertyName = null) {
            if (_ignoreChanges) return;
            base.OnPropertyChanged(propertyName);
        }

        public IDisposable IgnoreChanges() {
            _ignoreChanges = true;
            return new ActionAsDisposable(() => _ignoreChanges = false);
        }

        private int? _year;

        public virtual int? Year {
            get => _year;
            set {
                if (value == 0) value = null;
                if (Equals(value, _year)) return;
                _year = value;

                if (_year.HasValue && Name != null) {
                    var inName = AcStringValues.GetYearFromName(Name);
                    if (inName.HasValue && inName.Value != _year.Value) {
                        Name = AcStringValues.NameReplaceYear(Name, _year.Value);
                    }
                }

                if (Loaded) {
                    OnPropertyChanged(nameof(Year));
                    Changed = true;
                }
            }
        }

        public abstract Task SaveAsync();

        public virtual void ViewInExplorer() {
            if (File.GetAttributes(Location).HasFlag(FileAttributes.Directory)) {
                WindowsHelper.ViewDirectory(Location);
            } else {
                WindowsHelper.ViewFile(Location);
            }
        }

        protected virtual Task ToggleOverrideAsync() {
            return FileAcManager.ToggleAsync(Id);
        }

        protected virtual Task DeleteOverrideAsync() {
            return FileAcManager.DeleteAsync(Id);
        }

        public async Task<bool> ToggleAsync() {
            try {
                await ToggleOverrideAsync();
                return true;
            } catch (ToggleException ex) {
                NonfatalError.Notify(string.Format(ToolsStrings.AcObject_CannotToggleExt, ex.Message), ToolsStrings.AcObject_CannotToggle_Commentary);
                return false;
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotToggle, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
                return false;
            }
        }

        public async Task<bool> RenameAsync(string newId) {
            try {
                newId = newId?.Trim();
                if (string.IsNullOrWhiteSpace(newId)) return false;

                await FileAcManager.RenameAsync(Id, newId, Enabled);
                return true;
            } catch (ToggleException ex) {
                NonfatalError.Notify(string.Format(ToolsStrings.AcObject_CannotChangeIdExt, ex.Message), ToolsStrings.AcObject_CannotToggle_Commentary);
                return false;
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotChangeId, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
                return false;
            }
        }

        public async Task<bool> CloneAsync(string newId) {
            try {
                await FileAcManager.CloneAsync(Id, newId, Enabled);
                return true;
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotClone, ToolsStrings.AcObject_CannotClone_Commentary, ex);
                return false;
            }
        }

        public async Task<bool> DeleteAsync() {
            try {
                if (!SettingsHolder.Content.DeleteConfirmation ||
                        ModernDialog.ShowMessage(string.Format("Are you sure you want to move {0} to the Recycle Bin?", DisplayName), "Are You Sure?",
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    await DeleteOverrideAsync();
                }
                return true;
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotDelete, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
                return false;
            }
        }

        public virtual bool HandleChangedFile(string filename) => false;

        /// <summary>
        /// Using for remembering selected item when its ID is changed.
        /// </summary>
        public string PreviousId { get; internal set; }

        protected void OnImageChanged(string propertyName) {
            OnPropertyChanged(propertyName);
            BetterImage.Refresh((string)GetType().GetProperty(propertyName)?.GetValue(this, null));
        }

        protected void OnImageChangedValue(string filename) {
            BetterImage.Refresh(filename);
        }
    }
}
