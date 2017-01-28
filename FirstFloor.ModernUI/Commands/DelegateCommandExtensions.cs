using System.ComponentModel;
using System.Windows;

namespace FirstFloor.ModernUI.Commands {
    // based on http://stackoverflow.com/a/1857619/4267982
    public static class DelegateCommandExtensions {
        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of CommandBase object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOn<TObj>(this TObj delegateCommand, CommandBase observedCommand) where TObj : CommandBase {
            return delegateCommand.ListenOn(observedCommand, nameof(CommandBase.IsAbleToExecute));
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of CommandBase object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOnWeak<TObj>(this TObj delegateCommand, CommandBase observedCommand) where TObj : CommandBase {
            return delegateCommand.ListenOnWeak(observedCommand, nameof(CommandBase.IsAbleToExecute));
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of some object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOn<T, TObj>(this TObj delegateCommand, T observedObject, string propertyName) where T : INotifyPropertyChanged
                where TObj : CommandBase {
            observedObject.PropertyChanged += (sender, e) => {
                if (e.PropertyName == propertyName) {
                    delegateCommand.RaiseCanExecuteChanged();
                }
            };
            return delegateCommand;
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of some object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOnWeak<T, TObj>(this TObj delegateCommand, T observedObject, string propertyName) where T : INotifyPropertyChanged
                where TObj : CommandBase {
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