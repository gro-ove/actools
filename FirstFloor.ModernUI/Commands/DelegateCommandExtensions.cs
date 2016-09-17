using System.ComponentModel;
using System.Windows;

namespace FirstFloor.ModernUI.Commands {
    // based on http://stackoverflow.com/a/1857619/4267982
    public static class DelegateCommandExtensions {
        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of some object,
        /// so that DelegateCommnand can update its IsEnabled property.
        /// </summary>
        public static ICommandExt ListenOn<T>(this ICommandExt delegateCommand, T observedObject, string propertyName) where T : INotifyPropertyChanged {
            observedObject.PropertyChanged += (sender, e) => {
                if (e.PropertyName == propertyName) {
                    delegateCommand.RaiseCanExecuteChanged();
                }
            };
            return delegateCommand;
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of some object,
        /// so that DelegateCommnand can update its IsEnabled property.
        /// </summary>
        public static ICommandExt ListenOnWeak<T>(this ICommandExt delegateCommand, T observedObject, string propertyName) where T : INotifyPropertyChanged {
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(observedObject, nameof(INotifyPropertyChanged.PropertyChanged),
                    (sender, e) => {
                        if (e.PropertyName == propertyName) {
                            delegateCommand.RaiseCanExecuteChanged();
                        }
                    });
            return delegateCommand;
        }
    }
}