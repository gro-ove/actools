using System;
using JetBrains.Annotations;
using StringBasedFilter.TestEntries;

namespace StringBasedFilter {
    public interface ITestEntry {
        /// <summary>
        /// Override this tester with a tester which will treat data in a different
        /// way. Used for numerial test entries to add postfixes support.
        /// </summary>
        void Set([CanBeNull] ITestEntryFactory factory);

        bool Test([CanBeNull] string value);
        bool Test(double value);
        bool Test(bool value);
        bool Test(TimeSpan value);
        bool Test(DateTime value);
    }
}
