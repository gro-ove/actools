using System;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    public class DiapasonMapper : NotifyPropertyChanged {
        private readonly Diapason<int> _diapason;
        private readonly Tuple<int, int>[] _spaceAndInterval;

        public int Size { get; }

        public DiapasonMapper([NotNull] Diapason<int> diapason) {
            var spaceAndInterval = new Tuple<int, int>[diapason.Pieces.Count];
            var start = 0;
            var total = 0;

            for (var i = 0; i < diapason.Pieces.Count; i++) {
                var piece = diapason.Pieces[i];
                var size = piece.ToValue - piece.FromValue;
                spaceAndInterval[i] = Tuple.Create(piece.FromValue - start, size);
                start = piece.ToValue;
                total += size;
            }

            _diapason = diapason;
            _spaceAndInterval = spaceAndInterval;

            Size = total;
        }

        public int GetClosest(int value) {
            return _diapason.TryToFindClosest(value, out var closest) ? closest : value;
        }

        public int MappedToActual(int mappedValue) {
            var actualValue = 0;
            for (var i = 0; i < _spaceAndInterval.Length; i++) {
                var t = _spaceAndInterval[i];
                int space = t.Item1, interval = t.Item2;

                actualValue += space;
                if (mappedValue <= interval) {
                    actualValue += mappedValue;
                    break;
                }

                actualValue += interval;
                mappedValue -= interval;
            }

            return actualValue;
        }

        public int ActualToMapped(int actualValue) {
            var mappedValue = 0;
            for (var i = 0; i < _spaceAndInterval.Length; i++) {
                var t = _spaceAndInterval[i];
                int space = t.Item1, interval = t.Item2;

                actualValue -= space;
                if (actualValue <= interval) {
                    if (actualValue > 0) {
                        mappedValue += actualValue;
                    }
                    break;
                }

                mappedValue += interval;
                actualValue -= interval;
            }

            return mappedValue;
        }

        private int _mappedValue;

        public int MappedValue {
            get => _mappedValue;
            set => Apply(value.Clamp(0, Size), ref _mappedValue, () => ActualValue = MappedToActual(value));
        }

        private int _actualValue;

        public int ActualValue {
            get => _actualValue;
            set => Apply(value, ref _actualValue, () => MappedValue = ActualToMapped(value));
        }
    }
}