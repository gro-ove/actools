using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    /// <summary>
    /// AcManager for files (but without watching).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class FileAcManager<T> : BaseAcManager<T>, IFileAcManager where T : AcCommonObject {
        protected FileAcManager() {
            SettingsHolder.Content.PropertyChanged += Content_PropertyChanged;
            Superintendent.Instance.Closing += Superintendent_Closing;
            Superintendent.Instance.SavingAll += SuperintendentSavingAll;
        }

        private void SuperintendentSavingAll(object sender, EventArgs e) {
            foreach (var item in InnerWrappersList.Select(x => x.Value).OfType<T>().Where(x => x.Changed)) {
               item.Save();
            }
        }

        private void Superintendent_Closing(object sender, Superintendent.ClosingEventArgs e) {
            foreach (var item in InnerWrappersList.Select(x => x.Value).OfType<T>().Where(x => x.Changed)) {
                Logging.Debug(item);
                e.Add(item.DisplayName);
            }
        }

        private void Content_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.ContentSettings.NewContentPeriod)) return;
            if (!IsScanned) return;
            foreach (var entry in LoadedOnly) {
                entry.CheckIfNew();
            }
        }

        [NotNull]
        protected virtual string LocationToId(string directory) {
            var name = Path.GetFileName(directory);
            if (name == null) throw new Exception(ToolsStrings.AcObject_CannotGetId);
            return name;
        }

        public abstract IAcDirectories Directories { get; }

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            return Directories.GetSubDirectories().Where(Filter).Select(dir =>
                    CreateAcPlaceholder(LocationToId(dir), Directories.CheckIfEnabled(dir)));
        }

        protected virtual void MoveInner(string id, string newId, string oldLocation, string newLocation, bool newEnabled) {
            FileUtils.Move(oldLocation, newLocation);
            
            var obj = CreateAndLoadAcObject(newId, newEnabled);
            obj.PreviousId = id;
            ReplaceInList(id, new AcItemWrapper(this, obj));
        }

        protected virtual void DeleteInner(string id, string location) {
            FileUtils.RecycleVisible(location);
            if (!FileUtils.Exists(location)) {
                RemoveFromList(id);
            }
        }

        public virtual void Rename(string id, string newId, bool newEnabled) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var wrapper = GetWrapperById(id);
            if (wrapper == null) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(id));

            var currentLocation = ((AcCommonObject)wrapper.Value).Location;
            var path = newEnabled ? Directories.EnabledDirectory : Directories.DisabledDirectory;
            if (path == null) throw new InformativeException(ToolsStrings.Common_CannotDo, ToolsStrings.AcObject_DisablingNotSupported_Commentary);

            var newLocation = Path.Combine(path, newId);
            if (FileUtils.Exists(newLocation)) throw new ToggleException(ToolsStrings.AcObject_PlaceIsTaken);

            try {
                MoveInner(id, newId, currentLocation, newLocation, newEnabled);
            } catch (Exception e) {
                throw new ToggleException(e.Message);
            }
        }

        public void Toggle(string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var wrapper = GetWrapperById(id);
            if (wrapper == null) {
                throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(id));
            }

            Rename(id, id, !wrapper.Value.Enabled);
        }

        public virtual void Delete([NotNull]string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var obj = GetById(id);
            if (obj == null) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(id));
            
            DeleteInner(id, obj.Location);
        }

        public virtual string PrepareForAdditionalContent([NotNull] string id, bool removeExisting) {
            if (id == null) throw new ArgumentNullException(nameof(id));

            var existing = GetById(id);
            var location = existing?.Location ?? Directories.GetLocation(id, true);

            if (removeExisting && FileUtils.Exists(location)) {
                FileUtils.RecycleVisible(location);

                if (FileUtils.Exists(location)) {
                    throw new OperationCanceledException(ToolsStrings.AcObject_CannotRemove);
                }
            }

            return location;
        }
    }
}