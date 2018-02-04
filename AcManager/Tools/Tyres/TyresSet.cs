using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Tools.Tyres {
    public sealed class TyresSet : Displayable, IDraggable {
        private int _index;

        public int Index {
            get => _index;
            set {
                if (Equals(value, _index)) return;
                _index = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Position));
            }
        }

        public int Position => Index + 1;

        private bool _dublicate;

        public bool Dublicate {
            get => _dublicate;
            set {
                if (Equals(value, _dublicate)) return;
                _dublicate = value;
                OnPropertyChanged();
            }
        }

        private bool _differentTyres;

        public bool DifferentTyres {
            get => _differentTyres;
            set {
                if (Equals(value, _differentTyres)) return;
                _differentTyres = value;
                OnPropertyChanged();
            }
        }

        private void UpdateDifferentTyres() {
            DifferentTyres = _front?.Name != _rear?.Name;
        }

        private bool _defaultSet;

        public bool DefaultSet {
            get => _defaultSet;
            set {
                if (Equals(value, _defaultSet)) return;
                _defaultSet = value;
                OnPropertyChanged();
            }
        }

        private TyresEntry _front;

        [NotNull]
        public TyresEntry Front {
            get => _front;
            set {
                if (Equals(value, _front)) return;
                _front = value;
                OnPropertyChanged();
                UpdateDifferentTyres();
            }
        }

        private TyresEntry _rear;

        [NotNull]
        public TyresEntry Rear {
            get => _rear;
            set {
                if (Equals(value, _rear)) return;
                _rear = value;
                OnPropertyChanged();
                UpdateDifferentTyres();
            }
        }

        #region Unique stuff
        private sealed class FrontRearEqualityComparer : IEqualityComparer<TyresSet> {
            public bool Equals(TyresSet x, TyresSet y) {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return TyresEntry.TyresEntryComparer.Equals(x._front, y._front) && TyresEntry.TyresEntryComparer.Equals(x._rear, y._rear);
            }

            public int GetHashCode(TyresSet obj) {
                unchecked {
                    return ((obj._front != null ? TyresEntry.TyresEntryComparer.GetHashCode(obj._front) : 0) * 397) ^
                            (obj._rear != null ? TyresEntry.TyresEntryComparer.GetHashCode(obj._rear) : 0);
                }
            }
        }

        public static IEqualityComparer<TyresSet> TyresSetComparer { get; } = new FrontRearEqualityComparer();
        #endregion

        private bool _isDeleted;

        public bool IsDeleted {
            get => _isDeleted;
            set {
                if (Equals(value, _isDeleted)) return;
                _isDeleted = value;
                OnPropertyChanged();
            }
        }

        private bool _canBeDeleted;

        public bool CanBeDeleted {
            get => _canBeDeleted;
            set {
                if (Equals(value, _canBeDeleted)) return;
                _canBeDeleted = value;
                OnPropertyChanged();
                _deleteCommand?.RaiseCanExecuteChanged();
            }
        }

        private DelegateCommand _deleteCommand;

        public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => { IsDeleted = true; }, () => CanBeDeleted));

        public TyresSet([NotNull] TyresEntry front, [NotNull] TyresEntry rear) {
            Front = front;
            Rear = rear;
        }

        public string GetName() {
            return Front.Name == Rear.Name ? Front.Name : $@"{Front.Name}/{Rear.Name}";
        }

        public string GetShortName() {
            return Front.ShortName == Rear.ShortName ? Front.ShortName : $@"{Front.ShortName}/{Rear.ShortName}";
        }

        public const string DraggableFormat = "X-TyresSet";

        string IDraggable.DraggableFormat => DraggableFormat;

        [CanBeNull]
        public static TyresSet GetOriginal(CarObject car) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.IsEmptyOrDamaged() != false) return null;

            var front = TyresEntry.Create(car, @"__CM_FRONT_ORIGINAL", true);
            var rear = TyresEntry.Create(car, @"__CM_REAR_ORIGINAL", true);
            if (front != null && rear != null) {
                return new TyresSet(front, rear);
            } else {
                return null;
            }
        }

        public static IEnumerable<TyresSet> GetSets(CarObject car) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.IsEmptyOrDamaged() != false) return new TyresSet[0];

            var defaultSet = tyres["COMPOUND_DEFAULT"].GetInt("INDEX", 0);
            return TyresEntry.GetTyres(car).Where(x => x.Item1 != null && x.Item2 != null).Select((x, i) => new TyresSet(x.Item1, x.Item2) {
                DefaultSet = i == defaultSet
            });
        }
    }
}