using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class KunosCareerEventsManager : FileAcManager<KunosCareerEventObject> {
        public static bool OptionIgnoreSkippedEvents = false;

        private readonly string _kunosCareerId;
        private readonly KunosCareerObjectType _kunosCareerType;

        internal KunosCareerEventsManager(string kunosCareerId, KunosCareerObjectType kunosCareerType, AcDirectories directories) {
            _kunosCareerId = kunosCareerId;
            _kunosCareerType = kunosCareerType;
            Directories = directories;
        }

        private class NumericSortedAcWrapperObservableCollection : SortedAcWrapperObservableCollection {
            protected override int Compare(string x, string y) {
                return AlphanumComparatorFast.Compare(x, y);
            }
        }

        protected override AcWrapperObservableCollection CreateCollection() {
            return new NumericSortedAcWrapperObservableCollection();
        }

        /// <summary>
        /// Get event by number.
        /// </summary>
        /// <param name="number">Starts from 0</param>
        /// <returns>Event</returns>
        [CanBeNull]
        public KunosCareerEventObject GetByNumber(int number) {
            return GetById("event" + (number + 1));
        }

        protected override IEnumerable<AcPlaceholderNew> ScanOverride() {
            if (OptionIgnoreSkippedEvents) return base.ScanOverride();

            var entries = Directories.GetContentDirectories().Select(x => new {
                Name = Path.GetFileName(x)?.ToLowerInvariant(),
                Path = x
            }).ToList();

            return LinqExtension.RangeFrom(1)
                                .Select(x => @"event" + x)
                                .Select(x => entries.FirstOrDefault(y => y.Name == x))
                                .TakeWhile(x => x != null)
                                .Select(dir =>
                                        CreateAcPlaceholder(Directories.GetId(dir.Path), Directories.CheckIfEnabled(dir.Path)));
        }

        private static readonly Regex FilterRegex = new Regex(@"^event[1-9]\d*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected override bool Filter(string id, string filename) {
            return FilterRegex.IsMatch(id);
        }

        public override IAcDirectories Directories { get; }

        protected override KunosCareerEventObject CreateAcObject(string id, bool enabled) {
            var result = new KunosCareerEventObject(_kunosCareerId, _kunosCareerType, this, id, enabled);
            result.PropertyChanged += EventObject_PropertyChanged;
            return result;
        }

        private void EventObject_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(KunosCareerEventObject.HasErrors)) {
                EventHasErrorChanged?.Invoke(sender, EventArgs.Empty);
            }
        }

        public event EventHandler EventHasErrorChanged;
    }
}