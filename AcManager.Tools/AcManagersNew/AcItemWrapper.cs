using System;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    public class AcItemWrapper : NotifyPropertyChanged, IWithId {
        [CanBeNull]
        private readonly IAcWrapperLoader _loader;

        public AcItemWrapper(AcObjectNew value) {
            _isLoaded = true;
            _value = value;
        }

        public AcItemWrapper([NotNull]IAcWrapperLoader loader, [NotNull]AcPlaceholderNew initialValue) {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _value = initialValue ?? throw new ArgumentNullException(nameof(initialValue));
            IsLoaded = _value.GetType().IsSubclassOf(typeof(AcObjectNew));
        }

        public AcObjectNew Loaded() {
            if (IsLoaded) return (AcObjectNew)Value;
            if (_loader == null) throw new Exception(@"Loader is missing");
            _loader.Load(Value.Id);
            if (Value is AcObjectNew ret) return ret;
            throw new Exception($"Loading failure: {_loader}, {Value}");
        }

        public async Task<AcObjectNew> LoadedAsync() {
            if (IsLoaded) return (AcObjectNew)Value;
            if (_loader == null) throw new Exception(@"Loader is missing");
            await _loader.LoadAsync(Value.Id);
            if (Value is AcObjectNew ret) return ret;
            throw new Exception($"Loading failure: {_loader}, {Value}");
        }

        [NotNull]
        private AcPlaceholderNew _value;

        [NotNull]
        public AcPlaceholderNew Value {
            get => _value;
            internal set {
                if (value == null) throw new ArgumentNullException();
                if (Equals(value, _value)) return;

                var oldValue = _value;
                _value = value;

                IsLoaded = value.GetType().IsSubclassOf(typeof(AcObjectNew));

                OnPropertyChanged();
                OnPropertyChanged(nameof(Id));
                ValueChanged?.Invoke(this, new WrappedValueChangedEventArgs(oldValue, value));

                var oldAcObjectNew = oldValue as AcObjectNew;
                oldAcObjectNew?.Outdate();
            }
        }

        internal volatile object CurrentlyLoadingTask;

        private  bool _isLoaded;

        public bool IsLoaded {
            get => _isLoaded;
            private set => Apply(value, ref _isLoaded);
        }

        public override string ToString() {
            return Value.ToString();
        }

        public event WrappedValueChangedEventHandler ValueChanged;

        public static int CompareHelper(object x, object y) {
            var xw = (AcItemWrapper)x;
            var yw = (AcItemWrapper)y;

            if (xw.IsLoaded && yw.IsLoaded) {
                var xo = (AcObjectNew)xw.Value;
                var yo = (AcObjectNew)yw.Value;
                return xo.CompareTo(yo);
            }

            if (xw.IsLoaded) {
                return 1;
            }

            if (yw.IsLoaded) {
                return -1;
            }

            return string.Compare(xw.Value.Id, yw.Value.Id, StringComparison.CurrentCultureIgnoreCase);
        }

        public string Id => Value.Id;
    }
}
