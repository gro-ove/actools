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
            return Test(value > 0.0);
        }

        public bool Test(object value) {
            return value != null;
        }

        public bool Test(string value) {
            return value != null && Test(value != "");
        }
    }
}
