using System;
using System.Globalization;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Tools.Filters.TestEntries {
    public class TestEntryFactory : ITestEntry, ITestEntryFactory {
        private readonly TestEntryFactoryConvertation _convertation;

        ITestEntry ITestEntryFactory.Create(Operator op, string value) {
            return _convertation(value, out var meters) ? new TestEntryFactory(op, meters, _convertation) : null;
        }

        // For factory
        public TestEntryFactory([NotNull] TestEntryFactoryConvertation convertation) {
            _convertation = convertation;
        }

        private static bool IsPostfixChar(char c) {
            return char.IsLetter(c) || c == '/';
        }

        public static string GetPostfix(string value, int maxLength) {
            for (int j, i = 0; i < value.Length; i++) {
                if (!IsPostfixChar(value[i])) continue;
                for (j = i + 1; j < value.Length && j - i < maxLength && IsPostfixChar(value[j]); j++) { }
                return value.Substring(i, j - i).ToLowerInvariant().Replace("/", "");
            }

            return null;
        }

        public TestEntryFactory([NotNull] TestEntryFactoryPostfixMultiplier convertation, int maxPostfixLength = 4) {
            _convertation = (string value, out double valueToCompareWith) => {
                if (value == null) {
                    valueToCompareWith = 0d;
                    return false;
                }

                if (!FlexibleParser.TryParseDouble(value, out valueToCompareWith)) {
                    return false;
                }

                valueToCompareWith *= convertation(GetPostfix(value, maxPostfixLength));
                return true;
            };
        }

        private readonly Operator _op;
        private readonly double _metersValue;

        // For test entry
        private TestEntryFactory(Operator op, double metersValue, TestEntryFactoryConvertation convertation) {
            _op = op;
            _metersValue = metersValue;
            _convertation = convertation;
        }

        public override string ToString() {
            return _op.OperatorToString() + _metersValue.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(ITestEntryFactory factory) {}

        public bool Test(string value) {
            return _convertation(value, out var val) && Test(val);
        }

        public bool Test(double value) {
            switch (_op) {
                case Operator.Less:
                    return value < _metersValue;

                case Operator.LessEqual:
                    return value <= _metersValue;

                case Operator.More:
                    return value > _metersValue;

                case Operator.MoreEqual:
                    return value >= _metersValue;

                case Operator.Equal:
                    return Math.Abs(value - _metersValue) < 0.0001;

                default:
                    return false;
            }
        }

        public bool Test(bool value) {
            return Test(value ? 1.0 : 0.0);
        }

        public bool Test(TimeSpan value) {
            return Test(value.TotalSeconds);
        }

        public bool Test(DateTime value) {
            return Test(value.TimeOfDay);
        }
    }
}