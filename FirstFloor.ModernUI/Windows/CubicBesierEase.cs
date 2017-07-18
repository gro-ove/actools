using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace FirstFloor.ModernUI.Windows {
    public class CubicBesierEase : EasingFunctionBase {
        private const int NewtonIterations = 4;
        private const double NewtonMinSlope = 0.001;
        private const double SubdivisionPrecision = 0.0000001;
        private const double SubdivisionMaxIterations = 10;

        private const int SplineTableSize = 11;
        private const double SampleStepSize = 1.0 / (SplineTableSize - 1.0);

        private static double A(double aA1, double aA2) {
            return 1.0 - 3.0 * aA2 + 3.0 * aA1;
        }

        private static double B(double aA1, double aA2) {
            return 3.0 * aA2 - 6.0 * aA1;
        }

        private static double C(double aA1) {
            return 3.0 * aA1;
        }

        // Returns x(t) given t, x1, and x2, or y(t) given t, y1, and y2.
        private static double CalcBezier(double aT, double aA1, double aA2) {
            return ((A(aA1, aA2) * aT + B(aA1, aA2)) * aT + C(aA1)) * aT;
        }

        // Returns dx/dt given t, x1, and x2, or dy/dt given t, y1, and y2.
        private static double GetSlope(double aT, double aA1, double aA2) {
            return 3.0 * A(aA1, aA2) * aT * aT + 2.0 * B(aA1, aA2) * aT + C(aA1);
        }

        private static double BinarySubdivide (double aX,  double aA, double aB, double mX1, double mX2) {
            double currentX, currentT;
            var i = 0;
            do {
                currentT = aA + (aB - aA) / 2d;
                currentX = CalcBezier(currentT, mX1, mX2) - aX;
                if (currentX > 0d) {
                    aB = currentT;
                } else {
                    aA = currentT;
                }
            } while (Math.Abs(currentX) > SubdivisionPrecision && ++i < SubdivisionMaxIterations);
            return currentT;
        }

        private static double NewtonRaphsonIterate (double aX, double aGuessT, double mX1, double mX2) {
            for (var i = 0; i < NewtonIterations; ++i) {
                var currentSlope = GetSlope(aGuessT, mX1, mX2);
                if (currentSlope == 0d) return aGuessT;

                var currentX = CalcBezier(aGuessT, mX1, mX2) - aX;
                aGuessT -= currentX / currentSlope;
            }
            return aGuessT;
        }

        private bool _dirty = true;

        private double GetTForX (double aX) {
            var intervalStart = 0d;
            var currentSample = 1;

            const int lastSample = SplineTableSize - 1;
            for (; currentSample != lastSample && _sampleValues[currentSample] <= aX; ++currentSample) {
                intervalStart += SampleStepSize;
            }

            --currentSample;

            // Interpolate to provide an initial guess for t
            var dist = (aX - _sampleValues[currentSample]) / (_sampleValues[currentSample + 1] - _sampleValues[currentSample]);
            var guessForT = intervalStart + dist * SampleStepSize;

            var initialSlope = GetSlope(guessForT, X1, X2);
            return initialSlope >= NewtonMinSlope ? NewtonRaphsonIterate(aX, guessForT, X1, X2) :
                    initialSlope == 0d ? guessForT : BinarySubdivide(aX, intervalStart, intervalStart + SampleStepSize, X1, X2);
        }

        private bool _linearMode;
        private double[] _sampleValues;

        private void UpdateValues() {
            var sampleValues = new double[SplineTableSize];
            _linearMode = X1 == Y1 && X2 == Y2;

            if (!_linearMode) {
                for (var i = 0; i < SplineTableSize; ++i) {
                    sampleValues[i] = CalcBezier(i * SampleStepSize, X1, X2);
                }
            }

            _sampleValues = sampleValues;
        }

        #region Properies
        public static readonly DependencyProperty X1Property = DependencyProperty.Register(nameof(X1), typeof(double),
                typeof(CubicBesierEase), new PropertyMetadata(0.4, (o, e) => {
                    var f = (CubicBesierEase)o;
                    f._x1 = (double)e.NewValue;
                    f._dirty = true;
                }));

        private double _x1 = 0.4;

        public double X1 {
            get => _x1;
            set => SetValue(X1Property, value);
        }

        public static readonly DependencyProperty X2Property = DependencyProperty.Register(nameof(X2), typeof(double),
                typeof(CubicBesierEase), new PropertyMetadata(0d, (o, e) => {
                    var f = (CubicBesierEase)o;
                    f._x2 = (double)e.NewValue;
                    f._dirty = true;
                }));

        private double _y1;

        public double Y1 {
            get => _y1;
            set => SetValue(Y1Property, value);
        }

        public static readonly DependencyProperty Y2Property = DependencyProperty.Register(nameof(Y2), typeof(double),
                typeof(CubicBesierEase), new PropertyMetadata(0.2, (o, e) => {
                    var f = (CubicBesierEase)o;
                    f._y2 = (double)e.NewValue;
                    f._dirty = true;
                }));

        private double _x2 = 0.2;

        public double X2 {
            get => _x2;
            set => SetValue(X2Property, value);
        }

        public static readonly DependencyProperty Y1Property = DependencyProperty.Register(nameof(Y1), typeof(double),
                typeof(CubicBesierEase), new PropertyMetadata(1d, (o, e) => {
                    var f = (CubicBesierEase)o;
                    f._y1 = (double)e.NewValue;
                    f._dirty = true;
                }));

        private double _y2 = 1d;

        public double Y2 {
            get => _y2;
            set => SetValue(Y2Property, value);
        }
        #endregion

        protected override double EaseInCore(double x) {
            if (_dirty) {
                UpdateValues();
                _dirty = false;
            }

            return _linearMode || x == 0d || x == 1d ? x :
                    CalcBezier(GetTForX(x), Y1, Y2);
        }

        protected override Freezable CreateInstanceCore() {
            return new CubicBesierEase();
        }

    }
}