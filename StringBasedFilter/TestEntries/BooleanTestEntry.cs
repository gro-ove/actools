using System;

namespace StringBasedFilter.TestEntries {
    internal class BooleanTestEntry : ITestEntry {
        private readonly bool _value;

        public BooleanTestEntry(bool b) {
            _value = b;
        }

        public override string ToString() {
            return "==" + _value;
        }

        public bool Test(bool value) {
            return value == _value;
        }

        public bool Test(double value) {
            return _value != Equals(value, 0.0);
        }

        public bool Test(string value) {
            return _value != string.IsNullOrEmpty(value);
        }
        
        public bool Test(TimeSpan value) {
            return Test(value > default(TimeSpan));
        }

        public bool Test(DateTime value) {
            return Test(value > default(DateTime));
        }
    }
}
