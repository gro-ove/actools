using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public class RelayPropertyCommand : RelayCommand, INotifyPropertyChanged {
        public RelayPropertyCommand(Action<object> execute, Func<object, bool> canExecute = null)
                : base(execute, canExecute) { }

        public bool IsAbleToExecute => CanExecute(null);

        public override void OnCanExecuteChanged() {
            OnPropertyChanged(nameof(IsAbleToExecute));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}