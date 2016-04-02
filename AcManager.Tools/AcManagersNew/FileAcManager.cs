using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    /// <summary>
    /// AcManager for files (but without watching).
    /// TODO: Combine with AcManagerNew since watchers are required anyway?
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class FileAcManager<T> : BaseAcManager<T>, IFileAcManager where T : AcCommonObject {
        public abstract BaseAcDirectories Directories { get; }

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            return Directories.GetSubDirectories().Where(Filter).Select(dir =>
                    CreateAcPlaceholder(LocationToId(dir), Directories.CheckIfEnabled(dir)));
        }

        public void Toggle(string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var wrapper = GetWrapperById(id);
            if (wrapper == null) throw new ArgumentException(@"ID is wrong", nameof(id));

            var currentLocation = ((AcCommonObject)wrapper.Value).Location;
            var newLocation = Path.Combine(wrapper.Value.Enabled ? Directories.DisabledDirectory : Directories.EnabledDirectory, wrapper.Value.Id);

            if (File.Exists(newLocation)) {
                throw new ToggleException("Place is taken");
            }

            try {
                FileUtils.Move(currentLocation, newLocation);
            } catch (Exception e) {
                throw new ToggleException(e.Message);
            }
        }

        public void Delete([NotNull]string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));
            var wrapper = GetWrapperById(id);
            if (wrapper == null) throw new ArgumentException(@"ID is wrong", nameof(id));
            FileUtils.Recycle(((AcCommonObject)wrapper.Value).Location);
            if (wrapper.IsLoaded && !File.Exists(((AcCommonObject)wrapper.Value).Location)) {
                ((AcObjectNew)wrapper.Value).Outdate();
            }
        }

        public string PrepareForAdditionalContent(string id, bool removeExisting) {
            var existing = GetById(id);
            var directory = existing == null ? Directories.GetLocation(id, true) : existing.Location;

            if (removeExisting && Directory.Exists(directory)) {
                FileUtils.Recycle(directory);
            }

            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            return directory;
        }
    }
}