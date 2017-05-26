using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject : AcObjectNew {
        public const string AuthorKunos = "Kunos";

        public readonly IFileAcManager FileAcManager;

        public virtual bool NeedsMargin => false;

        // ReSharper disable once NotNullMemberIsNotInitialized
        protected AcCommonObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            FileAcManager = manager;

            var typeName = GetType().Name;
            _isFavouriteKey = $"{typeName}:{id}:favourite";
            _ratingKey = $"{typeName}:{id}:rating";
        }

        public static void MoveRatings(Type type, string oldId, string newId, bool keepOld) {
            var typeName = type.Name;
            var isFavouriteOldKey = $"{typeName}:{oldId}:favourite";
            var ratingOldKey = $"{typeName}:{oldId}:rating";
            var isFavouriteNewKey = $"{typeName}:{newId}:favourite";
            var ratingNewKey = $"{typeName}:{newId}:rating";

            if (RatingsStorage.Contains(isFavouriteOldKey)) {
                RatingsStorage.SetString(isFavouriteNewKey, RatingsStorage.GetString(isFavouriteOldKey));
                if (!keepOld) {
                    RatingsStorage.Remove(isFavouriteOldKey);
                }
            }

            if (RatingsStorage.Contains(ratingOldKey)) {
                RatingsStorage.SetString(ratingNewKey, RatingsStorage.GetString(ratingOldKey));
                if (!keepOld) {
                    RatingsStorage.Remove(ratingOldKey);
                }
            }
        }

        public static void MoveRatings<T>(string oldId, string newId, bool keepOld) {
            MoveRatings(typeof(T), oldId, newId, keepOld);
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

        private bool _isNew;

        public bool IsNew {
            get => _isNew;
            set {
                if (Equals(value, _isNew)) return;
                _isNew = value;
                OnPropertyChanged();
            }
        }

        public double AgeInDays => (DateTime.Now - CreationDateTime).TotalDays;

        public override void Reload() {
            ClearErrors();
            LoadOrThrow();
            Changed = false;
        }

        public DateTime CreationDateTime { get; private set; }

        public void CheckIfNew() {
            try {
                IsNew = DateTime.Now - CreationDateTime < SettingsHolder.Content.NewContentPeriod.TimeSpan;
            } catch (Exception) {
                IsNew = false;
            }
        }

        protected bool Loaded { get; private set; }

        protected abstract void LoadOrThrow();

        public sealed override void Load() {
            InitializeLocationsOnce();

            ClearErrors();
            Changed = false;

            CreationDateTime = File.GetCreationTime(Location);
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
                Manager.UpdateList();
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

        private bool _changed;

        public virtual bool Changed {
            get => _changed;
            protected set {
                if (value == _changed || !Loaded) return;
                _changed = value;
                OnPropertyChanged(nameof(Changed));
                _saveCommand?.RaiseCanExecuteChanged();
            }
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

        #region Rating
        private static Storage _ratingsStorage;

        private static Storage RatingsStorage
            => _ratingsStorage ?? (_ratingsStorage = new Storage(FilesStorage.Instance.GetFilename("Progress", "Ratings.data")));

        private readonly string _isFavouriteKey;
        private bool? _isFavourite;

        public bool IsFavourite {
            get => _isFavourite ?? (_isFavourite = RatingsStorage.GetBool(_isFavouriteKey)).Value;
            set {
                if (Equals(value, _isFavourite)) return;
                _isFavourite = value;

                if (value) {
                    RatingsStorage.Set(_isFavouriteKey, true);
                } else {
                    RatingsStorage.Remove(_isFavouriteKey);
                }

                OnPropertyChanged();
            }
        }

        private readonly string _ratingKey;
        private bool _ratingLoaded;
        private double? _rating;

        public double? Rating {
            get {
                if (!_ratingLoaded) {
                    _ratingLoaded = true;
                    _rating = RatingsStorage.GetDoubleNullable(_ratingKey);
                }
                return _rating;
            }
            set {
                if (Equals(value, _rating)) return;
                _rating = value;
                _ratingLoaded = true;

                if (value.HasValue) {
                    RatingsStorage.Set(_ratingKey, value.Value);
                } else {
                    RatingsStorage.Remove(_ratingKey);
                }

                OnPropertyChanged();
            }
        }
        #endregion

        public abstract void Save();

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
            BetterImage.ReloadImage((string)GetType().GetProperty(propertyName)?.GetValue(this, null));
        }

        protected void OnImageChangedValue(string filename) {
            BetterImage.ReloadImage(filename);
        }
    }
}
