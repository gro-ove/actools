namespace StringBasedFilter.TestEntries {
    internal class ConstTestEntry : ITestEntry {
        private readonly bool _value;

        public ConstTestEntry(bool b) {
            _value = b;
        }

        public override string ToString() {
            return "===" + _value;
        }

        public bool Test(bool value) {
            return _value;
        }

        public bool Test(double value) {
            return _value;
        }

        public bool Test(object value) {
            return _value;
        }

        public bool Test(string value) {
            return _value;
        }
    }
}
