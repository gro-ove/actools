using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.AcManagersNew {
    public abstract class AcManagerFileSpecific<T> : AcManagerNew<T> where T : AcCommonObject {
        public virtual string SearchPattern => @"*";

        private Regex _regex;

        protected override bool Filter(string id, string filename) => SearchPattern == @"*" || (_regex ?? (_regex = new Regex(
                SearchPattern.Replace(@".", @"[.]").Replace(@"*", @".*").Replace(@"?", @"."))))
                .IsMatch(id);

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            return Directories.GetSubFiles(SearchPattern).Select(dir => {
                var id = LocationToId(dir);
                return Filter(id, dir) ? CreateAcPlaceholder(LocationToId(dir), Directories.CheckIfEnabled(dir)) : null;
            }).NonNull();
        }
    }
}