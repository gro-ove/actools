using System.Threading;

namespace AcTools.Utils {
    public static class InterlockExtra {
        public static double AddTo(this double value, ref double location) {
            var newCurrentValue = location; // non-volatile read, so may be stale
            while (true) {
                var currentValue = newCurrentValue;
                var newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location, newValue, currentValue);
                if (newCurrentValue == currentValue) {
                    return newValue;
                }
            }
        }
    }
}