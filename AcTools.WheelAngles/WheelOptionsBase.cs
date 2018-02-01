using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace AcTools.WheelAngles {
    public class WheelOptionsBase : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _loaded;

        public void EnsureLoaded() {
            if (!_loaded) {
                _loaded = true;
                _storage?.LoadValues(this);
            }
        }

        private static IWheelSteerLockOptionsStorage _storage;

        public static void SetStorage([CanBeNull] IWheelSteerLockOptionsStorage storage) {
            _storage = storage;
        }

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            _storage?.StoreValues(this);
        }
    }
}