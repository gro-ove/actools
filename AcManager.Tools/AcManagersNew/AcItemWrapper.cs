using System;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    public interface IAcWrapperLoader {
        /// <summary>
        /// Not recommended way, but can’t think of something better 
        /// at the moment. After all, it’s not that bad.
        /// </summary>
        /// <param name="id"></param>
        void Load(string id);

        Task LoadAsync(string id);
    }

    public class AcItemWrapper : NotifyPropertyChanged, IWithId {
        private readonly IAcWrapperLoader _loader;

        public AcItemWrapper([NotNull]IAcWrapperLoader loader, [NotNull]AcPlaceholderNew initialValue) {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (initialValue == null) throw new ArgumentNullException(nameof(initialValue));
            _loader = loader;
            _value = initialValue;
            IsLoaded = _value.GetType().IsSubclassOf(typeof(AcObjectNew));
        }

        public AcObjectNew Loaded() {
            if (IsLoaded) return (AcObjectNew)Value;
            _loader.Load(Value.Id);
            return (AcObjectNew)Value;
        }

        public async Task<AcObjectNew> LoadedAsync() {
            if (IsLoaded) return (AcObjectNew)Value;
            await _loader.LoadAsync(Value.Id);
            return (AcObjectNew)Value;
        }

        [NotNull]
        private AcPlaceholderNew _value;

        [NotNull]
        public AcPlaceholderNew Value {
            get { return _value; }
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

        private bool _isLoaded;

        public bool IsLoaded {
            get { return _isLoaded; }
            private set {
                if (Equals(value, _isLoaded)) return;
                _isLoaded = value;
                OnPropertyChanged();
            }
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

    public delegate void WrappedValueChangedEventHandler(object sender, WrappedValueChangedEventArgs args);

    public class WrappedValueChangedEventArgs : EventArgs {
        [NotNull]
        public readonly AcPlaceholderNew OldValue;

        [NotNull]
        public readonly AcPlaceholderNew NewValue;

        public WrappedValueChangedEventArgs([NotNull]AcPlaceholderNew oldValue, [NotNull]AcPlaceholderNew newValue) {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public class WrappedValueChangedEventManager : WeakEventManager {
        private WrappedValueChangedEventManager() {}
        
        public static void AddListener(IBaseAcObjectObservableCollection source, IWeakEventListener listener) {
            CurrentManager.ProtectedAddListener(source, listener);
        }
        
        public static void RemoveListener(IBaseAcObjectObservableCollection source, IWeakEventListener listener) {
            CurrentManager.ProtectedRemoveListener(source, listener);
        }
        
        protected override void StartListening(object source) {
            var typedSource = (IBaseAcObjectObservableCollection)source;
            typedSource.WrappedValueChanged += OnWrappedValueChanged;
        }
        
        protected override void StopListening(object source) {
            var typedSource = (IBaseAcObjectObservableCollection)source;
            typedSource.WrappedValueChanged -= OnWrappedValueChanged;
        }
        
        private static WrappedValueChangedEventManager CurrentManager {
            get {
                var managerType = typeof(WrappedValueChangedEventManager);
                var manager = (WrappedValueChangedEventManager)GetCurrentManager(managerType);
                if (manager != null) return manager;

                manager = new WrappedValueChangedEventManager();
                SetCurrentManager(managerType, manager);
                return manager;
            }
        }
        
        private void OnWrappedValueChanged(object sender, WrappedValueChangedEventArgs args) {
            // what is that?!
            Logging.Write("WHOA");
            DeliverEvent(sender, args);
        }
    }
}
