using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Filters.TestEntries {
    public static class ExtraTestEntries {
        public static void Initialize() {
            TestEntriesRegistry.Register(TimeSpanTestEntry.RegisterInstance);
            TestEntriesRegistry.Register(DateTimeTestEntry.RegisterInstance);
            TestEntriesRegistry.Register(DistanceTestEntry.RegisterInstance);
            TestEntriesRegistry.Register(FileSizeTestEntry.RegisterInstance);
        }
    }
}