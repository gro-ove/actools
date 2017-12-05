using System;

namespace StringBasedFilter.TestEntries {
    internal class ConstTestEntry : ITestEntry {
        private readonly bool _value;

        public ConstTestEntry(bool b) {
            _value = b;
        }

        public override string ToString() {
            return "===" + _value;
        }

        public void Set(ITestEntryFactory factory) {}

        public bool Test(bool value) {
            return _value;
        }

        public bool Test(TimeSpan value) {
            return _value;
        }

        public bool Test(DateTime value) {
            return _value;
        }

        public bool Test(double value) {
            return _value;
        }

        public bool Test(string value) {
            return _value;
        }
    }
}
