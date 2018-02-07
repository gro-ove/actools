namespace AcTools.Utils {
    public class DoubleRange {
        private double _minimum = double.MaxValue;
        private double _maximum = double.MinValue;

        public double Minimum => _minimum;
        public double Maximum => _maximum;
        public double Range => _maximum - _minimum;

        public bool IsSet() {
            return !(_minimum > _maximum);
        }

        public virtual void Reset() {
            _minimum = double.MaxValue;
            _maximum = double.MinValue;
        }

        public void Update(double value) {
            if (_minimum > value) _minimum = value;
            if (_maximum < value) _maximum = value;
        }
    }
}