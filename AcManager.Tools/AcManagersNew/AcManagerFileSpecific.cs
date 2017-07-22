using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.AcManagersNew {
    public abstract class AcManagerFileSpecific<T> : AcManagerNew<T> where T : AcCommonObject {
        public virtual string SearchPattern => @"*";

        public virtual string[] AttachedExtensions => null;

        private Regex _regex;

        protected virtual bool FilterId(string id) {
            return SearchPattern == @"*" || (_regex ?? (_regex = new Regex(
                    SearchPattern.Replace(@".", @"[.]").Replace(@"*", @".*").Replace(@"?", @"."))))
                    .IsMatch(id);
        }

        protected bool MultiDirectoryMode;

        protected override void OnCreatedIgnored(string filename) {
            if (!MultiDirectoryMode) return;

            try {
                foreach (var objectLocation in Directory.GetFiles(filename, SearchPattern, SearchOption.AllDirectories)) {
                    var objectId = Directories.GetId(objectLocation);
                    if (Filter(objectId, objectLocation)) {
                        GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Created, null, objectLocation);
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
                Logging.Debug(filename);
            }
        }

        protected override void OnDeletedIgnored(string filename, string pseudoId) {
            if (!MultiDirectoryMode) return;

            var prefix = pseudoId + '\\';
            for (var i = 0; i < InnerWrappersList.Count; i++) {
                var obj = InnerWrappersList[i];
                if (obj.Id.StartsWith(prefix)) {
                    var objectLocation = Directories.GetLocation(obj.Id, obj.Value.Enabled);
                    GetWatchingTask(objectLocation).AddEvent(WatcherChangeTypes.Deleted, null, objectLocation);
                }
            }
        }

        protected sealed override string GetLocationByFilename(string filename, out bool inner) {
            var result = Directories.GetLocationByFilename(filename, out inner);
            if (!inner || result == null) return result;

            var attached = AttachedExtensions;
            if (attached == null) return result;

            var special = attached.FirstOrDefault(x => result.EndsWith(x, StringComparison.OrdinalIgnoreCase));
            if (special == null) return result;

            inner = true;
            return result.ApartFromLast(special, StringComparison.OrdinalIgnoreCase) + FontObject.FontExtension;
        }

        protected sealed override bool Filter(string id, string filename) => FilterId(id) && File.Exists(filename);

        protected override IEnumerable<AcPlaceholderNew> ScanOverride() {
            return Directories.GetContentFiles(SearchPattern).Select(dir => {
                var id = Directories.GetId(dir);
                return FilterId(id) ? CreateAcPlaceholder(id, Directories.CheckIfEnabled(dir)) : null;
            }).NonNull();
        }
    }
}