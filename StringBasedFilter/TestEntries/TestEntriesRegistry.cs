using System.Collections.Generic;
using JetBrains.Annotations;

namespace StringBasedFilter.TestEntries {
    public interface ITestEntryRegister {
        [CanBeNull]
        ITestEntry Create(Operator op, string value);

        bool TestValue([NotNull] string value);
        bool TestCommonKey([NotNull] string key);
    }

    public static class TestEntriesRegistry {
        private static readonly List<ITestEntryRegister> RegisteredList = new List<ITestEntryRegister>();

        public static void Register(ITestEntryRegister register) {
            RegisteredList.Add(register);
        }

        public static bool GetEntry(Operator op, string key, string value, out ITestEntry created) {
            foreach (var registered in RegisteredList) {
                if (registered.TestCommonKey(key) || registered.TestValue(value)) {
                    created = registered.Create(op, value) ?? new ConstTestEntry(false);
                    return true;
                }
            }

            created = null;
            return false;
        }
    }
}