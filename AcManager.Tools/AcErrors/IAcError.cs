using System.ComponentModel;
using System.Windows.Input;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.AcErrors {
    public interface IAcError : INotifyPropertyChanged {
        IAcObjectNew Target { get; }

        AcErrorCategory Category { get; }

        AcErrorType Type { get; }

        string Message { get; }

        ICommand StartErrorFixerCommand { get; }
    }
}